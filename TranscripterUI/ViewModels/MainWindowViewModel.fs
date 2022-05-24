namespace TranscripterUI.ViewModels

open System.Runtime.Serialization
open System.Threading.Tasks
open ReactiveUI
open TranscripterLib
open TranscripterUI.ViewModels

[<DataContract>]
type MainWindowViewModel() =
    inherit ViewModelBase()

    let csv = CurrentStepViewModel()
    let currentVM = ViewModelBase()

    member this.CurrentVM
        with get (): ViewModelBase = csv.GetCurrentStep.StepViewModel
        and set newVM =
            ignore
            <| this.RaiseAndSetIfChanged(ref currentVM, newVM)

    static member Log =
        NLog.LogManager.GetCurrentClassLogger()

    member val ShowSteps = false with get, set
    member val CurrentStepTracking = csv with get, set
    member val ShowOpenFileDialog = Interaction<Unit, List<string>>()
    member val CurrentlySelectedFiles = List.Empty with get, set

    member private this.SelectFilesAsync =
        fun () ->
            Task.Factory.StartNew (fun () ->
                this
                    .ShowOpenFileDialog
                    .Handle(())
                    .Subscribe(fun files ->
                        MainWindowViewModel.Log.Debug($"selected files: {files}")
                        this.CurrentlySelectedFiles <- files

                        if not this.CurrentlySelectedFiles.IsEmpty then
                            this.NextStep
                        //                                (this.CurrentVM :?> FileListViewModel).SetFileList(this.CurrentlySelectedFiles)
                        )
                |> ignore)

    member this.SelectFilesCommand =
        ReactiveCommand.CreateFromTask(this.SelectFilesAsync)

    member this.SetStepCommand(stepIndex: string) =
        this.CurrentStepTracking.SetStepCommand(stepIndex)
        this.CurrentVM <- this.CurrentStepTracking.GetCurrentStep.StepViewModel

    member private this.NextStep =
        this.CurrentStepTracking.NextStep
        this.CurrentVM <- this.CurrentStepTracking.GetCurrentStep.StepViewModel

    member private this.TranscribeTask =
        fun () ->
            Task.Factory.StartNew (fun () ->
                this.CurrentStepTracking.SetStepEnabled(false)
                this.NextStep

                match Transcripter.NewClient(true) with
                | Ok client ->
                    MainWindowViewModel.Log.Debug($"Files: {this.CurrentlySelectedFiles}")

                    this.CurrentlySelectedFiles
                    |> Seq.map (fun file ->
                        let stopWatch =
                            System.Diagnostics.Stopwatch.StartNew()

                        MainWindowViewModel.Log.Debug($"transcribing {file}")

                        match Transcripter.Transcribe(client, file) with
                        | Ok result -> MainWindowViewModel.Log.Debug $"ok: {result}"
                        | Error err -> MainWindowViewModel.Log.Debug $"err: {err}"

                        stopWatch.Stop()
                        MainWindowViewModel.Log.Debug($"time elapsed: {stopWatch.ElapsedMilliseconds / 1000L} seconds"))
                    |> Seq.toArray
                    |> ignore
                | Error err -> MainWindowViewModel.Log.Debug($"err creating client: {err}")

                this.CurrentStepTracking.SetStepEnabled(true))

    member this.Transcribe =
        ReactiveCommand.CreateFromTask(this.TranscribeTask)
