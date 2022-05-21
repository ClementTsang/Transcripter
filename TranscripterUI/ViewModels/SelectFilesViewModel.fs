namespace TranscripterUI.ViewModels

open System.Threading.Tasks
open ReactiveUI

type SelectFilesViewModel() =
    inherit ViewModelBase()

    member val ShowOpenFileDialog = Interaction<Unit, List<string>>()
    
    member private this.SelectFilesAsync =
        fun () ->
            Task.Factory.StartNew(fun () ->
                this.ShowOpenFileDialog.Handle(()).Subscribe(
                    fun files ->
                        printfn($"files: {files}")
                ) |> ignore
            )
        
    member this.SelectFiles = ReactiveCommand.CreateFromTask(this.SelectFilesAsync)