namespace TranscripterUI.Views

open System.Runtime.Serialization
open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml

[<DataContract>]
type HelpWindow () as this = 
    inherit Window ()

    do this.InitializeComponent()

    member private this.InitializeComponent() =
#if DEBUG
        this.AttachDevTools()
#endif
        AvaloniaXamlLoader.Load(this)
