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

type ProcessingConfig(config: ConfigureViewModel) =
    member val NumCPUs = config.NumCPUs with get, set
    member val MaxWordLength = config.MaxWordLength with get, set
    member val MaxLineLength = config.MaxLineLength with get, set
    member val NumCandidates = config.NumCandidates with get, set
    member val ModelPath = config.ModelPath with get, set
    member val ScorerPath = config.ScorerPath with get, set

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

type ProcessingViewModel(config: ConfigureViewModel) =
    inherit ViewModelBase()

    static member Log =
        NLog.LogManager.GetCurrentClassLogger()

    member val ProcessingFiles = ObservableCollection([]) with get, set
    member val Config = ProcessingConfig(config) with get
    member val Finished = false with get, set
    member val TotalTimeTaken = "" with get, set

    member this.SetFiles(fileListConfig: List<FileListEntry>) =
        this.ProcessingFiles.Clear()

        fileListConfig
        |> Seq.iter (fun f -> this.ProcessingFiles.Add(ProcessFile(f.In, f.Out)))

    member this.NumFilesDone() =
        (0, this.ProcessingFiles)
        ||> Seq.fold (fun acc file ->
            match file.Status with
            | NotStarted
            | Working -> acc + 0
            | Done _
            | Failed _ -> acc + 1)

    member this.ProcessFiles() =
        // Note that STT clients cannot be "shared" across a concurrent "job" - each job needs its own dedicated
        // client, otherwise you are guaranteed to get a nasty crash when running jobs in parallel.
        //
        // Therefore, we pre-allocate multiple clients and dump them it into a list, alongside an index list. The index
        // list controls which client is accessed, and we control this across our parallel thread accesses.
        //
        // Note that accesses to this index list should _probably_ be protected by a mutex, since it's parallel...
        let mutex = new Mutex()

        let numClients =
            min this.Config.NumCPUs this.ProcessingFiles.Count

        let clients =
            [ for _ in 1..numClients -> Transcripter.NewClient(true, this.Config.ModelPath, this.Config.ScorerPath) ]
            |> Seq.choose (fun client ->
                match client with
                | Ok client ->
                    ProcessingViewModel.Log.Debug($"Allocated a new client.")
                    Some(client)
                | Error err ->
                    ProcessingViewModel.Log.Debug($"err creating client: {err}")
                    None)
            |> Seq.toList

        let clientIndices =
            [ for i in 0 .. numClients - 1 -> i ]
            |> ResizeArray

        assert (clientIndices.Count = clients.Length)

        let totalFileWatch =
            System.Diagnostics.Stopwatch.StartNew()

        if not clients.IsEmpty then
            this.ProcessingFiles
            |> PSeq.withDegreeOfParallelism clients.Length
            |> PSeq.map (fun file ->
                mutex.WaitOne() |> ignore
                let clientIndex = clientIndices[0]
                clientIndices.RemoveAt(0)
                mutex.ReleaseMutex()

                let client = clients[clientIndex]

                file.Status <- ProcessFileState.Working
                ProcessingViewModel.Log.Debug($"transcribing {file.In}")

                let perFileWatch =
                    System.Diagnostics.Stopwatch.StartNew()

                let transcription =
                    client.Transcribe(file.In, this.Config.NumCandidates)

                perFileWatch.Stop()

                let time =
                    (float perFileWatch.ElapsedMilliseconds) / 1000.0

                ProcessingViewModel.Log.Debug $"Transcription task took %1.2f{time}s"

                match transcription with
                | Ok result ->
                    ProcessingViewModel.Log.Debug $"{file.In} task - ok"
                    file.Status <- ProcessFileState.Done $"(%1.2f{time}s)"
                | Error err ->
                    ProcessingViewModel.Log.Debug $"{file.In} task - err: {err}"
                    file.Status <- ProcessFileState.Failed $"{err} (%1.2f{time}s)"

                mutex.WaitOne() |> ignore
                clientIndices.Add(clientIndex)
                mutex.ReleaseMutex())
            |> PSeq.toArray
            |> ignore
        else
            // TODO: Handle the case all clients fail.
            ()

        totalFileWatch.Stop()
        this.Finished <- true

        this.TotalTimeTaken <-
            $"Transcription completed in %1.2f{(float totalFileWatch.ElapsedMilliseconds)
                                               / 1000.0}s"

        this.RaisePropertyChanged("Finished")
        this.RaisePropertyChanged("TotalTimeTaken")
        ProcessingViewModel.Log.Debug this.TotalTimeTaken
