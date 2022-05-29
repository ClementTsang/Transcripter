namespace TranscripterUI.Views

open System.IO
open System.Reactive.Disposables
open ReactiveUI
open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml
open Avalonia.ReactiveUI
open TranscripterUI.ViewModels

type MainWindow() as this =
    inherit ReactiveWindow<MainWindowViewModel>()

    do this.InitializeComponent()

    member private this.InitializeComponent() =
#if DEBUG
        this.AttachDevTools()
#endif
        AvaloniaXamlLoader.Load(this)
        this.BindSelectFileDialog()

    member private this.BindSelectFileDialog() =
        this.WhenActivated (fun (disposable: CompositeDisposable) ->
            disposable.Add(this.ViewModel.ShowOpenFileDialog.RegisterHandler(this.ShowOpenFileDialog))
            disposable.Add(this.ViewModel.FileListVM.ShowSaveFileDialog.RegisterHandler(this.ShowSaveFileDialog)))
        |> ignore

    /// <summary>
    ///     Opens the file dialog and sends the selected files through the given InteractionContext.
    ///
    ///     Approach based on the <a href="https://github.com/grokys/FileDialogMvvm">following example</a>.
    /// </summary>
    member private this.ShowOpenFileDialog(interaction: InteractionContext<Unit, List<string>>) =
        let dialog = OpenFileDialog()

        let allFilter = FileDialogFilter()
        allFilter.Name <- "All Files"
        allFilter.Extensions.Add("*")

        dialog.AllowMultiple <- true
        dialog.Filters.Add(allFilter)
        dialog.Title <- "Select files to transcribe"

        task {
            interaction.SetOutput(
                dialog.ShowAsync(this)
                |> Async.AwaitTask
                |> Async.RunSynchronously
                |> fun arr ->
                    if isNull arr then
                        List.Empty
                    else
                        Array.toList arr
            )
        }

    member private this.ShowSaveFileDialog(interaction: InteractionContext<string, Option<string>>) =
        let dialog = SaveFileDialog()
        let input = interaction.Input
        
        dialog.InitialFileName <- Path.GetFileName input
        dialog.Directory <- Path.GetDirectoryName (Path.GetFullPath input) // Currently bugged on Linux, see https://github.com/AvaloniaUI/Avalonia/issues/7535
        dialog.Title <- "Set transcript file save location"
        dialog.DefaultExtension <- Path.GetExtension input
        
        task {
            interaction.SetOutput(
                dialog.ShowAsync(this)
                |> Async.AwaitTask
                |> Async.RunSynchronously
                |> fun out ->
                    if isNull out then
                        None
                    else
                        Some(out)
            )
        }
