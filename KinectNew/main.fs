open System
open System.Windows
open System.Reactive.Linq
open WpfApplication1
open GameMode
open SkeletonProcessing
open Connection
open Graphics
open Microsoft.Kinect
open Kinect.Toolbox

                                     
printfn "Press Enter to run the app"
Console.ReadLine() |> ignore
window.Loaded.AddHandler(new RoutedEventHandler(WindowLoaded))
window.Show()
printfn "LOLOLOL"
window.Unloaded.AddHandler(new RoutedEventHandler(WindowUnloaded))


let breakConnectionDispose = 
    window.conectionButton.Unchecked
    |> Observable.subscribe 
        (fun _ -> 
            sendInstructionMessage "No connection with trik. Check IP, Port and restart"
            sender <- null)

[<EntryPoint;STAThread>]
let main _ =
    let kinect = tryAccessKinect()
    window.conectionButton.Checked.Subscribe(fun _ -> if setTrikConnection() then printfn "ASDASD";startKinectApp kinect) |> ignore
    
    let skeletonFrame = kinect.SkeletonFrameReady.Select ProccesSkeletonFrame
    
    
    let getPlayer n = 
        if n < 1 then failwith "Wrong Player number"
        else  skeletonFrame 
              |> Observable.choose (fun x -> if x.Length >= n then Some x.[n - 1] else None)

    let player1 = getPlayer 1
    let player2 = getPlayer 2
    let readytoSend controller (commands: IObservable<_>) = commands.Subscribe (SendRequest controller)
    let StartGame (player: IObservable<_>) controller = 
        player.Select(getPoints) 
        |> toFlappingHands
        |> readytoSend controller

    let disp1 = StartGame player1 0
    let disp2 = StartGame player2 1

    use videoDisp = kinect.ColorFrameReady.Subscribe ColorFrameReady
    let app = new Application()
    app.Run(window)

