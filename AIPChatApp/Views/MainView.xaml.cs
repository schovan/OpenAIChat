using System.Windows;
using AIPChatApp.Services;
using AIPChatApp.ViewModels;

namespace AIPChatApp.Views
{
    public partial class MainView : Window
    {
        public MainView()
        {
            InitializeComponent();
            AppServiceLocator.Initialize();
            this.DataContext = new MainViewModel(AppServiceLocator.ChatService);
        }
    }
}
