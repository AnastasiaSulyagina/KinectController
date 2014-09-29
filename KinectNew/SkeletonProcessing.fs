module SkeletonProcessing

open System
open System.Windows
open Microsoft.Kinect
open System.Diagnostics
open System.Reactive.Linq
open System.Windows.Media.Imaging
open WpfApplication1


type private Notifier<'T>() =
    let observers = new ResizeArray<IObserver<'T> >()
    let source = { new IObservable<'T> with
        member self.Subscribe observer =  
            lock observers <| fun () -> observers.Add(observer)
            { new IDisposable with 
                member this.Dispose() = lock observers <| fun () -> observers.Remove(observer) |> ignore }
              }

    member self.OnError e = 
            lock (observers)
            <| fun () -> observers.ForEach(fun obs -> obs.OnError e)
            observers.Clear()

    member self.OnNext (x: 'T) = 
        try 
            lock (observers)
            <| fun () -> observers.ForEach(fun obs -> obs.OnNext x) 
        with e -> self.OnError e

    member self.OnCompleted _ = 
            lock (observers)
            <| fun () -> observers.ForEach(fun obs -> obs.OnCompleted()) 
            observers.Clear()
    member self.Publish with get() = source

    interface IDisposable with
        member self.Dispose() = self.OnCompleted()


let TakeDerivative(source: IObservable<(int*int)*(int*int)>) = 
    let previousKey = ref (0, 0)
    let notifier = new Notifier<int*int>()

    let observer = { new System.IObserver<_> with
                        member self.OnNext((_,x)) = 
                            try 
                                let l,r = x
                                let pL, pR = !previousKey
                                //printfn "prev = %A, new = %A, diff %A" !previousKey x (l - pL, r - pR)
                                previousKey := x 
                                notifier.OnNext (l - pL, r - pR)

                            with e -> notifier.OnError e

                        member self.OnError e = notifier.OnError e
                        member self.OnCompleted() = notifier.OnCompleted()
                    }
    source.Subscribe(observer) |> ignore
    notifier.Publish




///From MAIN


let mutable minHeight = 20.
let mutable maxHeight = 400.
let eps = 10

//let skeletons = Array.zeroCreate 6



let ToPoint(s,c) = 
    CoordinateMapper(KinectSensor.KinectSensors.[0]).MapSkeletonPointToColorPoint(s, c)


let inline isJointInferred (joint: Joint) = joint.TrackingState = JointTrackingState.Inferred
let inline isSkeletonTracked (skeleton : Skeleton) = skeleton.TrackingState = SkeletonTrackingState.Tracked

let getPoints (skeleton: Skeleton) =
    let l = skeleton.Joints.[JointType.HandLeft]
    let r = skeleton.Joints.[JointType.HandRight]
    if (isJointInferred l) && (isJointInferred r) 
    then None
    else
        let cf = ColorImageFormat.RgbResolution640x480Fps30
        let leftHand = ToPoint(l.Position, cf)
        let rightHand = ToPoint(r.Position, cf)
        let tmp = (leftHand.X, rightHand.X), (leftHand.Y, rightHand.Y)
        Some tmp
        

    (*
[<Struct>]
type Hands(l : int, r: int) =
    member self.Left = l
    member self.Right = r
    static member (+) (a1: Hands, a2: Hands) = Hands(a1.Left + a2.Left, a1.Right + a2.Right)
    static member (-) (a1: Hands, a2: Hands) = Hands(a1.Left - a1.Left, a1.Right - a2.Right)
    static member (/) (a1: Hands, a2) = Hands(a1.Left / a2, a1.Right / a2)
    static member Zero = Hands(0, 0)


let HandsAverage (source: System.Collections.Generic.IList<Hands>) = 
    let mutable sum = Hands.Zero
    for hand in source do
        sum <- sum + hand
    sum/source.Count

    *)



let TupleAverage (source: System.Collections.Generic.IList<int*int>) = 
    let mutable maxL = 0
    let mutable maxR = 0
    for (l, r) in source do
        maxL <- Math.Max(maxL, abs l)
        maxR <- Math.Max(maxR, abs r)
    (maxL, maxR)


(*
let calibrate (handPositions: Collections.Generic.IList<int>) =
    printfn "%A %A calibration\n" maxHeight minHeight


let floatingFiltering (l,r) =
    let lim x = 
        let x' = double x
        x' > minHeight && x' < maxHeight in (lim l) && (lim r)
        

let floatingScale (l,r) = 

    let helper x' =
        let x = float x'
        100. - ((x - minHeight) * 100. / (maxHeight - minHeight)) 
    helper l, helper r
    *)

let flappingScale (l,r) =
  //  printfn "%A" (l,r)
    let helper x = 
        if x < 2 * eps then 0
        elif x > 100 then 100
        else x
    helper <| abs (2 * l), helper <| abs (2 * r)

let Buffer (size:int) (skip: int) (x: IObservable<_>) = Observable.Buffer(x, size, skip)
let toFlappingHands (hands: IObservable<_>) =
    (TakeDerivative
    hands)
    |> Buffer 10 1
    |> Observable.map TupleAverage
    |> Observable.map flappingScale

(*let toFloatingHands hands = 
    Observable.Buffer(hands, 20, 2)
    |> Observable.map TupleAverage  
    |> Observable.filter floatingFiltering
    |> Observable.map floatingScale
*)