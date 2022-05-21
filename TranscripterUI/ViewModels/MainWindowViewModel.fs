namespace TranscripterUI.ViewModels

open System.Runtime.Serialization

[<DataContract>]
type MainWindowViewModel() =
    inherit ViewModelBase()
 
    member val CurrentSteps = CurrentStepViewModel([
        "Select Files";
        "Configuration"
        "Go!"
    ]) with get
    
    member val SelectFilesVM = SelectFilesViewModel()