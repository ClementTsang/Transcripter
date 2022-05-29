namespace TranscripterUI.ViewModels

open System.Collections.ObjectModel
open System.Threading.Tasks
open ReactiveUI
open TranscripterUI.ViewModels

type FileListEntry(inputFile: string, outputFile: string, index: int) =
    member val In = inputFile
    member val Out = outputFile with get, set
    member val Index = index

type FileListViewModel() =
    inherit ViewModelBase()

    member val FileListConfiguration = ObservableCollection([]) with get, set

    member val ShowSaveFileDialog = Interaction<string, Option<string>>()

    member this.SetOutputFileTask =
        fun (index: int) ->
            Task.Factory.StartNew (fun () ->
                let fileEntry: FileListEntry =
                    this.FileListConfiguration[index]

                this
                    .ShowSaveFileDialog
                    .Handle(fileEntry.Out)
                    .Subscribe(fun file ->
                        match file with
                        | Some (file) ->
                            fileEntry.Out <- file
                            this.FileListConfiguration.RemoveAt(index)
                            this.FileListConfiguration.Insert(index, fileEntry)
                        | None -> ())
                |> ignore)

    member this.SetOutputFile =
        ReactiveCommand.CreateFromTask(this.SetOutputFileTask)
