module SkeletonProcessing

open System
open System.Windows
open Microsoft.Kinect
open Microsoft.Kinect
open System.Diagnostics
open System.Reactive.Linq
open System.Windows.Media.Imaging
open WpfApplication1

let sensor = try
                 KinectSensor.KinectSensors.[0]
             with :? System.ArgumentOutOfRangeException as e -> 
                 printfn "Kinect not found. Check connection and restart"
                 exit 0
                 null

let skeletons = Array.zeroCreate 6
let window = new MainWindow()
let UIDispatcher = window.form.Dispatcher

let postToUI (f: unit -> unit) = window.Dispatcher.InvokeAsync f |> ignore
let ToPoint(s,c) = sensor.CoordinateMapper.MapSkeletonPointToColorPoint(s, c)
let sendInstructionMessage message = postToUI <| fun () -> window.instructionMessage.Content <- message

let trackChecker color = postToUI <| fun () -> window.ifTracked.Fill <- new Media.SolidColorBrush(Color = color)

trackChecker <| Media.Color.FromRgb(200uy, 50uy, 50uy)

let ColorFormat = ColorImageFormat.RgbResolution640x480Fps30

let getPoints (skeleton: Skeleton) =
    let l = skeleton.Joints.[JointType.HandLeft]
    let r = skeleton.Joints.[JointType.HandRight]
    let leftHand = ToPoint(l.Position, ColorFormat)
    let rightHand = ToPoint(r.Position, ColorFormat)
    trackChecker <| Media.Color.FromRgb(119uy, 176uy, 203uy)
    (leftHand.Y, rightHand.Y)

let inline isTracked (skeleton : Skeleton) = skeleton.TrackingState = SkeletonTrackingState.Tracked

let ExtractHands playerId (args: SkeletonFrameReadyEventArgs) = 
    use frame = args.OpenSkeletonFrame()
    if frame = null then None 
    else 
        frame.CopySkeletonDataTo(skeletons)
        let player = skeletons.[playerId]
        if isTracked player then 
            Some (getPoints player)
        else None