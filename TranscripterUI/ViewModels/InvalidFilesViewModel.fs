namespace TranscripterUI.ViewModels

open TranscripterUI.ViewModels

type InvalidFile(name: string, error: string) =
    member val Name = name with get
    member val Error = error with get

type InvalidFilesViewModel() =
    inherit ViewModelBase()
    
    member val InvalidFiles: List<InvalidFile> = [] with set, get
