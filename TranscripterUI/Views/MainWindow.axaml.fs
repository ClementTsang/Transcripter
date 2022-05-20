namespace TranscripterUI.Views

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml

type MainWindow () as this = 
    inherit Window ()
    
    [<NonSerialized>]
    let ofd = OpenFileDialog()
    
    do this.InitializeComponent()
    
    member private this.InitializeComponent() =
#if DEBUG
        this.AttachDevTools()
#endif
        AvaloniaXamlLoader.Load(this)
        
        let allFilter = FileDialogFilter()
        allFilter.Name <- "All Files"
        allFilter.Extensions.Add("*")
        
        ofd.AllowMultiple <- true
        ofd.Filters.Add(allFilter)
        ofd.Title <- "Select files to transcribe"
        
        this.FindControl<Button>("SelectFiles").Click
        |> Event.add(fun _ ->
            async {
                ofd.ShowAsync(this)
                |> Async.AwaitTask
                |> Async.Ignore // TODO: Remove this later
                |> Async.RunSynchronously
            } |> Async.Start
        )
            