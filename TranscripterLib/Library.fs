namespace TranscripterLib

open System
open System.IO
open FFMpegCore
open NAudio.Wave
open STTClient

module Transcripter =
    let public transcribe (inputPath: string) =
        
        let mediaInfo = FFProbe.Analyse(inputPath)
        let audioStream = mediaInfo.PrimaryAudioStream
        
        if isNull(audioStream) then
            Error()
        else
            let modelFile = Path.Combine(Environment.CurrentDirectory, @"model\english_huge_1.0.0_model.tflite")
            
            if File.Exists modelFile then
                let client = new STT(modelFile)

                let inputBytes = File.ReadAllBytes(inputPath)
                let buffer = WaveBuffer inputBytes // TODO: Can this fail? If it can, should we fallback to ffmpeg?
                let bufferSize = Convert.ToUInt32(buffer.MaxSize / 2)

                let result = client.SpeechToTextWithMetadata(buffer, bufferSize, 1u)
                Ok result
            else
                Error()
