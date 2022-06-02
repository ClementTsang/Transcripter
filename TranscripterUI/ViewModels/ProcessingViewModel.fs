namespace TranscripterUI.ViewModels

open System
open System.IO
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


type TranscriptLine() =
    member val Offset = 0f with get, set
    member val Duration = 0f with get, set
    member val Words: ResizeArray<string> = ResizeArray() with get, set

    member this.AddWord(word: TranscriptWord) =
        if this.Words.Count = 0 then
            this.Offset <- word.Offset

        this.Words.Add word.Word
        this.Duration <- word.Duration + word.Offset - this.Offset

    member this.Display(index: int, format: FileType) =
        let startTime =
            TimeSpan.FromSeconds(float this.Offset)

        let endTime =
            TimeSpan.FromSeconds((float this.Offset) + (float this.Duration))

        match format with
        | FileType.SRT ->
            let startTimeDisplay =
                startTime.ToString($@"hh\:mm\:ss\,fff")

            let endTimeDisplay =
                endTime.ToString($@"hh\:mm\:ss\,fff")

            let line = this.Words |> String.concat " "

            $"{index + 1}\n{startTimeDisplay} --> {endTimeDisplay}\n{line}\n"
        | FileType.VTT ->
            let startTimeDisplay =
                startTime.ToString($@"hh\:mm\:ss\.fff")

            let endTimeDisplay =
                endTime.ToString($@"hh\:mm\:ss\.fff")

            let line = this.Words |> String.concat " "

            $"{startTimeDisplay} --> {endTimeDisplay}\n{line}\n"



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
    member val OverwriteFiles =
        match config with
        | Some config -> config.OverwriteSelectedIndex = 0
        | None -> false

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
        |> Option.flatten
        |> Option.defaultValue "model/english_huge_1.0.0_model.tflite" with get, set

    member val ScorerPath =
        config
        |> Option.map (fun config -> config.ScorerPath)
        |> Option.flatten
        |> Option.defaultValue "model/huge-vocabulary.scorer" with get, set

    member val LineSplitStrategy =
        let index =
            match config with
            | Some config -> config.LineSplittingIndex
            | None -> 0

        ConfigureViewModel.LineSplittingOptions[index]

    member val AutomaticSentence =
        match config with
        | Some config -> config.AutoSentenceIndex = 1
        | None -> false

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
    member val FileWriteMutex = new Mutex()

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

    member this.shouldSplit(currentLine: TranscriptLine) =
        match this.Config.LineSplitStrategy with
        | LineControlType.WordLength ->
            currentLine.Words.Count
            >= this.Config.MaxWordLength
        | LineControlType.CharLength ->
            currentLine.Words
            |> Seq.sumBy (fun word -> word.Length)
            >= this.Config.MaxLineLength
        | LineControlType.WordAndCharLength ->
            currentLine.Words.Count
            >= this.Config.MaxWordLength
            || currentLine.Words
               |> Seq.sumBy (fun word -> word.Length)
               >= this.Config.MaxLineLength

    member this.BuildTranscript(result: STTClient.Models.Metadata) =
        let bestTranscript =
            result.Transcripts
            |> Seq.maxBy (fun transcript -> transcript.Confidence)

        let mutable currentWord: Option<TranscriptWord> =
            None

        let mutable wordList: ResizeArray<TranscriptWord> =
            ResizeArray()

        bestTranscript.Tokens
        |> Seq.iter (fun token ->
            if String.IsNullOrWhiteSpace token.Text then
                match currentWord with
                | Some word ->
                    word.Duration <- max 0.0f (token.StartTime - word.Offset)
                    wordList.Add(word)
                    currentWord <- None
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
                wordList.Add(word)
        | None -> ()

        if this.Config.AutomaticSentence then
            let sentence_sec_offset_threshold = 0.2f

            wordList
            |> Seq.windowed 2
            |> Seq.iter (fun window ->
                let a = window[0]
                let b = window[1]

                if b.Offset - (a.Offset + a.Duration) > sentence_sec_offset_threshold then
                    a.Word <- a.Word + ".")

            if wordList.Count > 0 then
                wordList[wordList.Count - 1].Word <- wordList[wordList.Count - 1].Word + "."

        let mutable transcriptionList: ResizeArray<TranscriptLine> =
            [ TranscriptLine() ] |> ResizeArray

        wordList
        |> Seq.iter (fun word ->
            let currLine = transcriptionList[transcriptionList.Count - 1]
            if this.shouldSplit currLine then
                if word.Offset - (currLine.Duration + currLine.Offset) < 0.1f then
                    currLine.Duration <- word.Offset - currLine.Offset
                else
                    currLine.Duration <- currLine.Duration + 0.1f
                transcriptionList.Add(TranscriptLine())

            transcriptionList[transcriptionList.Count - 1]
                .AddWord word)

        transcriptionList[transcriptionList.Count - 1].Duration <- transcriptionList[transcriptionList.Count - 1].Duration + 0.1f
        
        transcriptionList

    member this.WriteTranscript(transcript: ResizeArray<TranscriptLine>, outputPath: string) =
        let outputFormat =
            let extension =
                (Path.GetExtension outputPath).ToLower()

            match extension with
            | ".srt" -> Ok(FileType.SRT)
            | ".vtt" -> Ok(FileType.VTT)
            | _ -> Error($"invalid file type ({extension})")

        match outputFormat with
        | Ok ft ->
            // We lock this to writing one file at a time for safety reasons.
            this.FileWriteMutex.WaitOne() |> ignore

            if (not this.Config.OverwriteFiles
                && not (File.Exists outputPath))
               || this.Config.OverwriteFiles then
                File.WriteAllText(
                    outputPath,
                    transcript
                    |> Seq.indexed
                    |> Seq.map (fun (index, t) -> t.Display(index, ft))
                    |> String.concat "\n"
                )

                this.FileWriteMutex.ReleaseMutex()
                Ok(())
            else
                this.FileWriteMutex.ReleaseMutex()
                Error("another file already exists at this location")

        | Error err -> Error(err)

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
            |> PSeq.iter (fun file ->
                if (not this.Config.OverwriteFiles
                    && not (File.Exists file.Out))
                   || this.Config.OverwriteFiles then
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


                    match transcription with
                    | Ok result ->
                        ProcessingViewModel.Log.Debug $"{file.In} task - ok"

                        let transcript =
                            this.BuildTranscript(result)

                        let writeTask =
                            this.WriteTranscript(transcript, file.Out)

                        perFileWatch.Stop()

                        let time =
                            (float perFileWatch.ElapsedMilliseconds) / 1000.0

                        ProcessingViewModel.Log.Debug $"Transcription + write task took %1.2f{time}s"

                        match writeTask with
                        | Ok _ ->
                            ProcessingViewModel.Log.Debug $"{file.In} write finished"
                            file.Status <- ProcessFileState.Done $"(%1.2f{time}s)"
                        | Error err ->
                            ProcessingViewModel.Log.Debug $"{file.In} write failed - err: {err}"
                            file.Status <- ProcessFileState.Failed $"{err} (%1.2f{time}s)"
                    | Error err ->
                        perFileWatch.Stop()

                        let time =
                            (float perFileWatch.ElapsedMilliseconds) / 1000.0

                        ProcessingViewModel.Log.Debug $"{file.In} task - err: {err}"
                        file.Status <- ProcessFileState.Failed $"{err} (%1.2f{time}s)"

                    processMutex.WaitOne() |> ignore
                    clientIndices.Add(clientIndex)
                    processMutex.ReleaseMutex()
                else
                    file.Status <- ProcessFileState.Failed "another file already exists at this location")
        else
            // TODO: Handle the case all clients fail. Also probably want to wrap this with a try/catch.
            ()

        totalFileWatch.Stop()
        this.Finished <- true

        this.TotalTimeTaken <-
            $"Transcription completed in %1.2f{(float totalFileWatch.ElapsedMilliseconds)
                                               / 1000.0}s"

        this.RaisePropertyChanged("Finished")
        this.RaisePropertyChanged("TotalTimeTaken")
        ProcessingViewModel.Log.Debug this.TotalTimeTaken
