namespace TranscripterUI.ViewModels

open System.Collections.ObjectModel
open TranscripterUI.ViewModels

type FileListViewModel() =
    inherit ViewModelBase()

    member val FileListConfiguration = ObservableCollection([]) with get, set
