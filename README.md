# Transcripter

A simple program to get transcriptions from files with audio. 

Written in F#, and built primarily on top of Coqui's [STT](https://github.com/coqui-ai/STT) and
[FFMpegCore](https://github.com/rosenbjerg/FFMpegCore) for the core transcription code.

## Usage

--- TODO: In progress ---

### Install FFMpeg


## Development

This repo is split into three parts:

- [`TranscripterLib`](./TranscripterLib): The "core" transcription library; its just a simple wrapper on top of STT for speech-to-text
  and FFMpegCore to extract audio from media files.
- [`STTClient`](./STTClient): Extracted directly from [the dotnet portion](https://github.com/coqui-ai/STT/tree/main/native_client/dotnet)
  of [the STT repo](https://github.com/coqui-ai/STT) for usage in `TranscripterLib`.
- [`TranscripterUI`](./TranscripterUI): A simple UI to transcribe files, built on [Avalonia](https://avaloniaui.net/).

## Motivation

I was looking for a simple offline program to get transcripts from a bunch of lecture recordings I had, and also wanted a bit of
an excuse to play with Avalonia and STT.