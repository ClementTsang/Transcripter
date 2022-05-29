namespace TranscripterUI.ViewModels

open ReactiveUI
open System.ComponentModel
open System.Collections.ObjectModel
open System.Threading
open FSharp.Collections.ParallelSeq
open TranscripterLib
open TranscripterUI.ViewModels

type ProcessFileState =
    | NotStarted
    | Working
    | Done of string
    | Failed of string

    member this.StringRep =
        match this with
        | NotStarted -> "Not started"
        | Working -> "Currently processing"
        | Done s -> $"Done {s}"
        | Failed s -> $"Failed: {s}"

type ProcessingConfig(?numCPUs: int, ?maxWordLength: int, ?maxLineLength: int) =
    member val NumCPUs =
        match numCPUs with
        | Some numCPUs -> numCPUs
        | None -> 0 with get, set

    member val MaxWordLength =
        match maxWordLength with
        | Some maxWordLength -> maxWordLength
        | None -> 0 with get, set

    member val MaxLineLength =
        match maxLineLength with
        | Some maxLineLength -> maxLineLength
        | None -> 0 with get, set

type ProcessFile(inputFile: string, outputFile: string) =
    let mutable status = NotStarted
    let event = Event<_, _>()

    member val In = inputFile
    member val Out = outputFile with get, set

    member this.Status
        with get () = status
        and set newVal =
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
    member val Config = ProcessingConfig() with get, set
    member val Finished = false with get, set
    member val TotalTimeTaken = "" with get, set
    
    member this.SetFiles(fileListConfig: List<FileListEntry>) =
        this.ProcessingFiles.Clear()

        fileListConfig
        |> Seq.iter (fun f -> this.ProcessingFiles.Add(ProcessFile(f.In, f.Out)))

    member this.SetConfig(configureVM: ConfigureViewModel) =
        this.Config.MaxLineLength <- configureVM.MaxLineLength
        this.Config.MaxWordLength <- configureVM.MaxWordLength
        this.Config.NumCPUs <- configureVM.NumCPUs

    member this.NumFilesDone() =
        (0, this.ProcessingFiles)
        ||> Seq.fold (fun acc file ->
            match file.Status with
            | NotStarted
            | Working -> acc + 0
            | Done _
            | Failed _ -> acc + 1)

    member this.ProcessFiles() =
        let totalFileWatch =
            System.Diagnostics.Stopwatch.StartNew()

        // We pre-allocate the clients and dump it into a list. Note that accesses to this should _probably_ be
        // protected by a mutex.
        let numClients = min this.Config.NumCPUs this.ProcessingFiles.Count
        let clients =
            [ for _ in 1 .. numClients -> Transcripter.NewClient(true, None, None) ]
            |> Seq.choose (fun client ->
                match client with
                | Ok client ->
                    ProcessingViewModel.Log.Debug($"Allocated a new client.")
                    Some(client)
                | Error err ->
                    ProcessingViewModel.Log.Debug($"err creating client: {err}")
                    None)
            |> ResizeArray
        let mutex = new Mutex()

        if clients.Count > 0 then
            this.ProcessingFiles
            |> PSeq.withDegreeOfParallelism clients.Count
            |> PSeq.map (fun file ->
                mutex.WaitOne() |> ignore
                let client = clients[0]
                clients.RemoveAt(0)
                mutex.ReleaseMutex()
                
                let perFileWatch =
                    System.Diagnostics.Stopwatch.StartNew()

                file.Status <- ProcessFileState.Working
                ProcessingViewModel.Log.Debug($"transcribing {file.In}")

                let transcription = Transcripter.Transcribe(client, file.In)
                perFileWatch.Stop()
                let time = (float perFileWatch.ElapsedMilliseconds) / 1000.0
                ProcessingViewModel.Log.Debug $"Transcription task took %1.2f{time}s"
                
                match transcription with
                | Ok result ->
                    ProcessingViewModel.Log.Debug $"{file.In} task - ok"
                    file.Status <- ProcessFileState.Done $"(%1.2f{time}s)"
                | Error err ->
                    ProcessingViewModel.Log.Debug $"{file.In} task - err: {err}"
                    file.Status <- ProcessFileState.Failed $"{err} (%1.2f{time}s)"

                mutex.WaitOne() |> ignore
                clients.Add(client)
                mutex.ReleaseMutex()
            )
            |> PSeq.toArray
            |> ignore
        else
            // TODO: Handle the case all clients fail.
            ()

        totalFileWatch.Stop()
        this.Finished <- true
        this.TotalTimeTaken <- $"Transcription completed in %1.2f{(float totalFileWatch.ElapsedMilliseconds) / 1000.0}s"
        this.RaisePropertyChanged("Finished")
        this.RaisePropertyChanged("TotalTimeTaken")
        ProcessingViewModel.Log.Debug this.TotalTimeTaken
