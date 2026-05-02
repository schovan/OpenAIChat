namespace OpenAIChat.Services
{
    public interface IOpenAIChatService
    {
        event EventHandler<OpenAIDeltaEventArgs> DeltaReceived;
        Task<string> StreamAsync(IReadOnlyList<(string Role, string Content)> history, CancellationToken ct = default);
    }
}
