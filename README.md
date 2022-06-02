<div align="center">
  <h1>Transcripter</h1>

  <p>
  A simple, offline program to automatically get transcriptions from files with English audio.</br>
  Primarily built on Coqui's STT 1.3.0, FFMpeg + FFMpegCore, and Avalonia.
  </p>
</div>

https://user-images.githubusercontent.com/34804052/171553226-64915e3e-f42a-4b44-a44a-8d21cac70509.mp4

## Development

To work on this, clone the repo and install FFMpeg + ffprobe, then run the setup script (`setup.sh`) to download all additional
required files. Note this script relies on `wget` to download files.

This project is written in F#, and as of writing, built on .NET Core 6.0.102. Personally, I wrote this on
Linux and used Rider for development.

### Project structure

This repo is split into two main parts:

- [`TranscripterLib`](./TranscripterLib): The "core" library for transcription logic. This isn't really much more than a
  simple wrapper on top of
  Coqui's [STT](https://github.com/coqui-ai/STT) for speech-to-text and FFMpeg via FFMpegCore to convert video files
  into the appropriate audio files for STT.

- [`TranscripterUI`](./TranscripterUI): Handles the UI and application logic to transcribe files, built
  on [Avalonia](https://avaloniaui.net/).

Transcripter's speech-to-text is currently based on version 1.3.0 of STT, and a submodule from v1.3.0
of [the STT repo](https://github.com/coqui-ai/STT) is also included in the repo for usage in .NET - the main
important part is the [.NET library](https://github.com/coqui-ai/STT/tree/main/native_client/dotnet) portion of the
repo.

The English language model and scorer used for speech-to-text is also from STT (English, 1.0.0). This can be found
inside the `TranscripterLib` portion of the repo [here](./TranscripterLib/model), and
the model/scorer itself can be found from Coqui's website [here](https://coqui.ai/english/coqui/v1.0.0-huge-vocab).

### Running

If you're running via something like Rider's Run, you may need to set the `LD_LIBRARY_PATH` environment variable
to not be blank (if it is) for the STT shared libraries to be detected. For example:

```bash
LD_LIBRARY_PATH=:
```

seems to work. This appears to be [a bug](https://github.com/dotnet/sdk/issues/9586) with dotnet in general.

## Disclaimer

Note that Transcripter is not perfect - the generated transcripts are not guaranteed to be
completely correct, and its generated transcripts should be treated as more of a starting point if perfect correctness
is required. Transcripter will also struggle on words that aren't clearly pronounced, or words that are not standard English.

Furthermore, this project isn't one that I'm actively going to maintain too much - it was more a project for fun. I'll
accept bug reports and PRs though.

## Motivation

I was looking for a simple offline program to get transcripts from a bunch of lecture recordings I had, and also wanted
a bit of an excuse to get my feet wet with F#, Avalonia, and STT.
