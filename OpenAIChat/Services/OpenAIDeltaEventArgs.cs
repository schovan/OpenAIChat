namespace OpenAIChat.Services
{
    public class OpenAIDeltaEventArgs : EventArgs
    {
        public OpenAIDeltaKind Kind { get; }
        public string Text { get; }
        public DateTime TimestampUtc { get; }

        public OpenAIDeltaEventArgs(OpenAIDeltaKind kind, string text, DateTime timestampUtc)
        {
            Kind = kind;
            Text = text;
            TimestampUtc = timestampUtc;
        }
    }
}
