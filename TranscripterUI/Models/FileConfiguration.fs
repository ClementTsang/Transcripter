namespace TranscripterUI.Models

type FileConfig(inputFile: string) =
    member val InputFile = inputFile with get, set
    member val OutputFile = "" with get, set

    member val IsValid =
        TranscripterLib
            .Transcripter
            .PotentiallyValidFile(
                inputFile
            )
            .Result
