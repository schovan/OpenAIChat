namespace OpenAIChat.Services
{
    public class NimDeltaEventArgs : EventArgs
    {
        public NimDeltaKind Kind { get; }
        public string Text { get; }
        public DateTime TimestampUtc { get; }

        public NimDeltaEventArgs(NimDeltaKind kind, string text, DateTime timestampUtc)
        {
            Kind = kind;
            Text = text;
            TimestampUtc = timestampUtc;
        }
    }
}
