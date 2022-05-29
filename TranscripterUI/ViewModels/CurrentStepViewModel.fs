namespace TranscripterUI.ViewModels

open ReactiveUI
open System.Collections.ObjectModel
open TranscripterUI.Models
open TranscripterUI.ViewModels

type CurrentStepViewModel() =
    inherit ViewModelBase()

    let steps: List<string * ViewModelBase> =
        [ ("Select Files", SelectFilesViewModel())
          ("Configuration", ConfigureViewModel())
          ("Set Output", FileListViewModel()) ]

    let list =
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

    member val Steps = ObservableCollection(list)

    member val CurrentStepIndex = 0 with get, set

    member val NumberSteps = steps.Length with get, set
    member val Enabled = true with get, set

    member this.GetCurrentStep() = this.Steps[this.CurrentStepIndex]

    member this.NextStep() = this.SetStep(this.CurrentStepIndex + 1)

    member this.PrevStep() = this.SetStep(this.CurrentStepIndex - 1)

    member this.SetStepsEnabled(enabled: bool) =
        this.Enabled <- enabled
        this.RaisePropertyChanged("Enabled")
        
        let oldSteps: Step [] =
            Array.zeroCreate this.Steps.Count

        this.Steps.CopyTo(oldSteps, 0)

        oldSteps
        |> Seq.indexed
        |> Seq.iter (fun (index, step) ->
            step.Enabled <- enabled
            this.Steps.RemoveAt(index)
            this.Steps.Insert(index, step))

    member this.SetStep(newStepIndex: int) =
        if newStepIndex >= 0
           && newStepIndex < this.NumberSteps then
            // This is *really* inefficient but I'm kinda ??? on how to do this properly...

            let oldSteps: Step [] =
                Array.zeroCreate this.Steps.Count

            this.Steps.CopyTo(oldSteps, 0)

            oldSteps
            |> Seq.indexed
            |> Seq.iter (fun (index, step) ->
                if index < newStepIndex then
                    step.ProgressState <- StepProgress.Completed
                else if index = newStepIndex then
                    step.ProgressState <- StepProgress.InProgress
                else
                    step.ProgressState <- StepProgress.Upcoming

                this.Steps.RemoveAt(index)
                this.Steps.Insert(index, step))

            this.CurrentStepIndex <- newStepIndex

    member this.SetStepCommand(stepIndex: string) =
        match System.Int32.TryParse stepIndex with
        | true, index -> this.SetStep(index)
        | _ -> ()
