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
open Microsoft.FSharp.Collections
                                     
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
    window.conectionButton.Checked.Subscribe(fun _ -> if setTrikConnection() then startKinectApp kinect) |> ignore
    
    let skeletonFrame = kinect.SkeletonFrameReady.Select ExtractTrackedSkeletons
    
    let inline trackingId (x: Skeleton) = x.TrackingId


    let skeltonsId = skeletonFrame |> Observable.map (Array.map trackingId) |> Observable.DistinctUntilChanged

    use printer = skeltonsId.Subscribe(fun ts -> mvvm.TrackedSkeletons <- ts)

    let activeSkeletons = new Collections.Generic.List<int * IDisposable>(5)
    let safeAddSub a = lock activeSkeletons <| fun () -> activeSkeletons.Add a            
    let safeRemove id = lock activeSkeletons <| fun () -> 
            let i = activeSkeletons.FindIndex( fun (i,_) -> i = id)
            if i >= 0 then let _,d = activeSkeletons.[i]
                           activeSkeletons.RemoveAt(i)
                           d.Dispose()


    let joinPlayer n = 
        let robot = 0
        let points = skeletonFrame |> Observable.choose (Array.tryFind (fun x -> trackingId x = n))
                      |> Observable.choose getPoints 
        let kick = ref false            
        let d1 = points |> toFlappingHands |> Observable.subscribe(sendRequest robot (fun () -> safeRemove n) kick)
        let d2 = points |> Observable.map (fun ((l,r),_) -> 10 > abs (l - r)) |> Observable.DistinctUntilChanged
                 |> Observable.subscribe (fun _ -> (:=) kick true)
        new Reactive.Disposables.CompositeDisposable(d1, d2)
    
    let updateSubscriptions (ids : int[]) =
            for id in ids do
                let i = activeSkeletons.FindIndex(fun (i,_) -> i = id)
                if i < 0 then
                    safeAddSub (id, joinPlayer id)

            for k,_ in activeSkeletons |> Linq.Enumerable.ToList do
                match ids |> Array.tryFind ((=) k) with
                | Some _ -> ()
                | None -> safeRemove k

    use subscriptionManager = skeltonsId.Subscribe(updateSubscriptions)

    use videoDisp = kinect.ColorFrameReady.Subscribe ColorFrameReady
    let app = new Application()
    app.Run(window)