using NUnit.Framework;

namespace HABIMON.Chatbot.Tests
{
    public class ChatbotSessionScopeTests
    {
        [Test]
        public void IsCurrentRequiresSameParentChildAndGeneration()
        {
            var scope = new ChatbotSessionScope("parent-a", "child-a", 7);

            Assert.That(scope.IsCurrent("parent-a", "child-a", 7), Is.True);
            Assert.That(scope.IsCurrent("parent-b", "child-a", 7), Is.False);
            Assert.That(scope.IsCurrent("parent-a", "child-b", 7), Is.False);
            Assert.That(scope.IsCurrent("parent-a", "child-a", 8), Is.False);
        }

        [TestCase(null, "child-a")]
        [TestCase(" ", "child-a")]
        [TestCase("parent-a", null)]
        [TestCase("parent-a", " ")]
        public void InvalidScopeNeverMatches(string parentId, string childId)
        {
            var scope = new ChatbotSessionScope(parentId, childId, 7);

            Assert.That(scope.IsValid, Is.False);
            Assert.That(scope.IsCurrent(parentId, childId, 7), Is.False);
        }
    }
}
