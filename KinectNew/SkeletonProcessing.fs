module SkeletonProcessing

open System
open System.Windows
open Microsoft.Kinect
open System.Diagnostics
open System.Reactive.Linq
open System.Windows.Media.Imaging
open WpfApplication1

let sensor = try
                 KinectSensor.KinectSensors.[0]
             with :? System.ArgumentOutOfRangeException as e -> 
                 printfn "Kinect not found. Check connection and restart"
                 exit(0)
                 null

let skeletons : Skeleton array = [| for i in [1..6] -> new Skeleton() |]
let window = new MainWindow()
let runOnThisThread (ui: UIElement) f = ui.Dispatcher.Invoke(new System.Action (f), null) |> ignore
let ToPoint(s,c) = 
            sensor.CoordinateMapper.MapSkeletonPointToColorPoint(s, c)
let ColorFormat = ColorImageFormat.RgbResolution640x480Fps30

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