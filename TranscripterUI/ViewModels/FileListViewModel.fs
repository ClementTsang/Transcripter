namespace TranscripterUI.ViewModels

open System.Collections.ObjectModel
open TranscripterUI.Models
open TranscripterUI.ViewModels

type FileListViewModel() =
    inherit ViewModelBase()

    member val FileListConfiguration = ObservableCollection([]) with get, set

    member this.SetFileList(files: list<string>) =
        let fileMappingList =
            files |> Seq.map (FileConfig) |> Seq.toList

        this.FileListConfiguration <- ObservableCollection(fileMappingList)
