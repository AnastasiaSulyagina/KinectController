module Connection

open System
open Microsoft.Kinect
open System.Net.Sockets
open WpfApplication1
open SkeletonProcessing

let mutable port = window.portBox.Text
let mutable ipAddr = window.IPBox.Text
let mutable sender = null 

let startApp ()=
    async {
        
        sensor.ColorStream.Enable()
        sensor.SkeletonStream.Enable()
        sensor.Start()
    }
    |> Async.RunSynchronously
    window.modeChooser.IsEnabled <- true

let setConnection args = 
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
    if sender <> null then
        startApp () |> ignore
