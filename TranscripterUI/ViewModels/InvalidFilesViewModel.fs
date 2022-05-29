namespace TranscripterUI.ViewModels

open TranscripterUI.ViewModels

type InvalidFile(name: string, error: string) =
    member val Name = name
    member val Error = error

type InvalidFilesViewModel() =
    inherit ViewModelBase()

    member val InvalidFiles: List<InvalidFile> = [] with get, set
