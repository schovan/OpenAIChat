using CommunityToolkit.Mvvm.ComponentModel;

namespace AIPChatApp.ViewModels
{
    public partial class MessageViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _content;

        [ObservableProperty]
        private bool _isUser;

        [ObservableProperty]
        private bool _isWelcome;
    }
}
