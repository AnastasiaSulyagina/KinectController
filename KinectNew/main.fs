open System
open System.Windows
open System.Drawing
open Microsoft.Kinect
open System.Diagnostics
open System.Reactive.Linq
open System.Net.Sockets
open System.Runtime.InteropServices
open System.Windows.Media.Imaging
open WpfApplication1
open GameMode
let sensor = try
                 KinectSensor.KinectSensors.[0]
             with :? System.ArgumentOutOfRangeException as e -> 
                 printfn "Kinect not found. Check connection and restart"
                 exit(0)
                 null

let runOnThisThread (ui: UIElement) f = ui.Dispatcher.Invoke(new System.Action (f), null) |> ignore
let pixelData : byte array = Array.zeroCreate sensor.ColorStream.FramePixelDataLength 
let skeletons : Skeleton array = [| for i in [1..6] -> new Skeleton() |]
let window = new MainWindow()

let mutable minHeight = 600.0
let mutable maxHeight = 0.0
let eps = 10
let mutable mode = (window.modeChooser.SelectedValue :?> GameMode)
let ToPoint(s,c) = sensor.CoordinateMapper.MapSkeletonPointToColorPoint(s, c)
let ColorFormat = ColorImageFormat.RgbResolution640x480Fps30

let mutable port = window.portBox.Text
let mutable ipAddr = window.IPBox.Text
let mutable sender = null

let GetPoints (skeleton: Skeleton) =
    let l = skeleton.Joints.[JointType.HandLeft]
    let r = skeleton.Joints.[JointType.HandRight]
    let leftHand = ToPoint(l.Position, ColorFormat)
    let rightHand = ToPoint(r.Position, ColorFormat)
    runOnThisThread window.ifTracked <| fun() -> 
        window.ifTracked.Fill <- new Media.SolidColorBrush(Color = Media.Color.FromRgb(119uy, 176uy, 203uy))
    (leftHand.Y, rightHand.Y)

let ExtractHands (args: SkeletonFrameReadyEventArgs) = 
    use frame = args.OpenSkeletonFrame()
    match frame with
        | null -> None
        | skeletonFrame ->  skeletonFrame.CopySkeletonDataTo(skeletons)
                            skeletons |> Array.tryFind(fun x -> x.TrackingState = SkeletonTrackingState.Tracked)
    |> Option.map GetPoints

let ColorFrameReady (args: ColorImageFrameReadyEventArgs) =      
    use frame = args.OpenColorImageFrame()          
    match frame with
        | null -> ()
        | videoFrame -> 
                videoFrame.CopyPixelDataTo(pixelData)
                runOnThisThread window.image <| fun() -> 
                    window.image.Source <- BitmapSource.Create(640, 480, 96.0, 96.0, Media.PixelFormats.Bgr32, null, pixelData, 640 * 4)



let setConnection args = 

    runOnThisThread window.instructionMessage <| fun() -> 
        window.instructionMessage.Content <- "Establishing connection. Please wait ..."
    sender <- try
                runOnThisThread window.instructionMessage <| fun() -> 
                    window.instructionMessage.Content <- "Trik connected. Choose the mode and start playing."
                new TcpClient(ipAddr, Convert.ToInt32(port))
                with :? SocketException as e ->
                runOnThisThread window.instructionMessage <| fun() -> 
                    window.instructionMessage.Content <- "No connection with trik. Check IP, Port and restart"
                    window.conectionButton.IsChecked <- new Nullable<bool>(false)
                null
    if sender <> null then
        let startApp =
            async {
                sensor.Start()
                sensor.ColorStream.Enable()
                sensor.SkeletonStream.Enable()
                sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30)
            }
            |> Async.Start 
            window.modeChooser.IsEnabled <- true
        startApp |> ignore


let hands = sensor.SkeletonFrameReady |> Observable.choose ExtractHands 
   
let calibrate (x: int) =
    runOnThisThread window.instructionMessage <| fun() -> 
        window.instructionMessage.Content <- "Calibration. Put your hands up and down slowly for a few seconds"
    maxHeight <- Math.Max(maxHeight, float x)
    minHeight <- Math.Min(minHeight, float x)
    printfn "%A %A\n" maxHeight minHeight

let toDifference prev x =
    let res = abs(x - !prev)
    prev := x
    res 

let floatingScale (x: float) = 
    int <| ((x - minHeight) * 100.0 / (maxHeight - minHeight)) 

let flappingScale (x: int) = 
    if x < 2 * eps then 0
    elif x > 100 then 100
    else x

let toMovingHand (splitter: int*int->int) =
//    let flappingSpeed (data: Collections.Generic.IList<_>) = 
//        let a = Array.zeroCreate data.Count 
//        data.CopyTo(a, 0)
//        let mutable m = 0
//        for i = 1 to a.Length - 1 do
//            let d = 3 * (abs <| a.[i] - a.[i - 1])
//            m <- max m d 
//        m
//
    let h = hands |> Observable.map splitter
    let h' = if mode = GameMode.Flapping then
                  h
                  |> Observable.map (toDifference (ref 0))
                  |> fun x -> Observable.Buffer(x, 20, 5)
                  |> Observable.map (System.Linq.Enumerable.Average >> (*) 3.7 >> int)
                  |> Observable.filter ((<) eps) 
                  |> Observable.map flappingScale
             else h
                  |> fun x -> Observable.Buffer(x, 20, 1)
                  |> Observable.map System.Linq.Enumerable.Average
                  |> Observable.filter (fun x -> x < minHeight && x > maxHeight)
                  |> Observable.map floatingScale
    h'

let left, right = toMovingHand fst, toMovingHand snd

let changeMode args =
    mode <- window.modeChooser.SelectedValue :?> GameMode
    runOnThisThread window.instructionMessage <| fun() -> 
        window.instructionMessage.Visibility <- Visibility.Hidden
    runOnThisThread window.modeBox <| fun() -> 
        window.modeBox.Visibility <- Visibility.Visible

let SendRequest (request: Collections.Generic.IList<int>) =
    runOnThisThread window.instructionMessage <| fun() -> 
        window.instructionMessage.Content <- ""
    let req = [|Convert.ToByte((request.[0]) : int); Convert.ToByte((request.[1]) : int); 48uy; 48uy|]
    if sender <> null then
        sender.GetStream().Write(req, 0, req.Length)
    else 
        runOnThisThread window.instructionMessage <| fun() -> 
                    window.instructionMessage.Content <- "No connection with trik. Check IP, Port and restart"
   
let ts = new TimeSpan(0, 0, 15)
let tm = DateTimeOffset.Now + ts
left.TakeUntil(tm)|>
    Observable.subscribe calibrate |> ignore
right.TakeUntil(tm)|> 
    Observable.subscribe calibrate |> ignore
let handsDispose = Observable.CombineLatest([left; right]) 
                    |> fun x -> Observable.SkipUntil(x, tm)
                    |> Observable.subscribe SendRequest                

let modesDispose = window.modeChooser.SelectionChanged 
                    |> Observable.subscribe changeMode 

let establishConnectionDispose = window.conectionButton.Checked
                                    |> Observable.subscribe setConnection

let breakConnectionDispose = window.conectionButton.Unchecked
                                    |> Observable.subscribe (fun x -> 
                                                                runOnThisThread window.instructionMessage <| fun() -> 
                                                                    window.instructionMessage.Content <- "No connection with trik. Check IP, Port and restart"
                                                                sender <- null)

let colorDispose = sensor.ColorFrameReady 
                    |> Observable.subscribe ColorFrameReady
        
let WindowLoaded (sender : obj) (args: EventArgs) = 
    runOnThisThread window.instructionMessage <| fun() -> 
        window.instructionMessage.Content <- "Enter trik IP and Port. Then press the connection button to start"
 
let WindowUnloaded (sender : obj) (args: EventArgs) = 
    sensor.Stop()

printfn "Press Enter to run the app"
Console.ReadLine() |> ignore

window.Loaded.AddHandler(new RoutedEventHandler(WindowLoaded))
window.Show()

window.Unloaded.AddHandler(new RoutedEventHandler(WindowUnloaded))

[<STAThread()>]
do
    let app = new Application() in
    app.Run(window) |> ignore