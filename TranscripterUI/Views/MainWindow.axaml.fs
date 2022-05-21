namespace TranscripterUI.Views

open System.Reactive.Disposables
open ReactiveUI
open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml
open Avalonia.ReactiveUI
open TranscripterUI.ViewModels

type MainWindow () as this = 
    inherit ReactiveWindow<MainWindowViewModel>()
    
    do this.InitializeComponent()
    
    member private this.InitializeComponent() =
#if DEBUG
        this.AttachDevTools()
#endif
        AvaloniaXamlLoader.Load(this)
        this.WhenActivated(fun (disposable: CompositeDisposable) ->
            let handler = this.ViewModel.SelectFilesVM.ShowOpenFileDialog.RegisterHandler(this.ShowOpenFileDialog)
            disposable.Add(handler)
        ) |> ignore
        
    member private this.ShowOpenFileDialog (interaction: InteractionContext<Unit, List<string>>) =
        let dialog = OpenFileDialog()
        
        let allFilter = FileDialogFilter()
        allFilter.Name <- "All Files"
        allFilter.Extensions.Add("*")
        
        dialog.AllowMultiple <- true
        dialog.Filters.Add(allFilter)
        dialog.Title <- "Select files to transcribe"
        
        printfn("Opening file dialog.")
        task {
            async {
                interaction.SetOutput(
                    dialog.ShowAsync(this)
                    |> Async.AwaitTask
                    |> Async.RunSynchronously
                    |> fun arr -> if isNull(arr) then List.Empty else Array.toList arr
                )
            } |> Async.Start
        }