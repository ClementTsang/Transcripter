namespace TranscripterUI.ViewModels

open TranscripterUI.ViewModels

type StepProgress =
    | Completed
    | InProgress
    | Upcoming
    

type Step(num: int, text: string, isNotLast: bool, progress: StepProgress) =
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
    
    member val IsNotLast = if isNotLast then "False" else "True" with get
    member val Position = num * 2 with get
    member val SeparatorIndex = (num * 2) + 1 with get
    member val Num = num + 1 with get
    member val Text = text with get
    member val ProgressState = progress with get, set
    
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

type CurrentStepViewModel(steps: List<string>) =
    inherit ViewModelBase()
    
    member val CurrentStep = 1 with get, set
    member val Steps = [for index, text in (steps |> List.indexed) -> Step(index, text, (index + 1) = steps.Length, (if index = 0 then StepProgress.InProgress else StepProgress.Upcoming))] with get
    member val NumberSteps = steps.Length with get, set
    
    member this.Increment =
        if this.CurrentStep < this.NumberSteps then
            this.CurrentStep <- this.CurrentStep + 1
            
    member this.SetStep(stepIndex: int) =
        if stepIndex >= 0 && stepIndex < this.NumberSteps then
            this.Steps
            |> List.indexed
            |> Seq.iter(fun (index, step) ->
                if index < stepIndex then
                    step.ProgressState <- StepProgress.Completed
                else if index = stepIndex then
                    step.ProgressState <- StepProgress.InProgress
                else
                    step.ProgressState <- StepProgress.Upcoming
            )
            this.CurrentStep <- stepIndex
            
    member this.Decrement =
        if this.CurrentStep > 0 then
            this.CurrentStep <- this.CurrentStep - 1
            

        
