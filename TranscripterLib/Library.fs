namespace TranscripterLib

open System
open System.IO
open System.Threading
open FFMpegCore
open FSharp.Control
open Microsoft.FSharp.Core
open Microsoft.VisualBasic.FileIO
open NAudio.Wave
open Whisper.net
open Whisper.net.Ggml

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

    type public TranscripterClient(client: WhisperProcessor) =
        let client = client

        member this.Transcribe(inputPath: string, ?token: CancellationToken) =
            if File.Exists inputPath then
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
                                        .WithFastStart()
                                        .WithCustomArgument("-ac 1")
                                        .WithCustomArgument("-ar 16000")
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

                    let fileStream = File.OpenRead(wavPath)
                    
                    let result = client.ProcessAsync(fileStream) |> TaskSeq.toList

                    deleteIfExists wavPath
                    Ok result
                with
                | ex ->
                    deleteIfExists wavPath
                    Error(ex.ToString())
            else
                Error($"{inputPath} does not exist.")

    let public NewClient (modelFilePath: string, numThreads: int) =
        let validFile =
            if not (File.Exists modelFilePath) then
                try
                    // printfn "Model doesn't exist at path, going to try downloading model..."
                    
                    let parent = Directory.GetParent(modelFilePath)
                    Directory.CreateDirectory(parent.FullName) |> ignore
                    
                    let writer = File.OpenWrite(modelFilePath)
                    let stream = WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base) |> Async.AwaitTask |> Async.RunSynchronously
                    stream.CopyToAsync(writer).Wait()
                    writer.Dispose()

                    // printfn "Finished downloading model."
                    
                    Ok(())
                with
                | ex ->
                    Error($"Failed to load model file at  `{modelFilePath}`; failed to download due to {ex}.")
            else
                Ok(())
                
        match validFile with
        | Ok _ ->
            // printfn $"Using model at {modelFilePath}."
            let factory = WhisperFactory.FromPath(modelFilePath)
            let client = factory
                             .CreateBuilder()
                             .WithLanguage("auto")
                             .WithThreads(numThreads)
                             .Build()

            Ok(TranscripterClient(client))
        | Error err ->
            Error(err)
