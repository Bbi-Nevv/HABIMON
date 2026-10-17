using NUnit.Framework;

namespace HABIMON.Chatbot.Tests
{
    public class ChatbotApiContractTests
    {
        [Test]
        public void SessionRoutesNormalizeBaseUrlAndEscapeSessionId()
        {
            Assert.That(
                ChatbotApiRoutes.Sessions("https://chat.example/demo/"),
                Is.EqualTo("https://chat.example/demo/api/v1/sessions"));
            Assert.That(
                ChatbotApiRoutes.Session("https://chat.example/demo", "session-id"),
                Is.EqualTo("https://chat.example/demo/api/v1/sessions/session-id"));
            Assert.That(
                ChatbotApiRoutes.Messages("https://chat.example/demo", "session id"),
                Is.EqualTo("https://chat.example/demo/api/v1/sessions/session%20id/messages"));
            Assert.That(
                ChatbotApiRoutes.Sessions("https://chat.example/demo", "child id", 50, 0),
                Is.EqualTo("https://chat.example/demo/api/v1/sessions?child_id=child%20id&limit=50&offset=0"));
            Assert.That(
                ChatbotApiRoutes.Messages("https://chat.example/demo", "session-id", "child/one"),
                Is.EqualTo("https://chat.example/demo/api/v1/sessions/session-id/messages?child_id=child%2Fone"));
            Assert.That(
                ChatbotApiRoutes.Session("https://chat.example/demo", "session-id", "child-one"),
                Is.EqualTo("https://chat.example/demo/api/v1/sessions/session-id?child_id=child-one"));
        }

        [Test]
        public void RequestJsonPreservesVietnameseAndNestedContext()
        {
            var request = new ChatbotSendMessageRequest
            {
                message = "Xin chào \"An\"",
                plan = "free",
                childId = "child-one",
                context = new ChatbotContext
                {
                    capturedAt = "2026-10-17T14:00:00+07:00",
                    localDate = "2026-10-17",
                    timeZone = "Asia/Ho_Chi_Minh",
                    child = null,
                    pet = null,
                    habits = new ChatHabitContext[0],
                    rewards = new ChatRewardContext[0]
                }
            };

            string json = ChatbotRequestJson.Serialize(request);

            StringAssert.Contains("\"message\":\"Xin chào \\\"An\\\"\"", json);
            StringAssert.Contains("\"plan\":\"free\"", json);
            StringAssert.Contains("\"child_id\":\"child-one\"", json);
            StringAssert.Contains("\"context\":{", json);
            StringAssert.Contains("\"child\":null", json);
            StringAssert.Contains("\"pet\":null", json);
        }

        [Test]
        public void SessionRequestJsonSendsChildScopeSeparately()
        {
            Assert.That(
                ChatbotRequestJson.SerializeSession("child-one"),
                Is.EqualTo("{\"child_id\":\"child-one\"}"));
        }

        [Test]
        public void AuthorizationValueUsesBearerScheme()
        {
            Assert.That(
                ChatbotApiClient.AuthorizationHeaderValue("installation-token"),
                Is.EqualTo("Bearer installation-token"));
        }
    }
}
