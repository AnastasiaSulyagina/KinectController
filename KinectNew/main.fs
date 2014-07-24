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
let mutable mode = "floatingMode" 
let ToPoint(s,c) = sensor.CoordinateMapper.MapSkeletonPointToColorPoint(s, c)
let ColorFormat = ColorImageFormat.RgbResolution640x480Fps30


let mutable port = window.portBox.Text
let mutable ipAddr = window.IPBox.Text
let mutable sender = try
                        new TcpClient(ipAddr, Convert.ToInt32(port))
                     with :? SocketException as e ->
                        printfn "No connection with trik. Check connection and restart"
                        exit(0) 
                        null

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
//                let bmap = new Bitmap(640, 480, Imaging.PixelFormat.Format32bppRgb)
//                let bmapdata = bmap.LockBits(
//                                    new Rectangle(0, 0, 640, 480),
//                                    Imaging.ImageLockMode.WriteOnly, 
//                                    bmap.PixelFormat)
//                let ptr = bmapdata.Scan0;
//                Marshal.Copy(pixelData, 0, ptr, videoFrame.PixelDataLength)
//                bmap.UnlockBits(bmapdata)
                runOnThisThread window.image <| fun() -> 
                    window.image.Source <- BitmapSource.Create(640, 480, 96.0, 96.0, Media.PixelFormats.Bgr32, null, pixelData, 640 * 4)
                  
let hands = sensor.SkeletonFrameReady |> Observable.choose ExtractHands 
   
let calibrate (x: int) =
    runOnThisThread window.instructionMessage <| fun() -> 
        window.instructionMessage.Content <- "Calibration. Put your hands up and down slowly for a few seconds"
    maxHeight <- Math.Max(maxHeight, float x)
    minHeight <- Math.Min(minHeight, float x)
    printfn "%A\n" maxHeight

let changeMode (args: Controls.SelectionChangedEventArgs) =
    let selected: System.Windows.Controls.ComboBoxItem = args.AddedItems.[0] :?> System.Windows.Controls.ComboBoxItem
    if selected.Content :?> string = "flapping mode" then 
        mode <- "flappingMode"
        window.instructionMessage.Content <- "Flap your hands quickly to move the robot"
    else
        mode <- "floatingMode"
        window.instructionMessage.Content <- "Put your hands up and down for speed regulating"

let toDifference prev x =
    let res = abs(x - !prev)
    prev := x
    res 

let floatingScale (x: float) = 
    ((x - minHeight) / (maxHeight - minHeight)) * 100.0

let flappingScale (x: int) = 
    if x < 2 * eps then 0
    elif x > 100 then 100
    else x

let toFloatingHand (splitter: int*int -> int) =
    hands |> Observable.map splitter 
          |> fun x -> Observable.Buffer(x, 20, 1)
          |> Observable.map (System.Linq.Enumerable.Average)
          |> Observable.filter (fun x -> x < minHeight && x > maxHeight)
          |> Observable.map (floatingScale >> int)

let toFlappingHand splitter = 
    hands |> Observable.map (splitter >> toDifference (ref 0)) 
          |> fun x -> Observable.Buffer(x, 20, 5)
          |> Observable.map (System.Linq.Enumerable.Average >> (*) 3.7 >> int)
          |> Observable.filter ((<) eps) 
          |> Observable.map (flappingScale)

let left, right = if mode = "flappingMode"
                    then toFlappingHand fst, toFlappingHand snd
                  else toFloatingHand fst, toFloatingHand snd

let SendRequest (request: Collections.Generic.IList<int>) =
    runOnThisThread window.instructionMessage <| fun() -> 
        window.instructionMessage.Content <- ""
    let req = [|Convert.ToByte((request.[0]) : int); Convert.ToByte((request.[1]) : int); 48uy; 48uy|]
    sender.GetStream().Write(req, 0, req.Length)
   
let ts = new TimeSpan(0, 0, 3)
let tm = DateTimeOffset.Now + ts

left.TakeUntil(tm).Subscribe calibrate |> ignore
right.TakeUntil(tm).Subscribe calibrate |> ignore
let handsDispose = Observable.CombineLatest([left; right]) 
                    |> fun x -> Observable.SkipUntil(x, tm)
                    |> Observable.subscribe SendRequest                

window.modeChooser.SelectionChanged |> Observable.subscribe changeMode 
                                    |> ignore

let colorDispose = sensor.ColorFrameReady 
                   |> Observable.subscribe ColorFrameReady
        
let WindowLoaded (sender : obj) (args: EventArgs) = 
    async {
        sensor.Start()
        sensor.ColorStream.Enable()
        sensor.SkeletonStream.Enable()
        sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30)
        colorDispose |> ignore
    }
    |> Async.Start 
 
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