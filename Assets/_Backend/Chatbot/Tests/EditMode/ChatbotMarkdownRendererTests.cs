using NUnit.Framework;

namespace HABIMON.Chatbot.Tests
{
    public class ChatbotMarkdownRendererTests
    {
        [Test]
        public void RenderFormatsEmphasisAndKeepsVietnameseText()
        {
            string rendered = ChatbotMarkdownRenderer.RenderToTmp("**Thói quen** của bé *mỗi ngày*", true);

            Assert.That(rendered, Is.EqualTo("<b>Thói quen</b> của bé <i>mỗi ngày</i>"));
        }

        [Test]
        public void RenderNormalizesDecomposedVietnameseAccents()
        {
            Assert.That(ChatbotMarkdownRenderer.RenderToTmp("Tha\u0301ng e\u0302\u0301", true),
                Is.EqualTo("Tháng ế"));
        }

        [Test]
        public void StreamingHidesUnmatchedEmphasisUntilClosingDelimiterArrives()
        {
            Assert.That(ChatbotMarkdownRenderer.RenderToTmp("Hãy thử **một bước nhỏ", false),
                Is.EqualTo("Hãy thử một bước nhỏ"));
            Assert.That(ChatbotMarkdownRenderer.RenderToTmp("Hãy thử **một bước nhỏ**", false),
                Is.EqualTo("Hãy thử <b>một bước nhỏ</b>"));
        }

        [Test]
        public void FinalMalformedEmphasisIsPreservedAsLiteralText()
        {
            Assert.That(ChatbotMarkdownRenderer.RenderToTmp("**chưa đóng", true), Is.EqualTo("**chưa đóng"));
        }

        [Test]
        public void StreamingHidesIncompleteInlineCodeMarkerAndFormatsWhenClosed()
        {
            Assert.That(ChatbotMarkdownRenderer.RenderToTmp("Nhấn `Gửi", false), Is.EqualTo("Nhấn Gửi"));
            Assert.That(ChatbotMarkdownRenderer.RenderToTmp("Nhấn `Gửi`", false),
                Is.EqualTo("Nhấn <mark=#EAF2F6>Gửi</mark>"));
        }

        [Test]
        public void StreamingHidesIncompleteLinksAndListMarkers()
        {
            Assert.That(ChatbotMarkdownRenderer.RenderToTmp("Xem [Habi](https://example.com", false),
                Is.EqualTo("Xem Habi"));
            Assert.That(ChatbotMarkdownRenderer.RenderToTmp("Xem [Habi", false), Is.EqualTo("Xem Habi"));
            Assert.That(ChatbotMarkdownRenderer.RenderToTmp("Các bước:\n- ", false), Is.EqualTo("Các bước:"));
        }

        [Test]
        public void RenderSupportsHeadingsListsAndTablesAsReadableText()
        {
            string rendered = ChatbotMarkdownRenderer.RenderToTmp(
                "## Bắt đầu\n\n- Bước một\n- Bước hai\n\n| Ngày | Việc |\n| --- | --- |\n| Mai | Đọc sách |", true);

            Assert.That(rendered, Does.Contain("<size=135%><b>Bắt đầu</b></size>"));
            Assert.That(rendered, Does.Contain("• Bước một\n• Bước hai"));
            Assert.That(rendered, Does.Contain("Ngày  |  Việc"));
            Assert.That(rendered, Does.Contain("Mai  |  Đọc sách"));
        }

        [Test]
        public void RawHtmlCannotBecomeTmpRichText()
        {
            string rendered = ChatbotMarkdownRenderer.RenderToTmp("<color=#ff0000>văn bản</color>", true);

            Assert.That(rendered, Is.EqualTo("&lt;color=#ff0000&gt;văn bản&lt;/color&gt;"));
        }

        [Test]
        public void UnsupportedEmojiUseReadableDisplayFallbacks()
        {
            Assert.That(ChatbotMarkdownRenderer.RenderToTmp("🎉 🎵", true), Is.EqualTo("Chúc mừng! ♪"));
        }

        [Test]
        public void CodeBlocksEscapeTmpTagsAndKeepCodeReadable()
        {
            string rendered = ChatbotMarkdownRenderer.RenderToTmp("```\n<color=red>abc</color>\n```", true);

            Assert.That(rendered, Does.Contain("<mark=#F1F5F8>"));
            Assert.That(rendered, Does.Contain("&lt;color=red&gt;abc&lt;/color&gt;"));
            Assert.That(rendered, Does.Not.Contain("<color=red>"));
        }
    }
}
