using System.Collections.Generic;
using System.IO;
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
            byte[] secondChunk = Encoding.UTF8.GetBytes("data: \"text\":\"xin chào\"}\r\n\r\nevent: done\r\ndata: {\"status\":\"completed\"}\r\n\r\n");

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

        [TestCase("\n\n")]
        [TestCase("\r\n\r\n")]
        [TestCase("\r\r")]
        public void EventOnlyFrameDoesNotLeakEventTypeToNextDataFrame(string frameSeparator)
        {
            var parser = new ChatbotSseParser();
            var events = new List<ChatbotSseEvent>();
            parser.EventReceived += events.Add;

            byte[] bytes = Encoding.UTF8.GetBytes("event: done" + frameSeparator + "data: payload" + frameSeparator);
            foreach (byte value in bytes)
                parser.Feed(new[] { value }, 1);

            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].eventName, Is.EqualTo("message"));
            Assert.That(events[0].data, Is.EqualTo("payload"));
        }

        [Test]
        public void CompleteDropsFinalFrameWithoutTrailingBlankLine()
        {
            var parser = new ChatbotSseParser();
            var events = new List<ChatbotSseEvent>();
            parser.EventReceived += events.Add;
            byte[] chunk = Encoding.UTF8.GetBytes("event: error\ndata: {\"code\":\"generation_failed\"}");

            parser.Feed(chunk, chunk.Length);
            parser.Complete();

            Assert.That(events, Is.Empty);
        }

        [Test]
        public void FeedAcceptsSingleChunkLargerThanDecoderBuffer()
        {
            var parser = new ChatbotSseParser();
            ChatbotSseEvent received = null;
            parser.EventReceived += value => received = value;
            byte[] chunk = Encoding.UTF8.GetBytes("data: " + new string('x', 5000) + "\n\n");

            parser.Feed(chunk, chunk.Length);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.data, Is.EqualTo(new string('x', 5000)));
        }

        [Test]
        public void FeedPreservesEmptyDataFieldInMultilineEvent()
        {
            var parser = new ChatbotSseParser();
            ChatbotSseEvent received = null;
            parser.EventReceived += value => received = value;
            byte[] chunk = Encoding.UTF8.GetBytes("data:\ndata: x\n\n");

            parser.Feed(chunk, chunk.Length);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.data, Is.EqualTo("\nx"));
        }

        [Test]
        public void FeedRejectsLineExceedingParserLimit()
        {
            var parser = new ChatbotSseParser();
            byte[] chunk = Encoding.UTF8.GetBytes(new string('x', 65537));

            Assert.Throws<InvalidDataException>(() => parser.Feed(chunk, chunk.Length));
        }

        [Test]
        public void FeedRejectsEventExceedingParserLimit()
        {
            var parser = new ChatbotSseParser();
            byte[] chunk = Encoding.UTF8.GetBytes("data: " + new string('x', 262145) + "\n\n");

            Assert.Throws<InvalidDataException>(() => parser.Feed(chunk, chunk.Length));
        }

        [Test]
        public void FeedRejectsInvalidUtf8()
        {
            var parser = new ChatbotSseParser();
            byte[] chunk = { 0x64, 0x61, 0x74, 0x61, 0x3A, 0x20, 0xC3, 0x28, 0x0A, 0x0A };

            Assert.Throws<DecoderFallbackException>(() => parser.Feed(chunk, chunk.Length));
        }

        [Test]
        public void FeedHandlesBomCrOnlyAndUtf8SplitAcrossChunks()
        {
            var parser = new ChatbotSseParser();
            ChatbotSseEvent received = null;
            parser.EventReceived += value => received = value;
            byte[] chunk = Encoding.UTF8.GetBytes("\uFEFFevent: delta\rdata: xin chào 😊\r\r");

            foreach (byte value in chunk)
                parser.Feed(new[] { value }, 1);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.eventName, Is.EqualTo("delta"));
            Assert.That(received.data, Is.EqualTo("xin chào 😊"));
        }
    }
}
