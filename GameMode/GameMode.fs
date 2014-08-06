namespace GameMode
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

type MVVM() =
    let props = ["Mode"; "Name"; "Description"]
    let ev = new Event<_,_>()  
    let mutable mode = GameMode.Flapping
    
    member self.ModeList = 
        [|FullMode(GameMode.Flapping, "Flapping mode")
          FullMode(GameMode.Floating, "Floating mode")|]

    member self.Mode 
        with get() = mode 
        and set v = 
            if v <> mode then 
                mode <- v
                props |> List.iter(fun x -> ev.Trigger(self, new PropertyChangedEventArgs(x)))

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member self.PropertyChanged = ev.Publish
   