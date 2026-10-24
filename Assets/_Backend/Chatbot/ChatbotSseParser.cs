using System;
using System.IO;
using System.Text;

namespace HABIMON.Chatbot
{
    public class ChatbotSseParser
    {
        private const int DefaultMaxLineCharacters = 64 * 1024;
        private const int DefaultMaxEventCharacters = 256 * 1024;

        private readonly Decoder decoder = new UTF8Encoding(false, true).GetDecoder();
        private readonly StringBuilder line = new StringBuilder();
        private readonly StringBuilder data = new StringBuilder();
        private readonly char[] decodedChars = new char[4096];
        private readonly int maxLineCharacters;
        private readonly int maxEventCharacters;
        private string eventName;
        private bool hasDataField;
        private bool skipNextLineFeed;
        private bool hasSeenCharacter;
        private bool isCompleted;

        public event Action<ChatbotSseEvent> EventReceived;

        public ChatbotSseParser(
            int maxLineCharacters = DefaultMaxLineCharacters,
            int maxEventCharacters = DefaultMaxEventCharacters)
        {
            if (maxLineCharacters < 1)
                throw new ArgumentOutOfRangeException(nameof(maxLineCharacters));
            if (maxEventCharacters < 1)
                throw new ArgumentOutOfRangeException(nameof(maxEventCharacters));

            this.maxLineCharacters = maxLineCharacters;
            this.maxEventCharacters = maxEventCharacters;
        }

        public void Feed(byte[] bytes, int count)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (count < 0 || count > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (isCompleted)
                throw new InvalidOperationException("The SSE stream is already complete.");

            int byteOffset = 0;
            while (byteOffset < count)
            {
                decoder.Convert(bytes, byteOffset, count - byteOffset, decodedChars, 0, decodedChars.Length, false,
                    out int bytesUsed, out int charsRead, out _);

                if (bytesUsed == 0 && charsRead == 0)
                    throw new InvalidDataException("The UTF-8 decoder made no progress.");

                byteOffset += bytesUsed;
                for (int index = 0; index < charsRead; index++)
                    ProcessCharacter(decodedChars[index]);
            }
        }

        public void Complete()
        {
            if (isCompleted)
                return;

            decoder.Convert(Array.Empty<byte>(), 0, 0, decodedChars, 0, decodedChars.Length, true,
                out _, out int charsRead, out _);
            for (int index = 0; index < charsRead; index++)
                ProcessCharacter(decodedChars[index]);

            // SSE dispatch requires a blank-line delimiter. An incomplete final line or event is discarded.
            line.Clear();
            data.Clear();
            eventName = null;
            hasDataField = false;
            isCompleted = true;
        }

        private void ProcessCharacter(char character)
        {
            if (skipNextLineFeed)
            {
                skipNextLineFeed = false;
                if (character == '\n')
                    return;
            }

            if (!hasSeenCharacter)
            {
                hasSeenCharacter = true;
                if (character == '\uFEFF')
                    return;
            }

            if (character == '\r')
            {
                ProcessLine(line.ToString());
                line.Clear();
                skipNextLineFeed = true;
                return;
            }

            if (character == '\n')
            {
                ProcessLine(line.ToString());
                line.Clear();
                return;
            }

            if (line.Length >= maxLineCharacters)
                throw new InvalidDataException("The SSE line exceeds the configured size limit.");

            line.Append(character);
        }

        private void ProcessLine(string value)
        {
            if (value.Length == 0)
            {
                DispatchEvent();
                return;
            }

            if (value[0] == ':')
                return;

            int separator = value.IndexOf(':');
            string field = separator < 0 ? value : value.Substring(0, separator);
            string fieldValue = separator < 0 ? string.Empty : value.Substring(separator + 1);
            if (fieldValue.StartsWith(" ", StringComparison.Ordinal))
                fieldValue = fieldValue.Substring(1);

            if (field == "event")
            {
                eventName = fieldValue;
                return;
            }

            if (field != "data")
                return;

            if (data.Length + fieldValue.Length + 1 > maxEventCharacters)
                throw new InvalidDataException("The SSE event exceeds the configured size limit.");

            data.Append(fieldValue);
            data.Append('\n');
            hasDataField = true;
        }

        private void DispatchEvent()
        {
            if (!hasDataField)
            {
                eventName = null;
                return;
            }

            int dataLength = data.Length - 1;
            string eventData = data.ToString(0, dataLength);
            string dispatchedEventName = string.IsNullOrEmpty(eventName) ? "message" : eventName;

            eventName = null;
            data.Clear();
            hasDataField = false;

            EventReceived?.Invoke(new ChatbotSseEvent
            {
                eventName = dispatchedEventName,
                data = eventData
            });
        }
    }
}
