using System;
using System.Reflection;
using HABIMON.Core;
using NUnit.Framework;

namespace HABIMON.Core.Tests
{
    public class FirebaseWriteScopeTests
    {
        [TestCase("")]
        [TestCase(null)]
        public void PendingHabitCreateAllowsAnOptionalDescriptionAndKeepsItOnRetry(string description)
        {
            var pending = new PendingHabitCreate(new FirebaseWriteScope("parent", "child", 1),
                "operation", "Read", description, "20:00", "icon_book", "2026-10-25");
            Assert.That(pending.Description, Is.Empty);
            var retry = pending.ResumeInSameTarget(new FirebaseWriteScope("parent", "child", 2));
            Assert.That(retry.Description, Is.Empty);
            Assert.That(retry.OperationId, Is.EqualTo("operation"));
        }

        [Test]
        public void PendingHabitCreateRetainsOriginalScopeAndPayloadForRetry()
        {
            var scope = new FirebaseWriteScope("parent-a", "child-a", 7);
            var pending = new PendingHabitCreate(
                scope, "operation-a", "Study", "Read a book", "Buổi Tối", "moon", "2026-10-24");

            Assert.That(pending.Scope, Is.SameAs(scope));
            Assert.That(pending.OperationId, Is.EqualTo("operation-a"));
            Assert.That(pending.Title, Is.EqualTo("Study"));
            Assert.That(pending.Description, Is.EqualTo("Read a book"));
            Assert.That(pending.TimeTarget, Is.EqualTo("Buổi Tối"));
            Assert.That(pending.IconKey, Is.EqualTo("moon"));
            Assert.That(pending.EffectiveFrom, Is.EqualTo("2026-10-24"));
            Assert.That(pending.IsCurrent("parent-a", "child-a", 7), Is.True);
            Assert.That(pending.IsCurrent("parent-a", "child-a", 9), Is.False);
            Assert.That(pending.IsCurrent("parent-b", "child-b", 10), Is.False);
        }

        [Test]
        public void PendingChildCreateRetainsOriginalOwnerAndPayloadForRetry()
        {
            var scope = new FirebaseWriteScope("parent-a", string.Empty, 7);
            var pending = new PendingChildCreate(scope, "operation-a", "Linh", 8, 0);

            Assert.That(pending.Scope, Is.SameAs(scope));
            Assert.That(pending.OperationId, Is.EqualTo("operation-a"));
            Assert.That(pending.Name, Is.EqualTo("Linh"));
            Assert.That(pending.Age, Is.EqualTo(8));
            Assert.That(pending.Gender, Is.EqualTo(0));
            Assert.That(pending.IsCurrent("parent-a", 7), Is.True);
            Assert.That(pending.IsCurrent("parent-b", 8), Is.False);
            Assert.That(pending.IsCurrent("parent-a", 9), Is.False);
        }

        [Test]
        public void PendingCreateCanResumeOnlyInTheSameTargetWithItsOriginalPayload()
        {
            var habitScope = new FirebaseWriteScope("parent-a", "child-a", 7);
            var pendingHabit = new PendingHabitCreate(
                habitScope, "operation-a", "Study", "Read a book", "Buổi Tối", "moon", "2026-10-24");
            MethodInfo resumeHabit = typeof(PendingHabitCreate).GetMethod("ResumeInSameTarget");
            Assert.That(resumeHabit, Is.Not.Null,
                "An explicit retry after returning to the original target must retain its first payload.");
            if (resumeHabit == null)
                return;

            var returnedHabitScope = new FirebaseWriteScope("parent-a", "child-a", 9);
            var resumedHabit = (PendingHabitCreate)resumeHabit.Invoke(pendingHabit, new object[] { returnedHabitScope });
            Assert.That(resumedHabit.Scope, Is.SameAs(returnedHabitScope));
            Assert.That(resumedHabit.OperationId, Is.EqualTo("operation-a"));
            Assert.That(resumedHabit.Title, Is.EqualTo("Study"));
            Assert.That(resumedHabit.Description, Is.EqualTo("Read a book"));
            Assert.That(resumedHabit.EffectiveFrom, Is.EqualTo("2026-10-24"));
            Assert.Throws<TargetInvocationException>(() => resumeHabit.Invoke(pendingHabit, new object[]
            {
                new FirebaseWriteScope("parent-a", "child-b", 9)
            }));

        }

        [Test]
        public void PendingChildCreateIsBoundToParentAndGenerationNotSelectedChild()
        {
            var scope = new FirebaseWriteScope("parent-a", string.Empty, 7);
            var pending = new PendingChildCreate(scope, "operation-a", "Linh", 8, 0);

            Assert.That(pending.IsCurrent("parent-a", 7), Is.True);
            Assert.That(pending.IsCurrent("parent-b", 7), Is.False);
            Assert.That(pending.IsCurrent("parent-a", 9), Is.False);

            var sameParentNewGeneration = new FirebaseWriteScope("parent-a", "child-two", 9);
            var resumed = pending.ResumeInSameParent(sameParentNewGeneration);
            Assert.That(resumed.Scope, Is.SameAs(sameParentNewGeneration));
            Assert.That(resumed.OperationId, Is.EqualTo("operation-a"));
            Assert.That(resumed.Name, Is.EqualTo("Linh"));
            Assert.Throws<InvalidOperationException>(() => pending.ResumeInSameParent(
                new FirebaseWriteScope("parent-b", "child-one", 9)));
        }

        [Test]
        public void CapturedScopeCannotBeReusedAfterAccountOrChildSwitchAndReturn()
        {
            Type scopeType = typeof(HabitSchedule).Assembly.GetType("HABIMON.Core.FirebaseWriteScope");
            Assert.That(scopeType, Is.Not.Null,
                "Pending Firebase creates must retain the scope captured before the first write.");
            if (scopeType == null)
                return;

            object scope = Activator.CreateInstance(scopeType, "parent-a", "child-a", 7);
            MethodInfo isCurrent = scopeType.GetMethod(
                "IsCurrent", new[] { typeof(string), typeof(string), typeof(int) });
            Assert.That(isCurrent, Is.Not.Null);
            if (isCurrent == null)
                return;

            Assert.That(isCurrent.Invoke(scope, new object[] { "parent-a", "child-a", 7 }), Is.True);
            Assert.That(isCurrent.Invoke(scope, new object[] { "parent-b", "child-b", 8 }), Is.False);
            Assert.That(isCurrent.Invoke(scope, new object[] { "parent-a", "child-b", 8 }), Is.False);
            Assert.That(isCurrent.Invoke(scope, new object[] { "parent-a", "child-a", 9 }), Is.False);
        }
    }
}
