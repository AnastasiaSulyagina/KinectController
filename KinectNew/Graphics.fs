module Graphics

open System
open System.Windows
open Microsoft.Kinect
open System.Windows.Media.Imaging
open WpfApplication1
open SkeletonProcessing
open GameMode

let sensor = try
                 KinectSensor.KinectSensors.[0]
             with :? System.ArgumentOutOfRangeException as e -> 
                 printfn "Kinect not found. Check connection and restart"
                 exit 0
                 null


let window = new MainWindow()

let mvvm = new MVVM();
window.DataContext <- mvvm

//let mutable mode = (window.modeChooser.SelectedValue :?> GameMode)


let postToUI (f: unit -> unit) = window.Dispatcher.InvokeAsync f |> ignore

let sendInstructionMessage message = postToUI <| fun () -> window.instructionMessage.Content <- message

let ifTrackedSet color = postToUI <| fun () -> window.ifTracked.Fill <- new Media.SolidColorBrush(Color = color)



ifTrackedSet <| Media.Color.FromRgb(200uy, 50uy, 50uy)

let private allSkeletons: Skeleton[] = Array.zeroCreate 6

let ProccesSkeletonFrame (ev: SkeletonFrameReadyEventArgs) =     
    use frame = ev.OpenSkeletonFrame()
    if frame = null then 
        failwith "frame is null"
    else 
        frame.CopySkeletonDataTo(allSkeletons)
        let trackedSkeletons = 
            allSkeletons |> Array.choose (fun x -> if isSkeletonTracked x then Some x else None)
        mvvm.TrackedSkeletons <- trackedSkeletons |> Array.map (fun x -> x.TrackingId)
        trackedSkeletons
        
         

let pixelData : byte array = Array.zeroCreate 1228800//sensor.ColorStream.FramePixelDataLength  


let ColorFrameReady (args: ColorImageFrameReadyEventArgs) =   
    use frame = args.OpenColorImageFrame()          
    if frame <> null then
       frame.CopyPixelDataTo(pixelData)
       let inline videoRender() = 
           window.image.Source <- BitmapSource.Create(640, 480, 96.0, 96.0, Media.PixelFormats.Bgr32, null, pixelData, 640 * 4)
       postToUI videoRender


let WindowLoaded (sender : obj) (args: EventArgs) = 
   sendInstructionMessage "Enter trik IP and Port. Then press the connection button to start"
 
let WindowUnloaded (sender : obj) (args: EventArgs) = 
    sensor.Stop()



(*let mutable handsDispose = FlappyHands
                            //|> fun x -> Observable.SkipUntil(x, tm)
                            |> Observable.subscribe SendRequest                


let changeMode args =
    mode <- window.modeChooser.SelectedValue :?> GameMode
    sensor.SkeletonStream.Disable()
    handsDispose.Dispose()
    if mode = GameMode.Flapping then
        handsDispose <- FlappyHands.Subscribe(SendRequest)
    else 
        handsDispose <- FloatingHands.Subscribe(SendRequest)
    
    sensor.SkeletonStream.Enable()
    postToUI <| fun() -> window.instructionMessage.Visibility <- Visibility.Hidden
    postToUI <| fun() -> window.modeBox.Visibility <- Visibility.Visible

let modesDispose = window.modeChooser.SelectionChanged 
                    |> Observable.subscribe changeMode 

let establishConnectionDispose = window.conectionButton.Checked
                                    |> Observable.subscribe setConnection

let breakConnectionDispose = 
    window.conectionButton.Unchecked
    |> Observable.subscribe 
        (fun _ -> 
            sendInstructionMessage "No connection with trik. Check IP, Port and restart"
            sender <- null)

            *)

