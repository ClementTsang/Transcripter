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

    let public NewClient(useScorer: bool) =
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
        let tempAudioPath = FileSystem.GetTempFileName() + ".mp3"
        let tempOutputWavPath = FileSystem.GetTempFileName() + ".wav"

        let currentAudioPath =
            let probe = FFProbe.Analyse(inputPath)

            if isNull (probe.PrimaryVideoStream) then
                // Treat it as an audio file.
                Ok(inputPath)
            else
                // Not audio, convert with ffmpeg.
                try
                    if FFMpeg.ExtractAudio(inputPath, tempAudioPath) then
                        Ok(tempAudioPath)
                    else
                        deleteIfExists tempAudioPath
                        deleteIfExists tempOutputWavPath
                        Error("Could not extract audio.")
                with
                | ex ->
                    deleteIfExists tempAudioPath
                    deleteIfExists tempOutputWavPath
                    Error(ex.ToString())

        match currentAudioPath with
        | Error (err) -> Error(err)
        | Ok (currentAudioPath) ->
            try
                FFMpegArguments
                    .FromFileInput(currentAudioPath)
                    .OutputToFile(
                        tempOutputWavPath,
                        false,
                        fun options ->
                            options
                                .WithAudioSamplingRate(16000)
                                .WithFastStart()
                                .WithCustomArgument("-ac 1")
                            |> ignore
                    )
                    .ProcessAsynchronously(true)
                    .Wait()

                let inputBytes = File.ReadAllBytes(tempOutputWavPath)
                let buffer = WaveBuffer inputBytes
                let bufferSize = Convert.ToUInt32(buffer.MaxSize / 2)
                let result = client.SpeechToText(buffer.ShortBuffer, bufferSize)
                buffer.Clear()

                deleteIfExists tempAudioPath
                deleteIfExists tempOutputWavPath
                Ok result
            with
            | ex ->
                deleteIfExists tempAudioPath
                deleteIfExists tempOutputWavPath
                Error(ex.ToString())
