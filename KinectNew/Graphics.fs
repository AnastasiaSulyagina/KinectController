module Graphics

open System
open System.Windows
open Microsoft.Kinect
open System.Windows.Media.Imaging
open WpfApplication1
open SkeletonProcessing

let pixelData : byte array = Array.zeroCreate sensor.ColorStream.FramePixelDataLength 

let ColorFrameReady (args: ColorImageFrameReadyEventArgs) =      
    use frame = args.OpenColorImageFrame()          
    match frame with
        | null -> ()
        | videoFrame -> 
                videoFrame.CopyPixelDataTo(pixelData)
                runOnThisThread window.image <| fun() -> 
                    window.image.Source <- BitmapSource.Create(640, 480, 96.0, 96.0, Media.PixelFormats.Bgr32, null, pixelData, 640 * 4)

let WindowLoaded (sender : obj) (args: EventArgs) = 
    runOnThisThread window.instructionMessage <| fun() -> 
        window.instructionMessage.Content <- "Enter trik IP and Port. Then press the connection button to start"
 
let WindowUnloaded (sender : obj) (args: EventArgs) = 
    sensor.Stop()


