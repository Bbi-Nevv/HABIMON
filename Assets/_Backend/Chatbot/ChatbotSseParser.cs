using System;
using System.Text;

namespace HABIMON.Chatbot
{
    public class ChatbotSseParser
    {
        private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
        private readonly StringBuilder bufferedText = new StringBuilder();
        private readonly StringBuilder data = new StringBuilder();
        private readonly char[] decodedChars = new char[4096];
        private string eventName;

        public event Action<ChatbotSseEvent> EventReceived;

        public void Feed(byte[] bytes, int count)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (count < 0 || count > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            int charsRead = decoder.GetChars(bytes, 0, count, decodedChars, 0, false);
            bufferedText.Append(decodedChars, 0, charsRead);
            ProcessCompleteLines(false);
        }

        public void Complete()
        {
            int charsRead = decoder.GetChars(Array.Empty<byte>(), 0, 0, decodedChars, 0, true);
            bufferedText.Append(decodedChars, 0, charsRead);
            ProcessCompleteLines(true);

            if (bufferedText.Length > 0)
            {
                ProcessLine(bufferedText.ToString());
                bufferedText.Clear();
            }

            DispatchEvent();
        }

        private void ProcessCompleteLines(bool flush)
        {
            while (bufferedText.Length > 0)
            {
                int lineEnd = -1;
                int terminatorLength = 0;

                for (int i = 0; i < bufferedText.Length; i++)
                {
                    char character = bufferedText[i];
                    if (character == '\n')
                    {
                        lineEnd = i;
                        terminatorLength = 1;
                        break;
                    }

                    if (character == '\r')
                    {
                        if (i + 1 == bufferedText.Length && !flush)
                            return;

                        lineEnd = i;
                        terminatorLength = i + 1 < bufferedText.Length && bufferedText[i + 1] == '\n' ? 2 : 1;
                        break;
                    }
                }

                if (lineEnd < 0)
                    return;

                string line = bufferedText.ToString(0, lineEnd);
                bufferedText.Remove(0, lineEnd + terminatorLength);
                ProcessLine(line);
            }
        }

        private void ProcessLine(string line)
        {
            if (line.Length == 0)
            {
                DispatchEvent();
                return;
            }

            if (line[0] == ':')
                return;

            int separator = line.IndexOf(':');
            string field = separator < 0 ? line : line.Substring(0, separator);
            string value = separator < 0 ? string.Empty : line.Substring(separator + 1);
            if (value.StartsWith(" ", StringComparison.Ordinal))
                value = value.Substring(1);

            if (field == "event")
            {
                eventName = value;
            }
            else if (field == "data")
            {
                if (data.Length > 0)
                    data.Append('\n');
                data.Append(value);
            }
        }

        private void DispatchEvent()
        {
            if (eventName == null && data.Length == 0)
                return;

            EventReceived?.Invoke(new ChatbotSseEvent
            {
                eventName = string.IsNullOrEmpty(eventName) ? "message" : eventName,
                data = data.ToString()
            });
            eventName = null;
            data.Clear();
        }
    }
}
