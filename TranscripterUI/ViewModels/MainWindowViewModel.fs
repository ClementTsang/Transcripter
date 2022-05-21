namespace TranscripterUI.ViewModels

open System.Runtime.Serialization

[<DataContract>]
type MainWindowViewModel() =
    inherit ViewModelBase()
 
    member val CurrentStepTracking = CurrentStepViewModel([
        ("Select Files", SelectFilesViewModel());
        ("Configuration", ViewModelBase());
        ("Go!", ViewModelBase())
    ]) with get
    