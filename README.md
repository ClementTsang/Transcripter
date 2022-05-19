<div style="text-align: center">
  <h1>Transcripter</h1>

  <p>
  A simple, offline program to automatically get transcriptions from files with audio.
  </p>
</div>

## Usage

--- TODO: In progress ---

### Install FFMpeg

## Development

If you want to mess around with the repo, feel free to clone it. This project is written in F#, and as of writing, built on .NET Core 6.0.102.

### Project Structure

This repo is split into two main parts:

- [`TranscripterLib`](./TranscripterLib): The "core" library for transcription logic;
  not much more than a simple wrapper on top of  Coqui's [STT](https://github.com/coqui-ai/STT) and
  [FFMpegCore](https://github.com/rosenbjerg/FFMpegCore) to extract audio from media files.
- [`TranscripterUI`](./TranscripterUI): Handles the UI and application logic to transcribe files, built on [Avalonia](https://avaloniaui.net/).

A submodule from v1.3.0 of [the STT repo](https://github.com/coqui-ai/STT) is also included in the repo - the main important part is the [.NET library](https://github.com/coqui-ai/STT/tree/main/native_client/dotnet) portion of the repo.

The English language model used for speech-to-text is also from STT (English, 1.0.0). This can be found inside the `TranscripterLib` portion of the repo [here](./TranscripterLib/model/).

## Motivation

I was looking for a simple offline program to get transcripts from a bunch of lecture recordings I had, and also wanted a bit of an excuse to get my feet wet with F#, Avalonia, and STT.
