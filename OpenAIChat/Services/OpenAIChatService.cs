using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OpenAIChat.Services
{
    public class OpenAIChatService : IOpenAIChatService
    {
        private const string ModelName = "zai-org/GLM-5.1-FP8";
        private const string Endpoint = "https://api.us-west-2.modal.direct/v1/chat/completions";

        private readonly HttpClient _http;

        public event EventHandler<OpenAIDeltaEventArgs>? DeltaReceived;

        public OpenAIChatService(IApiKeyService apiKeyService)
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
                var charBuffer = new char[8192];
                var decoder = Encoding.UTF8.GetDecoder();
                var lineBuffer = new StringBuilder();
                DateTime lineUtc = default;
                bool lineStamped = false;

                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    var utc = StopwatchClock.UtcNow();

                    int charCount = decoder.GetChars(buffer, 0, read, charBuffer, 0);

                    for (int i = 0; i < charCount; i++)
                    {
                        char c = charBuffer[i];

                        if (!lineStamped)
                        {
                            lineUtc = utc;
                            lineStamped = true;
                        }

                        if (c == '\n')
                        {
                            var line = lineBuffer.ToString().TrimEnd('\r');
                            lineBuffer.Clear();
                            var stampedAt = lineUtc;
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
                                        DeltaReceived?.Invoke(this, new OpenAIDeltaEventArgs(OpenAIDeltaKind.Thinking, text, stampedAt));
                                    }
                                }

                                if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                                {
                                    var text = content.GetString();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        assistant.Append(text);
                                        DeltaReceived?.Invoke(this, new OpenAIDeltaEventArgs(OpenAIDeltaKind.Final, text, stampedAt));
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
