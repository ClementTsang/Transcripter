namespace TranscripterUI.ViewModels

open System.IO
open System.Runtime.Serialization
open System.Threading.Tasks
open FSharp.Collections.ParallelSeq
open ReactiveUI
open TranscripterLib
open TranscripterUI.Models
open TranscripterUI.ViewModels

[<DataContract>]
type MainWindowViewModel() =
    inherit ViewModelBase()

    let currentStepVM = CurrentStepViewModel()

    // This is an ugly hack. Sorry.
    let _selectFilesVM =
        currentStepVM.Steps[0].StepViewModel :?> SelectFilesViewModel

    let configureVM =
        currentStepVM.Steps[1].StepViewModel :?> ConfigureViewModel

    let fileListVM =
        currentStepVM.Steps[2].StepViewModel :?> FileListViewModel

    let mutable currentVM =
        currentStepVM.GetCurrentStep().StepViewModel

    member this.CurrentVM
        with get (): ViewModelBase = currentVM
        and set newVM =
            currentVM <- newVM
            this.RaisePropertyChanged("CurrentVM")

    static member Log =
        NLog.LogManager.GetCurrentClassLogger()

    member val ShowSteps = false with get, set
    member val CurrentStepTracking = currentStepVM with get, set
    member val ShowOpenFileDialog = Interaction<Unit, List<string>>()
    member val CurrentlySelectedFiles = List.Empty with get, set
    member val FileListVM = fileListVM

    member private this.SelectFilesAsync =
        fun () ->
            Task.Factory.StartNew (fun () ->
                this
                    .ShowOpenFileDialog
                    .Handle(())
                    .Subscribe(fun files ->
                        MainWindowViewModel.Log.Debug($"selected files: {files}")

                        if not files.IsEmpty then
                            this.CheckAndThenAddFiles files)
                |> ignore)

    member this.SelectFilesCommand =
        ReactiveCommand.CreateFromTask(this.SelectFilesAsync)

    member this.CheckAndThenAddFiles files =
        let fileConfigList =
            files |> PSeq.map InputFile |> Seq.toList

        let invalidList =
            fileConfigList
            |> Seq.filter (fun file -> not file.IsValid)
            |> Seq.map (fun file -> file.InputFile)
            |> Seq.toList

        if invalidList.IsEmpty then
            this.CurrentlySelectedFiles <- fileConfigList
            this.NextStep()
        else
            let vm = InvalidFilesViewModel()

            vm.InvalidFiles <-
                invalidList
                |> Seq.map (fun file -> InvalidFile(file, "Missing audio stream"))
                |> Seq.toList // Hardcoded for now

            this.CurrentVM <- vm

    member this.ReselectFiles() = this.SetStepCommand("0")

    member this.SetStepCommand(stepIndex: string) =
        this.CurrentStepTracking.SetStepCommand(stepIndex)
        this.CurrentVM <- this.CurrentStepTracking.GetCurrentStep().StepViewModel

    member private this.NextStep() =
        this.CurrentStepTracking.NextStep()
        this.CurrentVM <- this.CurrentStepTracking.GetCurrentStep().StepViewModel

    member private this.ToFileListReview() =
        fileListVM.FileListConfiguration.Clear()
        
        this.CurrentlySelectedFiles
        |> Seq.indexed
        |> Seq.iter (fun (index, file) ->
            fileListVM.FileListConfiguration.Add(
                FileListEntry(
                    file.InputFile,
                    Path.ChangeExtension(
                        file.InputFile,
                        ConfigureViewModel.SupportedTypes[configureVM.OutputSelectedIndex]
                            .FileFormat
                    ),
                    index
                )
            ))

        this.NextStep()

    member private this.TranscribeTask =
        fun () ->
            Task.Factory.StartNew (fun () ->
                this.CurrentStepTracking.SetStepEnabled(false)
                this.NextStep()

                match Transcripter.NewClient(true, None, None) with
                | Ok client ->
                    fileListVM.FileListConfiguration
                    |> PSeq.withDegreeOfParallelism 1
                    |> PSeq.map (fun file ->
                        let stopWatch =
                            System.Diagnostics.Stopwatch.StartNew()

                        MainWindowViewModel.Log.Debug($"transcribing {file.In}")

                        match Transcripter.Transcribe(client, file.In) with
                        | Ok result -> MainWindowViewModel.Log.Debug $"ok: {result}"
                        | Error err -> MainWindowViewModel.Log.Debug $"err: {err}"

                        stopWatch.Stop()
                        MainWindowViewModel.Log.Debug($"time elapsed: {stopWatch.ElapsedMilliseconds / 1000L} seconds"))
                    |> PSeq.toArray
                    |> ignore
                | Error err -> MainWindowViewModel.Log.Debug($"err creating client: {err}")

                this.CurrentStepTracking.SetStepEnabled(true))

    member this.Transcribe =
        ReactiveCommand.CreateFromTask(this.TranscribeTask)
