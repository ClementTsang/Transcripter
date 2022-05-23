namespace TranscripterLib

open System
open System.IO
open FFMpegCore
open Microsoft.VisualBasic.FileIO
open NAudio.Wave
open STTClient

module Transcripter =
    let private deleteIfExists filePath =
        if File.Exists filePath then
            File.Delete filePath

    let public NewClient (useScorer: bool) =
        let modelFile =
            Path.Combine(Environment.CurrentDirectory, @"model/english_huge_1.0.0_model.tflite")

        let scorerFile =
            Path.Combine(Environment.CurrentDirectory, @"model/huge-vocabulary.scorer")

        if not (File.Exists modelFile) then
            Error("Failed to load model file.")
        else if useScorer && not (File.Exists scorerFile) then
            Error("Failed to load scorer file.")
        else
            let client = new STT(modelFile)

            if useScorer then
                client.EnableExternalScorer(scorerFile)

            Ok(client)

    let public Transcribe (client: STT, inputPath: string) =
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
                    .ProcessAsynchronously(true)
                    .Wait()

                let inputBytes = File.ReadAllBytes(wavPath)

                let buffer = WaveBuffer inputBytes

                let bufferSize =
                    Convert.ToUInt32(buffer.MaxSize / 2)

                let result =
                    client.SpeechToTextWithMetadata(buffer.ShortBuffer, bufferSize, 1u)
                    
                buffer.Clear()

                deleteIfExists wavPath
                Ok result
            with
            | ex ->
                deleteIfExists wavPath
                Error(ex.ToString())
        else
            Error($"{inputPath} does not exist.")
