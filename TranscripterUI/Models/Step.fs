namespace TranscripterUI.Models

//open System.ComponentModel
open TranscripterUI.ViewModels

type StepProgress =
    | Completed
    | InProgress
    | Upcoming

type Step(num: int, text: string, vm: ViewModelBase, isLast: bool, progress: StepProgress) =
    [<Literal>]
    let white = "#fff"

    [<Literal>]
    let backgroundGrey = "#2c2f33"

    [<Literal>]
    let accent = "#268bd2"

    [<Literal>]
    let veryLightGrey = "#686f78"

    [<Literal>]
    let lighterGrey = "#3f4449"

    //    let event = Event<_, _>()
//
//    let mutable progressState = progress
//    member this.ProgressState
//        with get(): StepProgress = progressState
//        and set value =
//            progressState <- value
//            event.Trigger(this, PropertyChangedEventArgs("ProgressState"))
//
//    interface INotifyPropertyChanged with
//        [<CLIEvent>]
//        member this.PropertyChanged = event.Publish

    member val ProgressState = progress with get, set

    member val IsNotLast = if isLast then "False" else "True"
    member val Position = num * 2
    member val SeparatorIndex = if isLast then 0 else (num * 2) + 1
    member val Num = num + 1
    member val Index = num
    member val Text = text
    member val StepViewModel = vm
    member val Enabled = true with get, set

    member this.IsCompleted =
        match this.ProgressState with
        | StepProgress.Completed -> "True"
        | StepProgress.InProgress -> "False"
        | StepProgress.Upcoming -> "False"

    member this.IsEnabled =
        this.Enabled
        && (match this.ProgressState with
            | StepProgress.Completed -> true
            | _ -> false)

    member this.NumberColour =
        match this.ProgressState with
        | StepProgress.Completed -> backgroundGrey
        | StepProgress.InProgress -> white
        | StepProgress.Upcoming -> white

    member this.TextColour =
        match this.ProgressState with
        | StepProgress.Completed -> white
        | StepProgress.InProgress -> accent
        | StepProgress.Upcoming -> veryLightGrey

    member this.BorderColour =
        match this.ProgressState with
        | StepProgress.Completed -> accent
        | StepProgress.InProgress -> accent
        | StepProgress.Upcoming -> veryLightGrey

    member this.LineColour =
        match this.ProgressState with
        | StepProgress.Completed -> accent
        | StepProgress.InProgress -> lighterGrey
        | StepProgress.Upcoming -> lighterGrey

    member this.InnerCircleColour =
        match this.ProgressState with
        | StepProgress.Completed -> accent
        | StepProgress.InProgress -> backgroundGrey
        | StepProgress.Upcoming -> lighterGrey
