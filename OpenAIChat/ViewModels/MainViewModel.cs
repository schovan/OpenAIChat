using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using OpenAIChat.Models;
using OpenAIChat.Services;
using OpenAIChat.Tools;

namespace OpenAIChat.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IOpenAIChatService _chat;
        private readonly List<(string Role, string Content)> _history = new();

        private MessageViewModel? _waitingMessage;
        private MessageViewModel? _thinkingMessage;
        private MessageViewModel? _finalMessage;

        private ObservableCollection<MessageViewModel> _messages = new();
        public ObservableCollection<MessageViewModel> Messages { get => _messages; set => SetProperty(ref _messages, value); }

        private string? _userInput;
        public string? UserInput { get => _userInput; set => SetProperty(ref _userInput, value); }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        public ICommand SendMessageCommand { get; }

        public MainViewModel(IOpenAIChatService chat)
        {
            _chat = chat;
            _chat.DeltaReceived += OnDeltaReceived;
            Messages.Add(new MessageViewModel { Content = "Welcome to OpenAI Chat", IsUser = false, IsSystem = true });
            SendMessageCommand = new RelayCommand(async () => await SendMessageAsync());
        }

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

        private void OnDeltaReceived(object? sender, OpenAIDeltaEventArgs e)
        {
            var local = e.TimestampUtc.ToLocalTime();

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_waitingMessage != null)
                {
                    Messages.Remove(_waitingMessage);
                    _waitingMessage = null;
                }

                if (e.Kind == OpenAIDeltaKind.Thinking)
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
    }
}
