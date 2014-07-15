open System
open System.Windows
open System.Windows.Media.Imaging
open System.Drawing
open Microsoft.Kinect
open System.Diagnostics
open System.Reactive.Linq
open System.Net.Sockets
open System.Windows.Forms

let sensor = KinectSensor.KinectSensors.[0]

let pixelData : byte array = Array.zeroCreate sensor.ColorStream.FramePixelDataLength 
let skeletons : Skeleton array = [| for i in [1..6] -> new Skeleton() |]
let mutable skeletonHistory : Skeleton list = []

let mutable minHeight = 800
let mutable maxHeight = 0
let mutable amplitude = 0
let mutable calibration = true
let mutable cnt = 0
let eps = 10
let ToPoint(s,c) = sensor.CoordinateMapper.MapSkeletonPointToColorPoint(s, c)
let ColorFormat = ColorImageFormat.RgbResolution640x480Fps30
let runOnThisThread (ui: UIElement) f = ui.Dispatcher.Invoke(new System.Action (f), null) |> ignore

let mutable rightBrush = System.Windows.Media.Brushes.DarkTurquoise
let mutable leftBrush = System.Windows.Media.Brushes.DarkTurquoise

let rightH = new System.Windows.Shapes.Rectangle(Height = 40.0, Width = 40.0, Fill = rightBrush)
let leftH = new System.Windows.Shapes.Rectangle(Height = 40.0, Width = 40.0, Fill = leftBrush)

let grid = new System.Windows.Controls.Grid()
let canvas = new System.Windows.Controls.Canvas(Background = System.Windows.Media.Brushes.Transparent) 
let image = new System.Windows.Controls.Image(Height = 600.0, Width = 800.0)

let port = 2000
let ipAddr = "192.168.1.1"
let mutable sender = new TcpClient(ipAddr, port)

grid.Children.Add(image) |> ignore
grid.Children.Add(canvas) |> ignore
canvas.Children.Add(rightH) |> ignore
canvas.Children.Add(leftH) |> ignore

let GetPoints (skeleton: Skeleton) =
    let leftHand = ToPoint(skeleton.Joints.[JointType.HandLeft].Position, ColorFormat)
    let rightHand = ToPoint(skeleton.Joints.[JointType.HandRight].Position, ColorFormat)
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
            runOnThisThread image <| fun () -> 
                image.Source <- BitmapSource.Create(640, 480, 96.0, 96.0, Media.PixelFormats.Bgr32, null, pixelData, 640 * 4)
                        
let hands = sensor.SkeletonFrameReady |> Observable.choose ExtractHands 
   
let toDifference prev x =
    let res = abs(x - !prev)
    prev := x
    res 

let toHand splitter = 
    let h = hands 
            |> Observable.map splitter 
            |> Observable.map (toDifference (ref 0)) 
    h.Buffer(20, 5)
    |> Observable.map (System.Linq.Enumerable.Average >> (*) 3.5 >> int)
    |> Observable.filter ((<) eps) 

let left, right = toHand fst, toHand snd

let ChangeColor motor x = 
    let scale = Convert.ToByte(x / 100 * 255)
    if motor = 1 then
        leftBrush.Color <- Media.Color.FromArgb(100uy, scale ,scale, scale)
    else 
        rightBrush.Color <- Media.Color.FromArgb(100uy, scale ,scale, scale)

let SendRequest request =
    let pow = if snd request < 2 * eps then 0 
              elif snd request > 100 then 100 
              else snd request
    ChangeColor (fst request) pow
    let req = [|Convert.ToByte((fst request) : int); Convert.ToByte((pow) : int)|]
    sender.GetStream().Write(req, 0, req.Length)

let SendToLeftMotor x =
    (1, x) |> SendRequest

let SendToRightMotor x =
    (2, x) |> SendRequest

left.Subscribe SendToLeftMotor |> ignore
right.Subscribe SendToRightMotor |> ignore

sensor.ColorFrameReady |> Observable.subscribe ColorFrameReady
        |> ignore  
        
let WindowLoaded (sender : obj) (args: RoutedEventArgs) = 
    async {
        sensor.Start()
        sensor.ColorStream.Enable()
        sensor.SkeletonStream.Enable()
        sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30)
    }
    |> Async.Start 
 
let WindowUnloaded (sender : obj) (args: RoutedEventArgs) = 
    SendRequest(0, 0)
    sensor.Stop()

let window = new Window(Height = 650.0, Width = 800.0, Title = "Kinect Application")
window.Loaded.AddHandler(new RoutedEventHandler(WindowLoaded))
window.Unloaded.AddHandler(new RoutedEventHandler(WindowUnloaded))
window.Content <- grid
window.Show()

[<STAThread()>]
do 
    let app = new Application() in
    app.Run(window) |> ignore
