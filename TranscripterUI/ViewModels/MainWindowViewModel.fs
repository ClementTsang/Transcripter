namespace TranscripterUI.ViewModels

open System.IO
open System.Runtime.Serialization
open System.Threading.Tasks
open FSharp.Collections.ParallelSeq
open ReactiveUI
open TranscripterUI.Models
open TranscripterUI.ViewModels

[<DataContract>]
type MainWindowViewModel() =
    inherit ViewModelBase()

    let currentStepVM = CurrentStepViewModel()

    let mutable currentVM =
        currentStepVM.GetCurrentStep().StepViewModel

    member val ConfigureVM = currentStepVM.Steps[1].StepViewModel :?> ConfigureViewModel

    member val FileListVM = currentStepVM.Steps[2].StepViewModel :?> FileListViewModel

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

        this.CurrentVM <-
            this
                .CurrentStepTracking
                .GetCurrentStep()
                .StepViewModel

    member private this.NextStep() =
        this.CurrentStepTracking.NextStep()
        this.CurrentVM <-
            this
                .CurrentStepTracking
                .GetCurrentStep()
                .StepViewModel

    member private this.ToFileListReview() =
        this.FileListVM.FileListConfiguration.Clear()

        this.CurrentlySelectedFiles
        |> Seq.indexed
        |> Seq.iter (fun (index, file) ->
            this.FileListVM.FileListConfiguration.Add(
                FileListEntry(
                    file.InputFile,
                    Path.ChangeExtension(
                        file.InputFile,
                        ConfigureViewModel.SupportedTypes[this.ConfigureVM.OutputSelectedIndex]
                            .FileFormat
                    ),
                    index
                )
            ))

        this.NextStep()

    member private this.TranscribeTask =
        fun () ->
            Task.Factory.StartNew (fun () ->
                this.CurrentStepTracking.SetStepsEnabled(false)
                let pvm = ProcessingViewModel()

                pvm.SetFiles(
                    this.FileListVM.FileListConfiguration
                    |> Seq.toList
                )

                pvm.SetConfig(this.ConfigureVM)
                this.CurrentVM <- pvm

                pvm.ProcessFiles()
            )

    member this.Transcribe =
        ReactiveCommand.CreateFromTask(this.TranscribeTask)

    member this.StartAgain() =
        this.CurrentStepTracking.SetStepsEnabled(true)
        this.CurrentStepTracking.SetStep(0)
        this.CurrentVM <-
            this
                .CurrentStepTracking
                .GetCurrentStep()
                .StepViewModel