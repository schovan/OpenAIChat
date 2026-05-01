using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using OpenAIChat.Services;

namespace OpenAIChat.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly INimChatService _chat;
        private readonly List<(string Role, string Content)> _history = new();

        private MessageViewModel? _waitingMessage;
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
            Messages.Add(new MessageViewModel { Content = "Welcome to OpenAI Chat", IsUser = false, IsSystem = true });
        }

        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(UserInput) || IsLoading)
            {
                return;
            }

            var prompt = UserInput;
            var now = StopwatchClock.LocalNow();
            Messages.Add(new MessageViewModel { Content = prompt, IsUser = true, Header = $"You · {StopwatchClock.FormatTimestamp(now)}" });
            UserInput = string.Empty;
            IsLoading = true;

            _history.Add(("user", prompt));

            _waitingMessage = new MessageViewModel { Content = "Waiting for response…", IsUser = false, IsSystem = true };
            Messages.Add(_waitingMessage);
            _thinkingMessage = null;
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
                if (_waitingMessage != null)
                {
                    Messages.Remove(_waitingMessage);
                    _waitingMessage = null;
                }
                CreateAiMessage($"Error · {StopwatchClock.FormatTimestamp(StopwatchClock.LocalNow())}").Content = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnDeltaReceived(object? sender, NimDeltaEventArgs e)
        {
            var local = e.TimestampUtc.ToLocalTime();

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_waitingMessage != null)
                {
                    Messages.Remove(_waitingMessage);
                    _waitingMessage = null;
                }

                if (e.Kind == NimDeltaKind.Thinking)
                {
                    if (_thinkingMessage == null)
                    {
                        _thinkingMessage = CreateAiMessage($"Thinking · {StopwatchClock.FormatTimestamp(local)}");
                    }
                    _thinkingMessage.Content += e.Text;
                }
                else
                {
                    _finalMessage ??= CreateAiMessage($"Response · {StopwatchClock.FormatTimestamp(local)}");
                    _finalMessage.Content += e.Text;
                }
            }));
        }

        private MessageViewModel CreateAiMessage(string header)
        {
            var message = new MessageViewModel { Header = header, Content = string.Empty, IsUser = false };
            Messages.Add(message);
            return message;
        }

        private void Write(string content)
        {
            Messages.Add(new MessageViewModel { Content = content, IsUser = false });
        }
    }
}
