namespace TranscripterLib

open System
open System.IO
open FFMpegCore
open Microsoft.VisualBasic.FileIO
open NAudio.Wave
open STTClient

module Transcripter =
    let deleteIfExists filePath =
        if File.Exists(filePath) then
            File.Delete(filePath)

    let public transcribe (inputPath: string) =
        let tempAudioOutputPath = FileSystem.GetTempFileName()

        if FFMpeg.ExtractAudio(inputPath, tempAudioOutputPath) then
            let modelFile = Path.Combine(Environment.CurrentDirectory, @"model\english_huge_1.0.0_model.tflite")
            
            if File.Exists(modelFile) then
                let sttClient = new STT(modelFile)

                let tempAudioFile = File.ReadAllBytes(tempAudioOutputPath)
                let buffer = WaveBuffer(tempAudioFile)
                let bufferSize = Convert.ToUInt32(buffer.MaxSize / 2)

                let result = sttClient.SpeechToTextWithMetadata(buffer, bufferSize, 1u)
                deleteIfExists tempAudioOutputPath
                Ok result
            else
                deleteIfExists tempAudioOutputPath
                Error()
        else
            deleteIfExists tempAudioOutputPath
            Error()
