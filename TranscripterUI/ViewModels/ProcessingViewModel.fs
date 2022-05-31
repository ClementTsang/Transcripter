namespace TranscripterUI.ViewModels

open ReactiveUI
open System.ComponentModel
open System.Collections.ObjectModel
open System.Threading
open FSharp.Collections.ParallelSeq
open TranscripterLib
open TranscripterUI.ViewModels

type TranscriptWord(offset: float32) =
    member val Offset = offset with get, set
    member val Duration = 0f with get, set
    member val Word = "" with get, set

type ProcessFileState =
    | NotStarted
    | Working
    | Cancelled
    | Done of string
    | Failed of string

    member this.StringRep =
        match this with
        | NotStarted -> "Not started"
        | Working -> "Currently processing"
        | Cancelled -> "Cancelled"
        | Done s -> $"Done {s}"
        | Failed s -> $"Failed: {s}"

type ProcessingConfig(?config: ConfigureViewModel) =
    member val NumCPUs =
        match config with
        | Some config -> config.NumCPUs
        | None -> 1 with get, set

    member val MaxWordLength =
        match config with
        | Some config -> config.MaxWordLength
        | None -> 10 with get, set

    member val MaxLineLength =
        match config with
        | Some config -> config.MaxLineLength
        | None -> 100 with get, set

    member val NumCandidates =
        match config with
        | Some config -> config.NumCandidates
        | None -> 1u with get, set

    member val ModelPath =
        config
        |> Option.map (fun config -> config.ModelPath)
        |> Option.flatten with get, set

    member val ScorerPath =
        config
        |> Option.map (fun config -> config.ScorerPath)
        |> Option.flatten with get, set

    member val LineSplitStrategy =
        let index =
            match config with
            | Some config -> config.LineSplittingIndex
            | None -> 0

        ConfigureViewModel.LineSplittingOptions[index]

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
    member val TotalTimeTaken = "Transcription completed in 0.00s " with get, set

    member this.SetFiles(fileListConfig: List<FileListEntry>) =
        this.ProcessingFiles.Clear()

        fileListConfig
        |> Seq.iter (fun f -> this.ProcessingFiles.Add(ProcessFile(f.In, f.Out)))

    member this.SetConfig(config: ConfigureViewModel) = this.Config <- ProcessingConfig(config)

    member this.NumFilesDone() =
        (0, this.ProcessingFiles)
        ||> Seq.fold (fun acc file ->
            match file.Status with
            | NotStarted
            | Working -> acc + 0
            | Cancelled
            | Done _
            | Failed _ -> acc + 1)

    member this.BuildTranscript(result: STTClient.Models.Metadata) =
        let bestTranscript =
            result.Transcripts
            |> Seq.maxBy (fun transcript -> transcript.Confidence)

        let mutable currentWord: Option<TranscriptWord> =
            None

        let mutable currentLine: ResizeArray<TranscriptWord> =
            ResizeArray()

        let mutable transcriptList: ResizeArray<ResizeArray<TranscriptWord>> =
            ResizeArray()

        bestTranscript.Tokens
        |> Seq.iter (fun token ->
            if System.String.IsNullOrWhiteSpace token.Text then
                match currentWord with
                | Some word ->
                    word.Duration <- max 0.0f (token.StartTime - word.Offset)
                    currentLine.Add(word)
                    currentWord <- None

                    match this.Config.LineSplitStrategy with
                    | LineControlType.WordLength ->
                        if currentLine.Count >= this.Config.MaxWordLength then
                            transcriptList.Add(currentLine)
                            currentLine <- ResizeArray()
                    | LineControlType.CharLength ->
                        if currentLine
                           |> Seq.sumBy (fun word -> word.Word.Length)
                           >= this.Config.MaxLineLength then
                            transcriptList.Add(currentLine)
                            currentLine <- ResizeArray()
                    | LineControlType.WordAndCharLength ->
                        if currentLine.Count >= this.Config.MaxWordLength
                           || currentLine
                              |> Seq.sumBy (fun word -> word.Word.Length)
                              >= this.Config.MaxLineLength then
                            transcriptList.Add(currentLine)
                            currentLine <- ResizeArray()
                | None -> ()
            else
                let word =
                    match currentWord with
                    | Some word -> word
                    | None -> TranscriptWord(token.StartTime)

                word.Word <- word.Word + token.Text
                word.Duration <- max 0.0f (token.StartTime - word.Offset)
                currentWord <- Some(word))
        
        match currentWord with
        | Some word ->
            if word.Word.Length > 0 then
                currentLine.Add(word)
                transcriptList.Add(currentLine)
        | None -> ()

        transcriptList
        |> Seq.iter (fun line ->
            line
            |> Seq.iter (fun word -> printfn $"{word.Word} - at {word.Offset}, for {word.Duration} seconds")

            printfn "====")

        ()

    member this.ProcessFiles() =
        // Note that STT clients cannot be "shared" across a concurrent "job" - each job needs its own dedicated
        // client, otherwise you are guaranteed to get a nasty crash when running jobs in parallel.
        //
        // Therefore, we pre-allocate multiple clients and dump them it into a list, alongside an index list. The index
        // list controls which client is accessed, and we synchronize this alongside our parallel thread accesses.
        //
        // Note that the accesses to this index list should _probably_ be protected by a mutex, since it's parallel...
        let processMutex = new Mutex()

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
            [ for i in 0 .. (clients.Length - 1) -> i ]
            |> ResizeArray

        assert (clientIndices.Count = clients.Length)

        let totalFileWatch =
            System.Diagnostics.Stopwatch.StartNew()

        if not clients.IsEmpty then
            this.ProcessingFiles
            |> PSeq.withDegreeOfParallelism clients.Length
            |> PSeq.map (fun file ->
                processMutex.WaitOne() |> ignore

                let clientIndex = clientIndices[0]
                clientIndices.RemoveAt(0)

                processMutex.ReleaseMutex()

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
                    this.BuildTranscript(result)
                    file.Status <- ProcessFileState.Done $"(%1.2f{time}s)"
                | Error err ->
                    ProcessingViewModel.Log.Debug $"{file.In} task - err: {err}"
                    file.Status <- ProcessFileState.Failed $"{err} (%1.2f{time}s)"

                processMutex.WaitOne() |> ignore

                clientIndices.Add(clientIndex)

                processMutex.ReleaseMutex())
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
