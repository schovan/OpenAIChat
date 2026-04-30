using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using AIPChatApp.Services;

namespace AIPChatApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly INimChatService _chat;
        private readonly List<(string Role, string Content)> _history = new();

        private MessageViewModel? _thinkingMessage;
        private MessageViewModel? _finalMessage;

        [ObservableProperty]
        private ObservableCollection<MessageViewModel> _messages = new ObservableCollection<MessageViewModel>();

        [ObservableProperty]
        private string _userInput;

        [ObservableProperty]
        private bool _isLoading;

        public MainViewModel(INimChatService chat)
        {
            _chat = chat;
            _chat.DeltaReceived += OnDeltaReceived;
            Write("Hello! I am your AI assistant. How can I help you today?");
        }

        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(UserInput) || IsLoading)
            {
                return;
            }

            var prompt = UserInput;
            Messages.Add(new MessageViewModel { Content = prompt, IsUser = true });
            UserInput = string.Empty;
            IsLoading = true;

            _history.Add(("user", prompt));

            _thinkingMessage = CreateAndWrite("=== [THINKING PROCESS] ===");
            _finalMessage = null;

            try
            {
                var assistantText = await _chat.StreamAsync(_history);
                if (!string.IsNullOrEmpty(assistantText))
                {
                    _history.Add(("assistant", assistantText));
                }
            }
            catch (Exception ex)
            {
                Write($"Error:\n\n{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnDeltaReceived(object? sender, NimDeltaEventArgs e)
        {
            var local = e.TimestampUtc.ToLocalTime();
            var ms = e.NanosecondOfSecond / 1_000_000;
            var stamp = $"[{local:HH:mm:ss}.{ms:D3}] ";

            // Marshal to UI thread; use BeginInvoke so the network loop never blocks on rendering.
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.Kind == NimDeltaKind.Thinking)
                {
                    if (_thinkingMessage != null)
                    {
                        _thinkingMessage.Content += stamp + e.Text;
                    }
                }
                else
                {
                    _finalMessage ??= CreateAndWrite("=== [FINAL RESPONSE] ===");
                    _finalMessage.Content += stamp + e.Text;
                }
            }));
        }

        private MessageViewModel CreateAndWrite(string content)
        {
            var message = new MessageViewModel { Content = $"{content}\n\n", IsUser = false };
            Messages.Add(message);
            return message;
        }

        private void Write(string content)
        {
            Messages.Add(new MessageViewModel { Content = content, IsUser = false });
        }
    }
}
