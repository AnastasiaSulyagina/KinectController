module GameMode
open System.ComponentModel 

[<RequireQualifiedAccess>]
type GameMode = Flapping | Floating
type FullMode(mode, name) =
    member self.Mode = mode
    member self.Name = name    
    member self.Description = 
        match mode with
        | GameMode.Flapping -> "Flap your hands quickly to move the robot"
        | GameMode.Floating -> "Put your hands higher and lower for speed regulating"

type MVVM() as self =
    let props = ["Mode"; "Name"; "Description"; "Detected"]
    let ev = new Event<_,_>()  
    let mutable mode = GameMode.Flapping
    let mutable trackedSkeletons: int[] = Array.empty
    
    let raisePropertyChanged() = props |> List.iter(fun x -> ev.Trigger(self, new PropertyChangedEventArgs(x)))

    member self.ModeList = 
        [|FullMode(GameMode.Flapping, "Flapping mode")
          FullMode(GameMode.Floating, "Floating mode")|]

    member self.Detected 
        with get() = trackedSkeletons |> Array.map string |> String.concat " "
    
    member self.TrackedSkeletons
        with get() = trackedSkeletons
        and set x = 
            let y = Array.sort x
            if trackedSkeletons <> y then
                trackedSkeletons <- y
                raisePropertyChanged()
                    
    member self.Mode 
        with get() = mode 
        and set v = 
            if v <> mode then 
                mode <- v
                raisePropertyChanged()

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member self.PropertyChanged = ev.Publish
   