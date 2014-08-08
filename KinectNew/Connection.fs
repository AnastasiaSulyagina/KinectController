module Connection

open System
open Microsoft.Kinect
open System.Net.Sockets
open WpfApplication1
open SkeletonProcessing

open Graphics

let mutable port = window.portBox.Text
let mutable ipAddr = window.IPBox.Text
let mutable sender = null 


let tryAccessKinect() = try 
                            KinectSensor.KinectSensors.[0]
                        with :? System.ArgumentOutOfRangeException as e -> 
                           printfn "Kinect not found. Check connection and restart"
                           exit 0
                           null



let startKinectApp (sensor: KinectSensor) =
    async {
        
        sensor.ColorStream.Enable()
        sensor.SkeletonStream.Enable()
        sensor.Start()
    }
    |> Async.RunSynchronously
    window.modeChooser.IsEnabled <- true


let SendRequest controller (l,r) =
    sendInstructionMessage ""
    //printfn "%A %A %A\n" l r (mode = GameMode.Flapping)
    let req = [| byte l; byte r; 48uy; 48uy|]
    if sender <> null then
        printfn "values %d %d controller %d" l r controller
        //sender.GetStream().Write(req, 0, req.Length)
    else 
        sendInstructionMessage "No connection with trik. Check IP, Port and restart"
   
let setTrikConnection() = 
    port <- window.portBox.Text
    ipAddr <- window.IPBox.Text
    sendInstructionMessage "Establishing connection. Please wait ..."
    sender <- new Object()(*try
                runOnThisThread window.instructionMessage <| fun() -> 
                    window.instructionMessage.Content <- "Trik connected. Choose the mode and start playing."
                new TcpClient(ipAddr, Convert.ToInt32(port))
                with :? SocketException as e ->
                runOnThisThread window.instructionMessage <| fun() -> 
                    window.instructionMessage.Content <- "No connection with trik. Check IP, Port and restart"
                    window.conectionButton.IsChecked <- new Nullable<bool>(false)
                null*)
    sender <> null
