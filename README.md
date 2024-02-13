<div align="center">
  <h1>Transcripter</h1>

  <p>
  A simple, offline program to automatically get transcriptions from files with English audio.</br>
  Built with F# alongside OpenAI's Whisper, FFMpeg + FFMpegCore, and Avalonia.
  </p>
</div>

https://user-images.githubusercontent.com/34804052/171553226-64915e3e-f42a-4b44-a44a-8d21cac70509.mp4

## Development

To work on this, clone the repo and install FFMpeg + ffprobe.

This project is written in F#, and as of writing, built on .NET Core 6.0.102. Personally, I wrote this on
Linux and used Rider for development.

### Project structure

This repo is split into two main parts:

- [`TranscripterLib`](./TranscripterLib): The "core" library for transcription logic. This isn't really much more than a
  simple wrapper on top of
  OpenAI's [Whisper](https://github.com/openai/whisper) for speech-to-text and FFMpeg via FFMpegCore to convert video files
  into the appropriate audio files for Whisper.

- [`TranscripterUI`](./TranscripterUI): Handles the UI and application logic to transcribe files, built
  on [Avalonia](https://avaloniaui.net/).


## Disclaimer

Note that Transcripter is not perfect - the generated transcripts are not guaranteed to be
completely correct, and its generated transcripts should be treated as more of a starting point if perfect correctness
is required. Transcripter will also struggle on words that aren't clearly pronounced, or words that are not standard English.

Furthermore, this project isn't one that I'm actively going to maintain too much - it was more a project for fun. I'll
accept bug reports and PRs though.

## Motivation

I was looking for a simple offline program to get transcripts from a bunch of lecture recordings I had, and also wanted
a bit of an excuse to get my feet wet with F# and Avalonia.
