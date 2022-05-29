namespace TranscripterUI.ViewModels

open System.ComponentModel
open ReactiveUI
open System.Collections.ObjectModel
open FSharp.Collections.ParallelSeq
open TranscripterLib
open TranscripterUI.ViewModels

type ProcessFileState =
    | NotStarted
    | Working
    | Done
    | Failed
    
    member this.StringRep =
        match this with
        | NotStarted -> "Not started"
        | Working -> "Currently processing"
        | Done -> "Done"
        | Failed -> "Failed"

type ProcessFile(inputFile: string, outputFile: string) =
    let mutable status = NotStarted
    let event = Event<_,_>()
    
    member val In = inputFile
    member val Out = outputFile with get, set
    member this.Status
        with get() = status
        and set(newVal) =
            status <- newVal
            event.Trigger(this, PropertyChangedEventArgs("Status"))
            
    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member this.PropertyChanged = event.Publish
    
type ProcessingViewModel() =
    inherit ViewModelBase()

    static member Log =
        NLog.LogManager.GetCurrentClassLogger()

    member val ProcessingFiles = ObservableCollection([]) with get, set

    member this.SetFiles(fileListConfig: List<FileListEntry>) =
        this.ProcessingFiles.Clear()
        fileListConfig |> Seq.iter (fun f -> this.ProcessingFiles.Add(ProcessFile(f.In, f.Out)))
        
    member this.NumFilesDone() =
        (0, this.ProcessingFiles)
        ||> Seq.fold (fun acc file ->
            match file.Status with
            | NotStarted
            | Working -> acc + 0
            | Done
            | Failed -> acc + 1)

    member this.ProcessFiles() =
        match Transcripter.NewClient(true, None, None) with
        | Ok client ->
            let totalFileWatch =
                System.Diagnostics.Stopwatch.StartNew()

            this.ProcessingFiles
            |> PSeq.withDegreeOfParallelism 1
            |> PSeq.map (fun file ->
                let perFileWatch =
                    System.Diagnostics.Stopwatch.StartNew()

                file.Status <- ProcessFileState.Working
                ProcessingViewModel.Log.Debug($"transcribing {file.In}")

                match Transcripter.Transcribe(client, file.In) with
                | Ok result ->
                    ProcessingViewModel.Log.Debug $"ok: {result}"
                    file.Status <- ProcessFileState.Done
                | Error err ->
                    ProcessingViewModel.Log.Debug $"err: {err}"
                    file.Status <- ProcessFileState.Failed

                perFileWatch.Stop()
                ProcessingViewModel.Log.Debug($"time elapsed: {perFileWatch.ElapsedMilliseconds / 1000L} seconds"))
            |> PSeq.toArray
            |> ignore

            totalFileWatch.Stop()
        | Error err ->
            ProcessingViewModel.Log.Debug($"err creating client: {err}")
