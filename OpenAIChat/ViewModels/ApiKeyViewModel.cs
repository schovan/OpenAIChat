namespace OpenAIChat.ViewModels
{
    public class ApiKeyViewModel : ViewModelBase, IDialogViewModel<string>
    {
        private string? _result;
        public string? Result { get => _result; set => SetProperty(ref _result, value); }

        string IDialogViewModel<string>.Result => Result!;
    }
}
