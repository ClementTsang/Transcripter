namespace TranscripterUI.ViewModels

open System.Threading.Tasks
open Avalonia.Controls
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

type LineControlType =
    | WordLength
    | CharLength
    | WordAndCharLength

    member this.Display =
        match this with
        | WordLength -> "Number of words"
        | CharLength -> "Number of characters"
        | WordAndCharLength -> "Both words and characters"

[<DataContract>]
type ConfigureViewModel() =
    inherit ViewModelBase()

    [<Literal>]
    let defaultModelPathString =
        "Using default model: ggml-base.bin"

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
    static member val YesNoComboOptions = [ "Yes"; "No" ]
    member val OverwriteSelectedIndex = 1 with get, set

    member val ShowOpenFileDialog = Interaction<OpenFileDialog, List<string>>()
    member val ModelPath: Option<string> = None with get, set
    member val ScorerPath: Option<string> = None with get, set
    member val ModelPathString = defaultModelPathString with get, set

    member private this.OpenFileDialog(dialog: OpenFileDialog, callback: List<string> -> unit) =
        this
            .ShowOpenFileDialog
            .Handle(dialog)
            .Subscribe(callback)
        |> ignore

    member private this.SelectModelAsync =
        fun () ->
            Task.Factory.StartNew (fun () ->
                let dialog = OpenFileDialog()

                let allFilter = FileDialogFilter()
                allFilter.Name <- "All Files"
                allFilter.Extensions.Add("*")

                let ggmlFilter = FileDialogFilter()
                ggmlFilter.Name <- "GGML Tensor File"
                ggmlFilter.Extensions.Add("ggml")

                dialog.AllowMultiple <- false
                dialog.Filters.Add(ggmlFilter)
                dialog.Filters.Add(allFilter)
                dialog.Title <- "Select files to transcribe"

                let callback =
                    fun (files: List<string>) ->
                        if files.IsEmpty then
                            this.ModelPath <- None
                        else
                            this.ModelPath <- Some(files[0])

                        this.ModelPathString <-
                            match this.ModelPath with
                            | None -> defaultModelPathString
                            | Some p -> $"Selected model: {p}"

                        this.RaisePropertyChanged("ModelPathString")

                this.OpenFileDialog(dialog, callback))

    member this.SelectModel =
        ReactiveCommand.CreateFromTask(this.SelectModelAsync)
