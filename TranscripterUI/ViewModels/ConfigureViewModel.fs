namespace TranscripterUI.ViewModels

open ReactiveUI
open System
open System.Runtime.Serialization
open TranscripterUI.ViewModels

type FileType =
    | SRT
    | VTT

    member this.FileFormat =
        match this with
        | SRT -> "srt"
        | VTT -> "vtt"

[<DataContract>]
type ConfigureViewModel() =
    inherit ViewModelBase()

    let mutable numCPUs = 1

    member this.NumCPUs
        with get (): int = numCPUs
        and set newVal =
            if numCPUs <> newVal
               && newVal >= 0
               && newVal <= this.MaxCPUs then
                numCPUs <- newVal
                this.RaisePropertyChanged("NumCPUs")

    member val MaxCPUs = Environment.ProcessorCount

    static member val SupportedTypes = [ FileType.SRT; FileType.VTT ]

    static member SupportedTypeStrings =
        ConfigureViewModel.SupportedTypes
        |> Seq.map (fun f -> f.ToString())
        |> Seq.toList

    member val OutputSelectedIndex = 0 with get, set

    member val OverwriteComboOptions = [ "Yes"; "No" ]
    member val OverwriteSelectedIndex = 1 with get, set

    member val MaxWordLength = 10 with get, set

    member val MaxLineLength = 10000 with get, set
