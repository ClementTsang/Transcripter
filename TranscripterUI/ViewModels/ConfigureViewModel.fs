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
        "Using built-in model: English v1.0.0 (Huge Vocab)"

    [<Literal>]
    let defaultScorePathString =
        "Using built-in scorer: English v1.0.0 (Huge Vocab)"

    let mutable numCPUs = 1
    let mutable lineSplittingIndex = 0

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
    member val MaxLineLength = 100 with get, set
    member val NumCandidates = 1u with get, set

    member val ShowOpenFileDialog = Interaction<OpenFileDialog, List<string>>()
    member val ModelPath: Option<string> = None with get, set
    member val ScorerPath: Option<string> = None with get, set
    member val ModelPathString = defaultModelPathString with get, set
    member val ScorerPathString = defaultScorePathString with get, set

    static member val LineSplittingOptions =
        [ LineControlType.WordLength
          LineControlType.CharLength
          LineControlType.WordAndCharLength ]

    member val WordLengthDisplay = (true, 1.0) with get, set
    member val LineLengthDisplay = (false, 0.5) with get, set

    member this.LineSplittingIndex
        with get () = lineSplittingIndex
        and set newVal =
            if not (newVal = lineSplittingIndex) then
                lineSplittingIndex <- newVal

                match ConfigureViewModel.LineSplittingOptions[lineSplittingIndex] with
                | LineControlType.WordLength ->
                    this.WordLengthDisplay <- (true, 1.0)
                    this.LineLengthDisplay <- (false, 0.5)
                | LineControlType.CharLength ->
                    this.WordLengthDisplay <- (false, 0.5)
                    this.LineLengthDisplay <- (true, 1.0)
                | LineControlType.WordAndCharLength ->
                    this.WordLengthDisplay <- (true, 1.0)
                    this.LineLengthDisplay <- (true, 1.0)

                this.RaisePropertyChanged("LineSplittingIndex")
                this.RaisePropertyChanged("LineLengthDisplay")
                this.RaisePropertyChanged("WordLengthDisplay")


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

                let tfliteFilter = FileDialogFilter()
                tfliteFilter.Name <- "TensorFlow Lite Model"
                tfliteFilter.Extensions.Add("tflite")

                dialog.AllowMultiple <- false
                dialog.Filters.Add(tfliteFilter)
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

    member private this.SelectScorerAsync =
        fun () ->
            Task.Factory.StartNew (fun () ->
                let dialog = OpenFileDialog()

                let allFilter = FileDialogFilter()
                allFilter.Name <- "All Files"
                allFilter.Extensions.Add("*")

                let scorerFilter = FileDialogFilter()
                scorerFilter.Name <- "Scorer"
                scorerFilter.Extensions.Add("scorer")

                dialog.AllowMultiple <- false
                dialog.Filters.Add(scorerFilter)
                dialog.Filters.Add(allFilter)
                dialog.Title <- "Select files to transcribe"

                let callback =
                    fun (files: List<string>) ->
                        if files.IsEmpty then
                            this.ScorerPath <- None
                        else
                            this.ScorerPath <- Some(files[0])

                        this.ScorerPathString <-
                            match this.ScorerPath with
                            | None -> defaultScorePathString
                            | Some p -> $"Selected scorer: {p}"

                        this.RaisePropertyChanged("ScorerPathString")

                this.OpenFileDialog(dialog, callback))

    member this.SelectModel =
        ReactiveCommand.CreateFromTask(this.SelectModelAsync)

    member this.SelectScorer =
        ReactiveCommand.CreateFromTask(this.SelectScorerAsync)
