namespace TranscripterLib

open System
open System.IO
open System.Threading
open FFMpegCore
open Microsoft.VisualBasic.FileIO
open NAudio.Wave
open STTClient

module Transcripter =
    let private deleteIfExists filePath =
        if File.Exists filePath then
            File.Delete filePath

    let public PotentiallyValidFile (inputPath: string) =
        task {
            try
                let! probe = FFProbe.AnalyseAsync(inputPath)
                return probe.AudioStreams.Count > 0
            with
            | _ -> return false
        }

    type public TranscripterClient(stt: STT) =
        let client = stt

        member this.Transcribe(inputPath: string, ?numAttempts: uint, ?token: CancellationToken) =
            if File.Exists inputPath then
                let sampleRate = client.GetModelSampleRate()

                let wavPath =
                    let tmp = FileSystem.GetTempFileName()

                    let newName =
                        Path.ChangeExtension(tmp, "wav")

                    if File.Exists(tmp) && not (File.Exists(newName)) then
                        FileSystem.MoveFile(tmp, newName)
                        newName
                    else
                        tmp

                try
                    let args =
                        FFMpegArguments
                            .FromFileInput(inputPath)
                            .OutputToFile(
                                wavPath,
                                true,
                                fun options ->
                                    options
                                        .WithAudioSamplingRate(sampleRate)
                                        .WithFastStart()
                                        .WithCustomArgument("-ac 1")
                                        .WithCustomArgument("-async 1")
                                    |> ignore
                            )

                    match token with
                    | Some token ->
                        args
                            .CancellableThrough(token)
                            .ProcessAsynchronously(true)
                            .Wait()
                    | None -> args.ProcessAsynchronously(true).Wait()

                    let inputBytes = File.ReadAllBytes(wavPath)

                    let buffer = WaveBuffer inputBytes

                    let bufferSize =
                        Convert.ToUInt32(buffer.MaxSize / 2)

                    let numAttempts =
                        match numAttempts with
                        | Some numAttempts -> numAttempts
                        | None -> 1u

                    let result =
                        client.SpeechToTextWithMetadata(buffer.ShortBuffer, bufferSize, numAttempts)

                    buffer.Clear()

                    deleteIfExists wavPath
                    Ok result
                with
                | ex ->
                    deleteIfExists wavPath
                    Error(ex.ToString())
            else
                Error($"{inputPath} does not exist.")

    let public NewClient (useScorer: bool, modelFilePath: string, scorerFilePath: string) =
        if not (File.Exists modelFilePath) then
            Error($"Failed to load model file at  `{modelFilePath}`.")
        else if useScorer && not (File.Exists scorerFilePath) then
            Error($"Failed to load scorer file at `{scorerFilePath}`.")
        else
            let client = new STT(modelFilePath)

            if useScorer then
                client.EnableExternalScorer(scorerFilePath)

            Ok(TranscripterClient(client))
