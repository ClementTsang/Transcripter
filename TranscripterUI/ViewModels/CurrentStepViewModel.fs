namespace TranscripterUI.ViewModels

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

    member val IsNotLast = if isLast then "False" else "True"
    member val Position = num * 2
    member val SeparatorIndex = if isLast then 0 else (num * 2) + 1
    member val Num = num + 1
    member val Text = text
    member val ProgressState = progress with get, set
    member val StepViewModel = vm

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

type CurrentStepViewModel(steps: List<string * ViewModelBase>) =
    inherit ViewModelBase()

    member val CurrentStepIndex = 0 with get, set

    member val Steps =
        [ for index, (text, vm) in (steps |> List.indexed) ->
              Step(
                  index,
                  text,
                  vm,
                  (index + 1) = steps.Length,
                  if index = 0 then
                      StepProgress.InProgress
                  else
                      StepProgress.Upcoming
              ) ]

    member val NumberSteps = steps.Length with get, set

    member this.Increment =
        if this.CurrentStepIndex < this.NumberSteps then
            this.CurrentStepIndex <- this.CurrentStepIndex + 1
            
    member this.Decrement =
        if this.CurrentStepIndex > 0 then
            this.CurrentStepIndex <- this.CurrentStepIndex - 1

    member this.SetStep(stepIndex: int) =
        if stepIndex >= 0 && stepIndex < this.NumberSteps then
            this.Steps
            |> List.indexed
            |> Seq.iter (fun (index, step) ->
                if index < stepIndex then
                    step.ProgressState <- StepProgress.Completed
                else if index = stepIndex then
                    step.ProgressState <- StepProgress.InProgress
                else
                    step.ProgressState <- StepProgress.Upcoming)

            this.CurrentStepIndex <- stepIndex

    member this.GetCurrentStep =
        this.Steps[this.CurrentStepIndex]
