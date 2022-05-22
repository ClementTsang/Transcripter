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
        with get(): ViewModelBase = csv.GetCurrentStep.StepViewModel
        and set newVM = ignore <| this.RaiseAndSetIfChanged(ref currentVM, newVM)

    member val CurrentStepTracking = csv with get, set

    member val ShowOpenFileDialog = Interaction<Unit, List<string>>()
    member val CurrentlySelectedFiles = List.Empty with get, set
    
    
    member private this.SelectFilesAsync =
        fun () ->
            Task.Factory.StartNew(fun () ->
                this.ShowOpenFileDialog.Handle(()).Subscribe(
                    fun files ->
                        printfn($"selected files: {files}")
                        this.CurrentlySelectedFiles <- files
                        if not this.CurrentlySelectedFiles.IsEmpty then
                            this.CurrentStepTracking.NextStep
                            this.CurrentVM <- this.CurrentStepTracking.GetCurrentStep.StepViewModel
                ) |> ignore
            )
        
    member this.SelectFiles = ReactiveCommand.CreateFromTask(this.SelectFilesAsync)
    
    member private this.TranscribeTask =
        fun() ->
            Task.Factory.StartNew(fun () ->
                match Transcripter.NewClient(true) with
                | Ok(client) -> 
                    printfn($"Files: {this.CurrentlySelectedFiles}")
                    this.CurrentlySelectedFiles
                    |> Seq.map (fun file ->
                            let stopWatch = System.Diagnostics.Stopwatch.StartNew()
                            printfn($"transcribing {file}")
                            match Transcripter.Transcribe(client, file) with
                            | Ok(result) -> printfn($"ok: {result}")
                            | Error(err) -> printfn($"err: {err}")
                            stopWatch.Stop()
                            printfn($"time elapsed: {stopWatch.ElapsedMilliseconds / 1000L}")
                        )
                    |> Seq.toArray
                    |> ignore
                | Error(err) -> printfn($"err creating client: {err}")
            )
        
    member this.Transcribe = ReactiveCommand.CreateFromTask(this.TranscribeTask)
    