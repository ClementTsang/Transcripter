namespace TranscripterUI

open System
open Avalonia
open Avalonia.ReactiveUI
open TranscripterUI

module Program =

    [<CompiledName "BuildAvaloniaApp">] 
    let buildAvaloniaApp () = 
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .LogToTrace(areas = Array.empty)
            .UseReactiveUI()

    [<EntryPoint; STAThread>]
    let main argv =
        buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)