using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace HABIMON.Chatbot.Tests
{
    public class ChatbotSseParserTests
    {
        [Test]
        public void FeedCombinesSplitCrLfFramesAndMultiLineData()
        {
            var parser = new ChatbotSseParser();
            var events = new List<ChatbotSseEvent>();
            parser.EventReceived += events.Add;

            byte[] firstChunk = Encoding.UTF8.GetBytes("event: delta\r\ndata: {\"seq\":1,\r\n");
            byte[] secondChunk = Encoding.UTF8.GetBytes("data: \"text\":\"xin chào\"}\r\n\r\nevent: done\r\ndata: {\"status\":\"completed\"}");

            parser.Feed(firstChunk, firstChunk.Length);
            Assert.That(events, Is.Empty);

            parser.Feed(secondChunk, secondChunk.Length);
            parser.Complete();

            Assert.That(events, Has.Count.EqualTo(2));
            Assert.That(events[0].eventName, Is.EqualTo("delta"));
            Assert.That(events[0].data, Is.EqualTo("{\"seq\":1,\n\"text\":\"xin chào\"}"));
            Assert.That(events[1].eventName, Is.EqualTo("done"));
            Assert.That(events[1].data, Is.EqualTo("{\"status\":\"completed\"}"));
        }

        [Test]
        public void CompleteFlushesFinalFrameWithoutTrailingBlankLine()
        {
            var parser = new ChatbotSseParser();
            var events = new List<ChatbotSseEvent>();
            parser.EventReceived += events.Add;
            byte[] chunk = Encoding.UTF8.GetBytes("event: error\ndata: {\"code\":\"generation_failed\"}");

            parser.Feed(chunk, chunk.Length);
            parser.Complete();

            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].eventName, Is.EqualTo("error"));
        }
    }
}
