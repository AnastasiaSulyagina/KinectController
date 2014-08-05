module main

open System
open System.Windows
open System.Reactive.Linq
open WpfApplication1
open GameMode
open SkeletonProcessing
open Connection
open Graphics

let mutable minHeight = 20.0
let mutable maxHeight = 400.0
let eps = 10
let mutable mode = (window.modeChooser.SelectedValue :?> GameMode)
let hands = sensor.SkeletonFrameReady |> Observable.choose ExtractHands 
   
let calibrate (handPositions: Collections.Generic.IList<int>) =
    printfn "%A %A calibration\n" maxHeight minHeight

let toDifference prev x =
    let res = abs(x - !prev)
    prev := x
    res 

let floatingScale (x: float) = 
    int <| 100.0 - ((x - minHeight) * 100.0 / (maxHeight - minHeight)) 

let flappingScale (x: int) = 
    if x < 2 * eps then 0
    elif x > 100 then 100
    else x

let toFlappingHand (splitter: int*int->int) = 
    hands |> Observable.map splitter 
          |> Observable.map (toDifference (ref 0))
          |> fun x -> Observable.Buffer(x, 20, 5)
          |> Observable.map (System.Linq.Enumerable.Average >> (*) 4.3 >> int)
          |> Observable.filter ((<) eps) 
          |> Observable.map flappingScale

let toFloatingHand (splitter: int*int->int) =    
    hands |> Observable.map splitter
          |> fun x -> Observable.Buffer(x, 20, 2)
          |> Observable.map System.Linq.Enumerable.Average
          |> Observable.filter (fun x -> x > minHeight && x < maxHeight)
          |> Observable.map floatingScale

let FlappyHands = Observable.CombineLatest([toFlappingHand fst; toFlappingHand snd])
let FloatingHands = Observable.CombineLatest([toFloatingHand fst; toFloatingHand snd])

let SendRequest (request: Collections.Generic.IList<int>) =
    runOnThisThread window.instructionMessage <| fun() -> 
        window.instructionMessage.Content <- ""
    printfn "%A %A %A\n" request.[0] request.[1] (mode = GameMode.Flapping)
    let req = [|Convert.ToByte((request.[0]) : int); Convert.ToByte((request.[1]) : int); 48uy; 48uy|]
    if sender <> null then
        sender.GetStream().Write(req, 0, req.Length)
    else 
        runOnThisThread window.instructionMessage <| fun() -> 
                    window.instructionMessage.Content <- "No connection with trik. Check IP, Port and restart"
   
let ts = new TimeSpan(0, 0, 15)
let tm = DateTimeOffset.Now + ts

//FloatingHands.TakeUntil(tm)|>
//    Observable.subscribe calibrate |> ignore

let mutable handsDispose = FlappyHands
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
    runOnThisThread window.instructionMessage <| fun() -> 
        window.instructionMessage.Visibility <- Visibility.Hidden
    runOnThisThread window.modeBox <| fun() -> 
        window.modeBox.Visibility <- Visibility.Visible

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
                                                                
printfn "Press Enter to run the app"
Console.ReadLine() |> ignore
window.Loaded.AddHandler(new RoutedEventHandler(WindowLoaded))
window.Show()

window.Unloaded.AddHandler(new RoutedEventHandler(WindowUnloaded))

[<STAThread>]
do
    let app = new Application() in
    app.Run(window) |> ignore