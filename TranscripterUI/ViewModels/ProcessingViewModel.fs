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
open Whisper.net


type TranscriptLine(startTime: TimeSpan, endTime: TimeSpan, words: String) =
    member val Start = startTime with get, set
    member val End = endTime with get, set
    member val Words = words with get, set

    member this.Display(index: int, format: FileType) =
        match format with
        | FileType.SRT ->
            let startTimeDisplay =
                this.Start.ToString($@"hh\:mm\:ss\,fff")

            let endTimeDisplay =
                this.End.ToString($@"hh\:mm\:ss\,fff")

            $"{index + 1}\n{startTimeDisplay} --> {endTimeDisplay}\n{this.Words}\n"
        | FileType.VTT ->
            let startTimeDisplay =
                this.Start.ToString($@"hh\:mm\:ss\.fff")

            let endTimeDisplay =
                this.End.ToString($@"hh\:mm\:ss\.fff")

            $"{startTimeDisplay} --> {endTimeDisplay}\n{this.Words}\n"



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

    member val ModelPath =
        config
        |> Option.map (fun config -> config.ModelPath)
        |> Option.flatten
        |> Option.defaultValue "model/ggml-base.bin" with get, set

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

    member this.BuildTranscript(segments: List<SegmentData>) =
        segments
        |> Seq.map(fun segment ->
            TranscriptLine(segment.Start, segment.End, segment.Text.Replace("[BLANK_AUDIO]", "").Trim())
        )
        |> Seq.filter (fun line ->
            not (String.IsNullOrWhiteSpace line.Words)
        )
        |> Seq.toList

    member this.WriteTranscript(transcript: List<TranscriptLine>, outputPath: string) =
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
                
                let parent = Directory.GetParent(outputPath)
                Directory.CreateDirectory(parent.FullName) |> ignore
                
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
        // We pre-allocate multiple clients and dump them it into a list, alongside an index list. The index
        // list controls which client is accessed, and we synchronize this alongside our parallel thread accesses.
        //
        // Note that the accesses to this index list should _probably_ be protected by a mutex, since it's parallel...
        let processMutex = new Mutex()
        
        // TODO:
        // - Allow configuring number of threads per client
        // - Set up a threshold for confidence!
        // - Allow setting a language.
        // - Allow selecting GPU acceleration.
        // - Update Avalonia version.

        let numClients =
            min this.Config.NumCPUs this.ProcessingFiles.Count
            
        let numThreads = Math.Clamp(Environment.ProcessorCount / this.Config.NumCPUs, 1, Math.Max(1, Environment.ProcessorCount - 3))
        ProcessingViewModel.Log.Debug $"Using {numThreads} per client."

        let clients =
            [ for _ in 1..numClients -> Transcripter.NewClient(this.Config.ModelPath, numThreads) ]
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

                    let transcription = client.Transcribe(file.In)

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
