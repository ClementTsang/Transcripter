namespace TranscripterUI.Models

type InputFile(inputFile: string) =
    member val InputFile = inputFile with get, set

    member val IsValid =
        TranscripterLib
            .Transcripter
            .PotentiallyValidFile(
                inputFile
            )
            .Result
