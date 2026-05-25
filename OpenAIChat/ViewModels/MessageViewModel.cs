namespace OpenAIChat.ViewModels
{
    public class MessageViewModel : ViewModelBase
    {
        private string? _content;
        public string? Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        private bool _isUser;
        public bool IsUser
        {
            get => _isUser;
            set => SetProperty(ref _isUser, value);
        }

        private bool _isSystem;
        public bool IsSystem
        {
            get => _isSystem;
            set => SetProperty(ref _isSystem, value);
        }

        private string? _header;
        public string? Header
        {
            get => _header;
            set => SetProperty(ref _header, value);
        }
    }
}
