namespace TranscripterUI.ViewModels

open ReactiveUI
open System
open System.Runtime.Serialization
open TranscripterUI.ViewModels

[<DataContract>]
type ConfigureViewModel() =
    inherit ViewModelBase()
    
    let mutable numCPUs = 1
    member this.NumCPUs
        with get(): int = numCPUs
        and set v =
            if numCPUs <> v then
                numCPUs <- v
                this.RaisePropertyChanged("NumCPUs")
    member val MaxCPUs = Environment.ProcessorCount with get
    
    member val SupportedTypes =
        [
            "SRT";
            "VTT"
        ] with get

    member val OutputSelectedIndex = 0 with get, set
    
    
    member val OverwriteComboOptions = ["Yes"; "No"] with get
    member val OverwriteSelectedIndex = 1 with get, set
    