namespace TranscripterUI.ViewModels

type MainWindowViewModel() =
    inherit ViewModelBase()
    
    member val CurrentSteps = CurrentStepViewModel([
        "Select Files";
        "Configuration"
        "Go!"
    ]) with get
    
    