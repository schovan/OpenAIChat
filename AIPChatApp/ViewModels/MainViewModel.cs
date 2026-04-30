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
        private bool _thinkingStamped;

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
            Messages.Add(new MessageViewModel { Content = "Welcome to NVIDIA NIM Chat", IsUser = false, IsWelcome = true });
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

            _thinkingMessage = CreateAndWrite("Waiting for response…");
            _finalMessage = null;
            _thinkingStamped = false;

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
            var stamp = $" [{local:HH:mm:ss}.{ms:D3}]";

            // Marshal to UI thread; use BeginInvoke so the network loop never blocks on rendering.
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.Kind == NimDeltaKind.Thinking)
                {
                    if (_thinkingMessage != null)
                    {
                        if (!_thinkingStamped)
                        {
                            _thinkingMessage.Content = $"=== [THINKING PROCESS]{stamp} ===\n\n";
                            _thinkingStamped = true;
                        }
                        _thinkingMessage.Content += e.Text;
                    }
                }
                else
                {
                    _finalMessage ??= CreateAndWrite($"=== [FINAL RESPONSE]{stamp} ===");
                    _finalMessage.Content += e.Text;
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
