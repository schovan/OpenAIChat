using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIPChatApp.Services
{
    public enum NimDeltaKind
    {
        Thinking,
        Final
    }

    public class NimDeltaEventArgs : EventArgs
    {
        public NimDeltaKind Kind { get; }
        public string Text { get; }
        public DateTime TimestampUtc { get; }
        public long NanosecondOfSecond { get; }

        public NimDeltaEventArgs(NimDeltaKind kind, string text, DateTime timestampUtc, long nanosecondOfSecond)
        {
            Kind = kind;
            Text = text;
            TimestampUtc = timestampUtc;
            NanosecondOfSecond = nanosecondOfSecond;
        }
    }

    public interface INimChatService
    {
        event EventHandler<NimDeltaEventArgs> DeltaReceived;
        Task<string> StreamAsync(IReadOnlyList<(string Role, string Content)> history, CancellationToken ct = default);
    }

    public class NimChatService : INimChatService
    {
        private const string ModelName = "nvidia/nemotron-3-super-120b-a12b";
        private const string Endpoint = "https://integrate.api.nvidia.com/v1/chat/completions";

        private readonly HttpClient _http;

        // Anchor a wall-clock instant to a Stopwatch tick so we can produce
        // sub-100ns-resolution wall-clock timestamps for each chunk.
        private static readonly DateTime _anchorUtc;
        private static readonly long _anchorTicks;
        private static readonly double _nsPerStopwatchTick;

        static NimChatService()
        {
            _anchorUtc = DateTime.UtcNow;
            _anchorTicks = Stopwatch.GetTimestamp();
            _nsPerStopwatchTick = 1_000_000_000.0 / Stopwatch.Frequency;
        }

        private static (DateTime Utc, long NanoOfSecond) Now()
        {
            var elapsedNs = (long)((Stopwatch.GetTimestamp() - _anchorTicks) * _nsPerStopwatchTick);
            var utc = _anchorUtc.AddTicks(elapsedNs / 100);
            // remainder of the current second, in nanoseconds
            var nanoOfSecond = (utc.Ticks % TimeSpan.TicksPerSecond) * 100 + (elapsedNs % 100);
            return (utc, nanoOfSecond);
        }

        public event EventHandler<NimDeltaEventArgs>? DeltaReceived;

        public NimChatService(IApiKeyService apiKeyService)
        {
            _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKeyService.GetKey());
            _http.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");
        }

        public Task<string> StreamAsync(IReadOnlyList<(string Role, string Content)> history, CancellationToken ct = default)
        {
            return Task.Run(async () =>
            {
                var payload = new
                {
                    model = ModelName,
                    messages = history.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                    temperature = 1,
                    top_p = 0.95,
                    max_tokens = 16384,
                    stream = true,
                    chat_template_kwargs = new { enable_thinking = true },
                    reasoning_budget = 16384
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };

                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

                var assistant = new StringBuilder();
                var buffer = new byte[8192];
                var lineBuffer = new StringBuilder();
                DateTime lineUtc = default;
                long lineNs = 0;
                bool lineStamped = false;

                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    // Timestamp the *exact* moment bytes came off the wire.
                    var (utc, ns) = Now();

                    for (int i = 0; i < read; i++)
                    {
                        char c = (char)buffer[i];

                        // First byte that belongs to the current line wins the timestamp.
                        if (!lineStamped)
                        {
                            lineUtc = utc;
                            lineNs = ns;
                            lineStamped = true;
                        }

                        if (c == '\n')
                        {
                            var line = lineBuffer.ToString().TrimEnd('\r');
                            lineBuffer.Clear();
                            var stampedAt = (lineUtc, lineNs);
                            lineStamped = false;

                            if (string.IsNullOrEmpty(line) || !line.StartsWith("data:"))
                            {
                                continue;
                            }

                            var data = line.Substring(5).Trim();
                            if (data == "[DONE]")
                            {
                                return assistant.ToString();
                            }

                            JsonDocument doc;
                            try { doc = JsonDocument.Parse(data); }
                            catch { continue; }

                            using (doc)
                            {
                                if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                                {
                                    continue;
                                }

                                var delta = choices[0].GetProperty("delta");

                                if (delta.TryGetProperty("reasoning_content", out var reasoning) && reasoning.ValueKind == JsonValueKind.String)
                                {
                                    var text = reasoning.GetString();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        DeltaReceived?.Invoke(this, new NimDeltaEventArgs(NimDeltaKind.Thinking, text, stampedAt.lineUtc, stampedAt.lineNs));
                                    }
                                }

                                if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                                {
                                    var text = content.GetString();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        assistant.Append(text);
                                        DeltaReceived?.Invoke(this, new NimDeltaEventArgs(NimDeltaKind.Final, text, stampedAt.lineUtc, stampedAt.lineNs));
                                    }
                                }
                            }
                        }
                        else
                        {
                            lineBuffer.Append(c);
                        }
                    }
                }

                return assistant.ToString();
            }, ct);
        }
    }
}
