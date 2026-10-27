using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HABIMON.Scene.PlayMode.Tests
{
    public class FirebaseProvisioningEmulatorTests
    {
        private const string DenyChildWritesRules = @"rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /users/{parentId} {
      allow create, read, update: if request.auth != null && request.auth.uid == parentId;
      match /{document=**} {
        allow read: if request.auth != null && request.auth.uid == parentId;
        allow write: if false;
      }
    }
  }
}";

        private const string DenyExistingChildPetWriteRules = @"rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /users/{parentId} {
      allow create, read, update: if request.auth != null && request.auth.uid == parentId;
      match /children/{childId} {
        allow read, write: if request.auth != null && request.auth.uid == parentId;
        match /habimon/{petId} {
          allow read: if request.auth != null && request.auth.uid == parentId;
          allow write: if request.auth != null && request.auth.uid == parentId &&
                       !(childId == 'existing-child' && petId == 'status');
        }
        match /habits/{habitId} {
          allow read, write: if request.auth != null && request.auth.uid == parentId;
        }
        match /rewards/{rewardId} {
          allow read, write: if request.auth != null && request.auth.uid == parentId;
        }
        match /habit_logs/{logId} {
          allow read, write: if request.auth != null && request.auth.uid == parentId;
        }
      }
    }
  }
}";

        [UnityTest, Explicit("Requires local Firebase Auth and Firestore emulators.")]
        public IEnumerator ExistingAuthUserIsProvisionedOnLogin()
        {
            if (Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST") != "127.0.0.1:9099" ||
                Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST") != "127.0.0.1:8080")
                Assert.Ignore("Run with the local Auth and Firestore emulators through the test script.");

            Task provision = ProvisionExistingAuthUserAsync();
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!provision.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(provision.IsCompleted, Is.True, "Provisioning test timed out.");
            if (provision.IsFaulted)
                throw provision.Exception;
            if (provision.IsCanceled)
                Assert.Fail("Provisioning test was canceled.");
        }

        private static async Task ProvisionExistingAuthUserAsync()
        {
            Type appType = FindFirebaseType("Firebase.FirebaseApp");
            MethodInfo checkDependencies = appType.GetMethod(
                "CheckAndFixDependenciesAsync",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            object dependencyTask = checkDependencies.Invoke(null, null);
            await (Task)dependencyTask;
            object dependencyStatus = dependencyTask.GetType().GetProperty("Result").GetValue(dependencyTask);
            Assert.That(dependencyStatus.ToString(), Is.EqualTo("Available"));

            Type authType = FindFirebaseType("Firebase.Auth.FirebaseAuth");
            object auth = authType.GetProperty("DefaultInstance", BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);
            authType.GetMethod("UseEmulator", new[] { typeof(string), typeof(int) })
                .Invoke(auth, new object[] { "127.0.0.1", 9099 });

            Type firestoreType = FindFirebaseType("Firebase.Firestore.FirebaseFirestore");
            object firestore = firestoreType.GetProperty("DefaultInstance", BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);
            object settings = firestoreType.GetProperty("Settings").GetValue(firestore);
            settings.GetType().GetProperty("Host").SetValue(settings, "127.0.0.1:8080");
            settings.GetType().GetProperty("SslEnabled").SetValue(settings, false);
            settings.GetType().GetProperty("PersistenceEnabled").SetValue(settings, false);

            string email = "f15-" + Guid.NewGuid().ToString("N") + "@example.test";
            const string password = "emulator-password-123";
            Type managerType = FindProjectType("FirebaseManager");
            var managerObject = new GameObject("FirebaseProvisioningEmulatorTest");
            Component manager = managerObject.AddComponent(managerType);
            try
            {
                object initializeTask = managerType.GetMethod("EnsureInitializedAsync").Invoke(manager, null);
                object initialized = await GetTaskResultAsync(initializeTask);
                Assert.That(initialized, Is.True);

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string ownerRules = File.ReadAllText(Path.Combine(projectRoot, "firestore.rules"));
                MethodInfo login = managerType.GetMethod("LoginParentAsync", new[] { typeof(string), typeof(string) });
                string parentId;
                try
                {
                    await SetFirestoreRulesAsync(DenyChildWritesRules);
                    LogAssert.Expect(LogType.Error, new Regex(".*\\[Firebase Auth Error\\].*"));
                    object registrationResult = await GetTaskResultAsync(managerType.GetMethod("RegisterParentAsync")
                        .Invoke(manager, new object[] { email, password }));
                    Assert.That((bool)registrationResult.GetType().GetField("Item1").GetValue(registrationResult), Is.False);
                    Assert.That((string)registrationResult.GetType().GetField("Item2").GetValue(registrationResult),
                        Does.Contain("đã được tạo"));
                    Assert.That(managerType.GetMethod("GetCurrentParentId").Invoke(manager, null), Is.Empty,
                        "A partially provisioned account should be signed out so the promised login recovery is actionable.");

                    await (Task)authType.GetMethod("SignInWithEmailAndPasswordAsync",
                        new[] { typeof(string), typeof(string) }).Invoke(auth, new object[] { email, password });
                    object signedInUser = authType.GetProperty("CurrentUser").GetValue(auth);
                    parentId = (string)signedInUser.GetType().GetProperty("UserId").GetValue(signedInUser);
                    authType.GetMethod("SignOut", Type.EmptyTypes).Invoke(auth, null);
                    Assert.That(await GetProvisioningStateAsync(parentId), Is.EqualTo("pending"));

                    LogAssert.Expect(LogType.Error, new Regex(".*\\[Firebase Profile\\].*"));
                    object failedLoginResult = await GetTaskResultAsync(login.Invoke(
                        manager, new object[] { email, password }));
                    Assert.That((bool)failedLoginResult.GetType().GetField("Item1").GetValue(failedLoginResult), Is.False);
                    Assert.That(managerType.GetMethod("GetCurrentParentId").Invoke(manager, null), Is.Empty,
                        "A login that cannot finish Firestore provisioning must not leave an authenticated parent session.");
                    Assert.That(await GetProvisioningStateAsync(parentId), Is.EqualTo("pending"));
                }
                finally
                {
                    await SetFirestoreRulesAsync(ownerRules);
                }

                await (Task)authType.GetMethod("SignInWithEmailAndPasswordAsync",
                    new[] { typeof(string), typeof(string) }).Invoke(auth, new object[] { email, password });
                string childOperationId = Guid.NewGuid().ToString("N");
                MethodInfo addChild = managerType.GetMethod("AddChildAsync",
                    new[] { FindProjectType("HABIMON.Core.PendingChildCreate") });
                object preexistingChildAdd = await GetTaskResultAsync(addChild.Invoke(manager,
                    new[] { CreatePendingChild(manager, managerType, childOperationId, "Provisioning retry child", 7, 0) }));
                Assert.That((bool)preexistingChildAdd.GetType().GetField("Item1").GetValue(preexistingChildAdd), Is.True,
                    (string)preexistingChildAdd.GetType().GetField("Item2").GetValue(preexistingChildAdd));

                await DeleteFirestoreDocumentAsync(firestoreType, firestore, parentId,
                    "children", childOperationId, "habimon", "status");
                try
                {
                    await SetFirestoreRulesAsync(
                        DenyExistingChildPetWriteRules.Replace("existing-child", childOperationId));
                    LogAssert.Expect(LogType.Error, new Regex(".*\\[Firebase Profile\\].*"));
                    object failedPetRecovery = await GetTaskResultAsync(login.Invoke(
                        manager, new object[] { email, password }));
                    Assert.That((bool)failedPetRecovery.GetType().GetField("Item1").GetValue(failedPetRecovery), Is.False);
                    Assert.That(managerType.GetMethod("GetCurrentParentId").Invoke(manager, null), Is.Empty,
                        "A failed repair of an existing child's pet must not leave the parent signed in.");
                    Assert.That(await GetProvisioningStateAsync(parentId), Is.EqualTo("pending"),
                        "The parent must remain pending when a later provisioning step fails.");
                }
                finally
                {
                    await SetFirestoreRulesAsync(ownerRules);
                }

                object loginResult = await GetTaskResultAsync(login.Invoke(manager, new object[] { email, password }));
                Type resultType = loginResult.GetType();
                Assert.That((bool)resultType.GetField("Item1").GetValue(loginResult), Is.True,
                    (string)resultType.GetField("Item2").GetValue(loginResult));

                string authenticatedParentId = (string)managerType.GetMethod("GetCurrentParentId").Invoke(manager, null);
                Assert.That(authenticatedParentId, Is.EqualTo(parentId));
                string childId = (string)managerType.GetMethod("GetCurrentChildId").Invoke(manager, null);
                Assert.That(childId, Is.EqualTo(childOperationId));
                Assert.That(await GetProvisioningStateAsync(parentId), Is.EqualTo("ready"));

                object children = await GetTaskResultAsync(managerType.GetMethod(
                    "GetChildrenAsync", new[] { typeof(string) }).Invoke(manager, new object[] { parentId }));
                Assert.That(((ICollection)children).Count, Is.EqualTo(2));

                object defaultHabits = await GetTaskResultAsync(managerType.GetMethod(
                    "GetHabitsAsync", new[] { typeof(string), typeof(string) })
                    .Invoke(manager, new object[] { parentId, "default" }));
                object defaultRewards = await GetTaskResultAsync(managerType.GetMethod(
                    "GetRewardsAsync", new[] { typeof(string), typeof(string) })
                    .Invoke(manager, new object[] { parentId, "default" }));
                Assert.That(((ICollection)defaultHabits).Count, Is.GreaterThan(0));
                Assert.That(((ICollection)defaultRewards).Count, Is.GreaterThan(0));

                await DeleteFirestoreDocumentAsync(firestoreType, firestore, parentId,
                    "children", "default", "habits", "habit_001");
                await DeleteFirestoreDocumentAsync(firestoreType, firestore, parentId,
                    "children", "default", "rewards", "reward_01");
                object retryAfterReady = await GetTaskResultAsync(login.Invoke(manager, new object[] { email, password }));
                Assert.That((bool)retryAfterReady.GetType().GetField("Item1").GetValue(retryAfterReady), Is.True);
                object habitsAfterReadyRetry = await GetTaskResultAsync(managerType.GetMethod(
                    "GetHabitsAsync", new[] { typeof(string), typeof(string) })
                    .Invoke(manager, new object[] { parentId, "default" }));
                object rewardsAfterReadyRetry = await GetTaskResultAsync(managerType.GetMethod(
                    "GetRewardsAsync", new[] { typeof(string), typeof(string) })
                    .Invoke(manager, new object[] { parentId, "default" }));
                Assert.That(((ICollection)habitsAfterReadyRetry).Count, Is.Zero,
                    "A ready profile must not recreate a seed habit that was deleted intentionally.");
                Assert.That(((ICollection)rewardsAfterReadyRetry).Count, Is.Zero,
                    "A ready profile must not recreate a seed reward that was deleted intentionally.");

                await DeleteFirestoreDocumentFieldAsync(firestoreType, firestore, parentId, "provisioningState");
                Assert.That(await GetProvisioningStateAsync(parentId), Is.Null,
                    "The Emulator fixture should represent a legacy parent without a provisioning marker.");
                object legacyRetry = await GetTaskResultAsync(login.Invoke(manager, new object[] { email, password }));
                Assert.That((bool)legacyRetry.GetType().GetField("Item1").GetValue(legacyRetry), Is.True);
                object habitsAfterLegacyRetry = await GetTaskResultAsync(managerType.GetMethod(
                    "GetHabitsAsync", new[] { typeof(string), typeof(string) })
                    .Invoke(manager, new object[] { parentId, "default" }));
                object rewardsAfterLegacyRetry = await GetTaskResultAsync(managerType.GetMethod(
                    "GetRewardsAsync", new[] { typeof(string), typeof(string) })
                    .Invoke(manager, new object[] { parentId, "default" }));
                Assert.That(((ICollection)habitsAfterLegacyRetry).Count, Is.Zero,
                    "A legacy parent without provisioningState must not be auto-seeded on login.");
                Assert.That(((ICollection)rewardsAfterLegacyRetry).Count, Is.Zero,
                    "A legacy parent without provisioningState must not be auto-seeded on login.");
                Assert.That(await GetProvisioningStateAsync(parentId), Is.Null,
                    "Login must not add a provisioning marker to an unclassified legacy parent.");

                string e11ChildOperationId = Guid.NewGuid().ToString("N");
                object pendingE11Child = CreatePendingChild(
                    manager, managerType, e11ChildOperationId, "Emulator child", 7, 0);
                object firstChildAdd = await GetTaskResultAsync(addChild.Invoke(manager,
                    new[] { pendingE11Child }));
                Assert.That((bool)firstChildAdd.GetType().GetField("Item1").GetValue(firstChildAdd), Is.True,
                    (string)firstChildAdd.GetType().GetField("Item2").GetValue(firstChildAdd));
                object matchingChildRetry = await GetTaskResultAsync(addChild.Invoke(manager,
                    new[] { pendingE11Child }));
                Assert.That((bool)matchingChildRetry.GetType().GetField("Item1").GetValue(matchingChildRetry), Is.True,
                    (string)matchingChildRetry.GetType().GetField("Item2").GetValue(matchingChildRetry));
                object conflictingChildRetry = await GetTaskResultAsync(addChild.Invoke(manager,
                    new[] { CreatePendingChild(manager, managerType, e11ChildOperationId, "Changed retry name", 9, 1) }));
                Assert.That((bool)conflictingChildRetry.GetType().GetField("Item1").GetValue(conflictingChildRetry), Is.False,
                    "A reused operation ID with a different child payload must be rejected.");

                string originalSelectedChild = (string)managerType.GetMethod("GetCurrentChildId").Invoke(manager, null);
                string switchedChild = originalSelectedChild == e11ChildOperationId ? "default" : e11ChildOperationId;
                managerType.GetMethod("SetCurrentChildId").Invoke(manager, new object[] { switchedChild });
                object switchedChildRetry = await GetTaskResultAsync(addChild.Invoke(manager, new[] { pendingE11Child }));
                Assert.That((bool)switchedChildRetry.GetType().GetField("Item1").GetValue(switchedChildRetry), Is.False,
                    "The pending child operation must not run under a different selected-child session.");
                managerType.GetMethod("SetCurrentChildId").Invoke(manager, new object[] { originalSelectedChild });
                object returnedScopeRetry = await GetTaskResultAsync(addChild.Invoke(manager, new[] { pendingE11Child }));
                Assert.That((bool)returnedScopeRetry.GetType().GetField("Item1").GetValue(returnedScopeRetry), Is.False,
                    "Returning to the same child must not silently revive an old session generation.");
                object returnedScope = CreateCurrentParentWriteScope(manager, managerType);
                object resumedPendingChild = pendingE11Child.GetType().GetMethod("ResumeInSameParent")
                    .Invoke(pendingE11Child, new[] { returnedScope });
                object explicitResume = await GetTaskResultAsync(addChild.Invoke(manager, new[] { resumedPendingChild }));
                Assert.That((bool)explicitResume.GetType().GetField("Item1").GetValue(explicitResume), Is.True,
                    (string)explicitResume.GetType().GetField("Item2").GetValue(explicitResume));

                object childrenAfterRetry = await GetTaskResultAsync(managerType.GetMethod(
                    "GetChildrenAsync", new[] { typeof(string) }).Invoke(manager, new object[] { parentId }));
                Assert.That(((ICollection)childrenAfterRetry).Count, Is.EqualTo(3));
                object createdChild = ((IEnumerable)childrenAfterRetry).Cast<object>()
                    .Single(item => (string)item.GetType().GetProperty("childId").GetValue(item) == e11ChildOperationId);
                Assert.That(createdChild.GetType().GetProperty("name").GetValue(createdChild),
                    Is.EqualTo("Emulator child"));
                Assert.That(createdChild.GetType().GetProperty("age").GetValue(createdChild), Is.EqualTo(7));
                Assert.That(createdChild.GetType().GetProperty("gender").GetValue(createdChild), Is.EqualTo(0));
                Assert.That(createdChild.GetType().GetProperty("ageAsOf").GetValue(createdChild), Is.Not.Null);

                object habits = await GetTaskResultAsync(managerType.GetMethod(
                    "GetHabitsAsync", new[] { typeof(string), typeof(string) })
                    .Invoke(manager, new object[] { parentId, childId }));
                object rewards = await GetTaskResultAsync(managerType.GetMethod(
                    "GetRewardsAsync", new[] { typeof(string), typeof(string) })
                    .Invoke(manager, new object[] { parentId, childId }));
                Assert.That(((ICollection)habits).Count, Is.Zero,
                    "An existing non-default child must not be mistaken for the default seed target.");
                Assert.That(((ICollection)rewards).Count, Is.Zero,
                    "An existing non-default child must not be mistaken for the default seed target.");

                string today = (string)FindProjectType("HabitLog")
                    .GetMethod("GetTodayDateKey", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, null);
                Type habitType = FindProjectType("HabitModel");
                object habit = Activator.CreateInstance(habitType);
                habitType.GetProperty("title").SetValue(habit, "Emulator codec habit");
                habitType.GetProperty("timeTarget").SetValue(habit, "20:00");
                habitType.GetProperty("repeatDays").SetValue(habit, new List<int> { 0, 1, 2, 3, 4, 5, 6 });
                habitType.GetProperty("effectiveFrom").SetValue(habit, today);
                habitType.GetProperty("rewardGold").SetValue(habit, 11);
                habitType.GetProperty("rewardExp").SetValue(habit, 17);
                habitType.GetProperty("requireApproval").SetValue(habit, true);
                habitType.GetProperty("isActive").SetValue(habit, true);
                MethodInfo addHabit = managerType.GetMethod("AddHabitAsync",
                    new[] { FindProjectType("HABIMON.Core.FirebaseWriteScope"), habitType, typeof(string) });
                object scope = CreateCurrentWriteScope(manager, managerType);
                object addHabitResult = await GetTaskResultAsync(addHabit
                    .Invoke(manager, new[] { scope, habit, "f15_codec_habit" }));
                Assert.That((bool)addHabitResult.GetType().GetField("Item1").GetValue(addHabitResult), Is.True,
                    (string)addHabitResult.GetType().GetField("Item2").GetValue(addHabitResult));
                habitType.GetProperty("title").SetValue(habit, "Changed retry title");
                object conflictingHabitRetry = await GetTaskResultAsync(addHabit
                    .Invoke(manager, new[] { scope, habit, "f15_codec_habit" }));
                Assert.That((bool)conflictingHabitRetry.GetType().GetField("Item1").GetValue(conflictingHabitRetry), Is.False,
                    "A reused operation ID with a different habit payload must be rejected.");
                object habitsAfterConflict = await GetTaskResultAsync(managerType.GetMethod("GetHabitsAsync",
                    new[] { typeof(string), typeof(string) }).Invoke(manager, new object[] { parentId, childId }));
                object persistedHabit = ((IEnumerable)habitsAfterConflict).Cast<object>()
                    .Single(item => (string)item.GetType().GetProperty("id").GetValue(item) == "f15_codec_habit");
                Assert.That(persistedHabit.GetType().GetProperty("title").GetValue(persistedHabit),
                    Is.EqualTo("Emulator codec habit"));
                habitType.GetProperty("title").SetValue(habit, "Emulator codec habit");
                string switchedHabitChild = childId == e11ChildOperationId ? "default" : e11ChildOperationId;
                managerType.GetMethod("SetCurrentChildId").Invoke(manager, new object[] { switchedHabitChild });
                object switchedHabitRetry = await GetTaskResultAsync(addHabit
                    .Invoke(manager, new[] { scope, habit, "f15_codec_habit" }));
                Assert.That((bool)switchedHabitRetry.GetType().GetField("Item1").GetValue(switchedHabitRetry), Is.False,
                    "The pending habit operation must not write under a different selected child.");
                Assert.That(await GetFirestoreDocumentExistsAsync(parentId,
                    "children", switchedHabitChild, "habits", "f15_codec_habit"), Is.False);
                managerType.GetMethod("SetCurrentChildId").Invoke(manager, new object[] { childId });
                object returnedHabitScope = CreateCurrentWriteScope(manager, managerType);
                object returnedHabitRetry = await GetTaskResultAsync(addHabit
                    .Invoke(manager, new[] { returnedHabitScope, habit, "f15_codec_habit" }));
                Assert.That((bool)returnedHabitRetry.GetType().GetField("Item1").GetValue(returnedHabitRetry), Is.True,
                    (string)returnedHabitRetry.GetType().GetField("Item2").GetValue(returnedHabitRetry));
                scope = returnedHabitScope;

                Type rewardServiceType = FindProjectType("HabitRewardService");
                MethodInfo setCompletion = rewardServiceType.GetMethod("SetTodayCompletionAsync");
                DateTime todayDate = DateTime.ParseExact(
                    today, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                int todayIsoWeekday = ((int)todayDate.DayOfWeek + 6) % 7 + 1;
                int unscheduledWeekday = todayIsoWeekday % 7 + 1;
                object unscheduledHabit = Activator.CreateInstance(habitType);
                habitType.GetProperty("title").SetValue(unscheduledHabit, "Unscheduled fixture habit");
                habitType.GetProperty("timeTarget").SetValue(unscheduledHabit, "20:00");
                habitType.GetProperty("repeatDays").SetValue(unscheduledHabit, new List<int> { unscheduledWeekday });
                habitType.GetProperty("effectiveFrom").SetValue(unscheduledHabit, today);
                habitType.GetProperty("isActive").SetValue(unscheduledHabit, true);
                object unscheduledHabitResult = await GetTaskResultAsync(addHabit
                    .Invoke(manager, new[] { scope, unscheduledHabit, "f09_unscheduled_habit" }));
                Assert.That((bool)unscheduledHabitResult.GetType().GetField("Item1").GetValue(unscheduledHabitResult), Is.True);
                Assert.That(await GetTaskResultAsync(setCompletion.Invoke(
                    null, new object[] { parentId, childId, "f09_unscheduled_habit", today, true })), Is.EqualTo(false),
                    "The client transaction must reject completion on a day outside the habit's repeatDays.");
                object logsAfterUnscheduledAttempt = await GetTaskResultAsync(managerType.GetMethod("GetHabitLogsAsync")
                    .Invoke(manager, new object[] { parentId, childId }));
                Assert.That(((IEnumerable)logsAfterUnscheduledAttempt).Cast<object>()
                    .Any(item => (string)item.GetType().GetProperty("habitId").GetValue(item) == "f09_unscheduled_habit"), Is.False,
                    "An unscheduled completion must not create a daily log.");

                Task<bool> firstCompletion = (Task<bool>)setCompletion
                    .Invoke(null, new object[] { parentId, childId, "f15_codec_habit", today, true });
                Task<bool> concurrentCompletion = (Task<bool>)setCompletion
                    .Invoke(null, new object[] { parentId, childId, "f15_codec_habit", today, true });
                bool[] completionResults = await Task.WhenAll(firstCompletion, concurrentCompletion);
                Assert.That(completionResults, Is.All.True);

                object logs = await GetTaskResultAsync(managerType.GetMethod("GetHabitLogsAsync")
                    .Invoke(manager, new object[] { parentId, childId }));
                Assert.That(((ICollection)logs).Count, Is.EqualTo(1));
                object log = ((IEnumerable)logs).Cast<object>().Single();
                Type logType = log.GetType();
                Assert.That(logType.GetProperty("requiresApprovalAtCompletion").GetValue(log), Is.EqualTo(true));
                Assert.That(logType.GetProperty("rewardGoldAtCompletion").GetValue(log), Is.EqualTo(11));
                Assert.That(logType.GetProperty("rewardExpAtCompletion").GetValue(log), Is.EqualTo(17));
                Assert.That(logType.GetProperty("completedAt").GetValue(log), Is.Not.Null);
                Assert.That(logType.GetProperty("approvedAt").GetValue(log), Is.Null);

                MethodInfo approveForChild = rewardServiceType.GetMethod("ApprovePendingLogForChildAsync");
                Assert.That(approveForChild, Is.Not.Null,
                    "The multi-child UI needs an explicit-child approval with an expected completion timestamp.");
                string logId = (string)logType.GetProperty("logId").GetValue(log);
                object staleCompletedAt = logType.GetProperty("completedAt").GetValue(log);
                Assert.That(await GetTaskResultAsync(setCompletion.Invoke(null,
                    new object[] { parentId, childId, "f15_codec_habit", today, false })), Is.EqualTo(true));
                Assert.That(await GetTaskResultAsync(setCompletion.Invoke(null,
                    new object[] { parentId, childId, "f15_codec_habit", today, true })), Is.EqualTo(true));
                object renewedLogs = await GetTaskResultAsync(managerType.GetMethod("GetHabitLogsAsync")
                    .Invoke(manager, new object[] { parentId, childId }));
                object renewedLog = ((IEnumerable)renewedLogs).Cast<object>().Single();
                object expectedCompletedAt = logType.GetProperty("completedAt").GetValue(renewedLog);
                Assert.That(expectedCompletedAt, Is.Not.EqualTo(staleCompletedAt));
                Assert.That(await GetTaskResultAsync(approveForChild.Invoke(null,
                    new[] { (object)parentId, childId, logId, staleCompletedAt })), Is.EqualTo(false),
                    "A request from before cancel/recomplete must not approve the new completion.");
                Assert.That(await GetTaskResultAsync(approveForChild.Invoke(null,
                    new[] { (object)"different-parent", childId, logId, expectedCompletedAt })), Is.EqualTo(false));

                managerType.GetMethod("SetCurrentChildId").Invoke(manager, new object[] { e11ChildOperationId });
                Assert.That(await GetTaskResultAsync(setCompletion.Invoke(null,
                    new object[] { parentId, childId, "f15_codec_habit", today, true })), Is.EqualTo(false),
                    "Child completion must retain the selected-child boundary.");
                Task<bool> explicitApproval = (Task<bool>)approveForChild.Invoke(null,
                    new[] { (object)parentId, childId, logId, expectedCompletedAt });
                Task<bool> repeatedExplicitApproval = (Task<bool>)approveForChild.Invoke(null,
                    new[] { (object)parentId, childId, logId, expectedCompletedAt });
                Assert.That(await Task.WhenAll(explicitApproval, repeatedExplicitApproval), Is.All.True,
                    "The all-child parent list must approve its explicit target without switching the current child.");
                Assert.That(managerType.GetMethod("GetCurrentChildId").Invoke(manager, null), Is.EqualTo(e11ChildOperationId));
                managerType.GetMethod("SetCurrentChildId").Invoke(manager, new object[] { childId });

                MethodInfo approveLog = rewardServiceType.GetMethod("ApprovePendingLogAsync");
                Task<bool> firstApproval = (Task<bool>)approveLog.Invoke(
                    null, new object[] { parentId, childId, logId });
                Task<bool> concurrentApproval = (Task<bool>)approveLog.Invoke(
                    null, new object[] { parentId, childId, logId });
                bool[] approvalResults = await Task.WhenAll(firstApproval, concurrentApproval);
                Assert.That(approvalResults, Is.All.True);
                Assert.That(await GetTaskResultAsync(approveLog.Invoke(
                    null, new object[] { parentId, childId, logId })), Is.EqualTo(true));

                object updatedChildren = await GetTaskResultAsync(managerType.GetMethod(
                    "GetChildrenAsync", new[] { typeof(string) }).Invoke(manager, new object[] { parentId }));
                object updatedChild = ((IEnumerable)updatedChildren).Cast<object>()
                    .Single(item => (string)item.GetType().GetProperty("childId").GetValue(item) == childId);
                Assert.That(updatedChild.GetType().GetProperty("gold").GetValue(updatedChild), Is.EqualTo(11));
                Assert.That(updatedChild.GetType().GetProperty("totalExp").GetValue(updatedChild), Is.EqualTo(17));

                object approvedLogs = await GetTaskResultAsync(managerType.GetMethod("GetHabitLogsAsync")
                    .Invoke(manager, new object[] { parentId, childId }));
                object approvedLog = ((IEnumerable)approvedLogs).Cast<object>().Single();
                Assert.That(approvedLog.GetType().GetProperty("isApproved").GetValue(approvedLog), Is.EqualTo(true));
                Assert.That(approvedLog.GetType().GetProperty("rewardGranted").GetValue(approvedLog), Is.EqualTo(true));

                string legacyLogId = today + "_f15_codec_habit";
                object legacyLog = Activator.CreateInstance(logType);
                logType.GetProperty("habitId").SetValue(legacyLog, "f15_codec_habit");
                logType.GetProperty("dateKey").SetValue(legacyLog, today);
                logType.GetProperty("isCompleted").SetValue(legacyLog, true);
                logType.GetProperty("completedAt").SetValue(legacyLog, expectedCompletedAt);
                object logsCollection = GetChildCollection(firestoreType, firestore, parentId, childId, "habit_logs");
                object legacyReference = logsCollection.GetType().GetMethod("Document", new[] { typeof(string) })
                    .Invoke(logsCollection, new object[] { legacyLogId });
                MethodInfo setLegacy = legacyReference.GetType().GetMethods().Single(method =>
                    method.Name == "SetAsync" && method.GetParameters().Length == 2);
                await (Task)setLegacy.Invoke(legacyReference, new object[] { legacyLog, null });
                Assert.That(await GetTaskResultAsync(setCompletion.Invoke(null,
                    new object[] { parentId, childId, "f15_codec_habit", today, true })), Is.EqualTo(false),
                    "Conflicting legacy identities must be reconciled before another completion write.");
                Assert.That(await GetTaskResultAsync(approveForChild.Invoke(null,
                    new[] { (object)parentId, childId, legacyLogId, expectedCompletedAt })), Is.EqualTo(false),
                    "A legacy document must not independently grant the canonical day's reward.");
                Assert.That(await GetTaskResultAsync(approveForChild.Invoke(null,
                    new[] { (object)parentId, childId, logId, expectedCompletedAt })), Is.EqualTo(false),
                    "Canonical approval must also stop when a conflicting legacy record exists.");
                await DeleteFirestoreDocumentAsync(firestoreType, firestore, parentId,
                    "children", childId, "habit_logs", legacyLogId);

                object autoHabit = Activator.CreateInstance(habitType);
                habitType.GetProperty("title").SetValue(autoHabit, "Automatic reward fixture");
                habitType.GetProperty("timeTarget").SetValue(autoHabit, "20:00");
                habitType.GetProperty("repeatDays").SetValue(autoHabit, new List<int> { 1, 2, 3, 4, 5, 6, 7 });
                habitType.GetProperty("effectiveFrom").SetValue(autoHabit, today);
                habitType.GetProperty("isActive").SetValue(autoHabit, true);
                habitType.GetProperty("rewardGold").SetValue(autoHabit, 3);
                habitType.GetProperty("rewardExp").SetValue(autoHabit, 110);
                object autoHabitResult = await GetTaskResultAsync(addHabit.Invoke(manager,
                    new[] { CreateCurrentWriteScope(manager, managerType), autoHabit, "auto_reward_habit" }));
                Assert.That((bool)autoHabitResult.GetType().GetField("Item1").GetValue(autoHabitResult), Is.True);
                Task<bool> autoCompletion = (Task<bool>)setCompletion.Invoke(null,
                    new object[] { parentId, childId, "auto_reward_habit", today, true });
                Task<bool> repeatedAutoCompletion = (Task<bool>)setCompletion.Invoke(null,
                    new object[] { parentId, childId, "auto_reward_habit", today, true });
                Assert.That(await Task.WhenAll(autoCompletion, repeatedAutoCompletion), Is.All.True);
                object autoLogs = await GetTaskResultAsync(managerType.GetMethod("GetHabitLogsAsync")
                    .Invoke(manager, new object[] { parentId, childId }));
                object autoLog = ((IEnumerable)autoLogs).Cast<object>().Single(item =>
                    (string)logType.GetProperty("habitId").GetValue(item) == "auto_reward_habit");
                Assert.That(logType.GetProperty("isApproved").GetValue(autoLog), Is.EqualTo(true));
                Assert.That(logType.GetProperty("rewardGranted").GetValue(autoLog), Is.EqualTo(true));
                object autoChildren = await GetTaskResultAsync(managerType.GetMethod("GetChildrenAsync")
                    .Invoke(manager, new object[] { parentId }));
                object autoChild = ((IEnumerable)autoChildren).Cast<object>().Single(item =>
                    (string)item.GetType().GetProperty("childId").GetValue(item) == childId);
                Assert.That(autoChild.GetType().GetProperty("gold").GetValue(autoChild), Is.EqualTo(14));
                Assert.That(autoChild.GetType().GetProperty("totalExp").GetValue(autoChild), Is.EqualTo(127));

                string cleanupChildId = Guid.NewGuid().ToString("N");
                object cleanupChildResult = await GetTaskResultAsync(addChild.Invoke(manager,
                    new[] { CreatePendingChild(manager, managerType, cleanupChildId, "Cleanup fixture", 8, 1) }));
                Assert.That((bool)cleanupChildResult.GetType().GetField("Item1").GetValue(cleanupChildResult), Is.True);

                object cleanupHabit = Activator.CreateInstance(habitType);
                habitType.GetProperty("title").SetValue(cleanupHabit, "Cleanup fixture habit");
                habitType.GetProperty("timeTarget").SetValue(cleanupHabit, "20:00");
                habitType.GetProperty("repeatDays").SetValue(cleanupHabit, new List<int> { 1, 2, 3, 4, 5, 6, 7 });
                habitType.GetProperty("effectiveFrom").SetValue(cleanupHabit, today);
                habitType.GetProperty("isActive").SetValue(cleanupHabit, true);
                managerType.GetMethod("SetCurrentChildId").Invoke(manager, new object[] { cleanupChildId });
                object cleanupScope = Activator.CreateInstance(FindProjectType("HABIMON.Core.FirebaseWriteScope"),
                    new object[] { parentId, cleanupChildId, (int)managerType.GetProperty("SessionGeneration").GetValue(manager) });
                object cleanupHabitResult = await GetTaskResultAsync(addHabit
                    .Invoke(manager, new[] { cleanupScope, cleanupHabit, "cleanup_fixture_habit" }));
                Assert.That((bool)cleanupHabitResult.GetType().GetField("Item1").GetValue(cleanupHabitResult), Is.True);

                Type rewardType = FindProjectType("RewardModel");
                object cleanupReward = Activator.CreateInstance(rewardType);
                rewardType.GetProperty("name").SetValue(cleanupReward, "Cleanup fixture reward");
                rewardType.GetProperty("itemType").SetValue(cleanupReward, "item");
                rewardType.GetProperty("cost").SetValue(cleanupReward, 1);
                rewardType.GetProperty("isUnlocked").SetValue(cleanupReward, false);
                await (Task)managerType.GetMethod("AddRewardAsync")
                    .Invoke(manager, new[] { parentId, cleanupChildId, cleanupReward });

                object cleanupLog = Activator.CreateInstance(FindProjectType("HabitLogModel"));
                cleanupLog.GetType().GetProperty("habitId").SetValue(cleanupLog, "cleanup_fixture_habit");
                cleanupLog.GetType().GetProperty("dateKey").SetValue(cleanupLog, today);
                cleanupLog.GetType().GetProperty("isCompleted").SetValue(cleanupLog, true);
                await (Task)managerType.GetMethod("AddHabitLogAsync")
                    .Invoke(manager, new[] { parentId, cleanupChildId, cleanupLog });

                Assert.That(((ICollection)await GetTaskResultAsync(managerType.GetMethod(
                    "GetHabitsAsync", new[] { typeof(string), typeof(string) })
                    .Invoke(manager, new object[] { parentId, cleanupChildId }))).Count, Is.EqualTo(1));
                Assert.That(((ICollection)await GetTaskResultAsync(managerType.GetMethod("GetHabitLogsAsync")
                    .Invoke(manager, new object[] { parentId, cleanupChildId }))).Count, Is.EqualTo(1));
                Assert.That(((ICollection)await GetTaskResultAsync(managerType.GetMethod(
                    "GetRewardsAsync", new[] { typeof(string), typeof(string) })
                    .Invoke(manager, new object[] { parentId, cleanupChildId }))).Count, Is.EqualTo(1));
                Assert.That(await GetFirestoreDocumentExistsAsync(parentId,
                    "children", cleanupChildId, "habimon", "status"), Is.True);

                object deleteChildResult = await GetTaskResultAsync(managerType.GetMethod("DeleteChildAsync")
                    .Invoke(manager, new object[] { cleanupChildId }));
                Assert.That((bool)deleteChildResult.GetType().GetField("Item1").GetValue(deleteChildResult), Is.True,
                    (string)deleteChildResult.GetType().GetField("Item2").GetValue(deleteChildResult));
                object remainingChildren = await GetTaskResultAsync(managerType.GetMethod(
                    "GetChildrenAsync", new[] { typeof(string) }).Invoke(manager, new object[] { parentId }));
                Assert.That(((IEnumerable)remainingChildren).Cast<object>()
                    .Any(item => (string)item.GetType().GetProperty("childId").GetValue(item) == cleanupChildId), Is.False);
                Assert.That(((ICollection)await GetTaskResultAsync(managerType.GetMethod(
                    "GetHabitsAsync", new[] { typeof(string), typeof(string) })
                    .Invoke(manager, new object[] { parentId, cleanupChildId }))).Count, Is.Zero);
                Assert.That(((ICollection)await GetTaskResultAsync(managerType.GetMethod("GetHabitLogsAsync")
                    .Invoke(manager, new object[] { parentId, cleanupChildId }))).Count, Is.Zero);
                Assert.That(((ICollection)await GetTaskResultAsync(managerType.GetMethod(
                    "GetRewardsAsync", new[] { typeof(string), typeof(string) })
                    .Invoke(manager, new object[] { parentId, cleanupChildId }))).Count, Is.Zero);
                Assert.That(await GetFirestoreDocumentExistsAsync(parentId,
                    "children", cleanupChildId, "habimon", "status"), Is.False);
                Assert.That(((ICollection)remainingChildren).Count, Is.EqualTo(3),
                    "Deleting one child and its known collections must preserve its siblings.");
            }
            finally
            {
                managerType.GetMethod("LogoutParent").Invoke(manager, null);
                UnityEngine.Object.Destroy(managerObject);
            }
        }

        private static async Task<object> GetTaskResultAsync(object taskObject)
        {
            Task task = taskObject as Task;
            Assert.That(task, Is.Not.Null);
            await task;
            return task.GetType().GetProperty("Result").GetValue(task);
        }

        private static object GetChildCollection(Type firestoreType, object firestore,
            string parentId, string childId, string collectionName)
        {
            object users = firestoreType.GetMethod("Collection").Invoke(firestore, new object[] { "users" });
            object parent = users.GetType().GetMethod("Document", new[] { typeof(string) }).Invoke(users, new object[] { parentId });
            object children = parent.GetType().GetMethod("Collection").Invoke(parent, new object[] { "children" });
            object child = children.GetType().GetMethod("Document", new[] { typeof(string) }).Invoke(children, new object[] { childId });
            return child.GetType().GetMethod("Collection").Invoke(child, new object[] { collectionName });
        }

        private static object CreateCurrentWriteScope(object manager, Type managerType)
        {
            return Activator.CreateInstance(FindProjectType("HABIMON.Core.FirebaseWriteScope"), new object[]
            {
                managerType.GetMethod("GetCurrentParentId").Invoke(manager, null),
                managerType.GetMethod("GetCurrentChildId").Invoke(manager, null),
                managerType.GetProperty("SessionGeneration").GetValue(manager)
            });
        }

        private static object CreateCurrentParentWriteScope(object manager, Type managerType)
        {
            return Activator.CreateInstance(FindProjectType("HABIMON.Core.FirebaseWriteScope"), new object[]
            {
                managerType.GetMethod("GetCurrentParentId").Invoke(manager, null),
                string.Empty,
                managerType.GetProperty("SessionGeneration").GetValue(manager)
            });
        }

        private static object CreatePendingChild(
            object manager,
            Type managerType,
            string operationId,
            string name,
            int age,
            int gender)
        {
            object scope = CreateCurrentParentWriteScope(manager, managerType);
            return Activator.CreateInstance(FindProjectType("HABIMON.Core.PendingChildCreate"), new object[]
            {
                scope,
                operationId,
                name,
                age,
                gender
            });
        }

        private static async Task<string> GetProvisioningStateAsync(string parentId)
        {
            string url = "http://127.0.0.1:8080/v1/projects/habimon-808e2/databases/(default)/documents/users/" +
                         Uri.EscapeDataString(parentId);
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "owner");
                using (HttpResponseMessage response = await client.SendAsync(request))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("Could not read local Emulator provisioning state: " +
                            response.StatusCode + " " + await response.Content.ReadAsStringAsync());

                    string payload = await response.Content.ReadAsStringAsync();
                    Match match = Regex.Match(payload,
                        "\\\"provisioningState\\\"\\s*:\\s*\\{\\s*\\\"stringValue\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
                    if (!match.Success)
                        return null;
                    return match.Groups[1].Value;
                }
            }
        }

        private static async Task DeleteFirestoreDocumentFieldAsync(
            Type firestoreType,
            object firestore,
            string parentId,
            string fieldPath)
        {
            object users = firestoreType.GetMethod("Collection", new[] { typeof(string) })
                .Invoke(firestore, new object[] { "users" });
            object parent = users.GetType().GetMethod("Document", new[] { typeof(string) })
                .Invoke(users, new object[] { parentId });

            Type fieldValueType = FindFirebaseType("Firebase.Firestore.FieldValue");
            object deleteSentinel = fieldValueType.GetProperty("Delete", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            if (deleteSentinel == null)
                deleteSentinel = fieldValueType.GetField("Delete", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null);
            if (deleteSentinel == null)
                throw new InvalidOperationException("Firebase Firestore FieldValue.Delete is unavailable.");

            MethodInfo update = parent.GetType().GetMethods()
                .First(method => method.Name == "UpdateAsync" && method.GetParameters().Length == 2 &&
                                 method.GetParameters()[0].ParameterType == typeof(string) &&
                                 method.GetParameters()[1].ParameterType == typeof(object));
            await (Task)update.Invoke(parent, new[] { (object)fieldPath, deleteSentinel });
        }

        private static async Task<bool> GetFirestoreDocumentExistsAsync(string parentId, params string[] path)
        {
            string documentPath = string.Join("/", new[] { "users", parentId }.Concat(path)
                .Select(Uri.EscapeDataString));
            string url = "http://127.0.0.1:8080/v1/projects/habimon-808e2/databases/(default)/documents/" +
                         documentPath;
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "owner");
                using (HttpResponseMessage response = await client.SendAsync(request))
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        return false;
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("Could not inspect local Emulator document: " +
                            response.StatusCode + " " + await response.Content.ReadAsStringAsync());
                    return true;
                }
            }
        }

        private static async Task DeleteFirestoreDocumentAsync(
            Type firestoreType,
            object firestore,
            string parentId,
            params string[] path)
        {
            object users = firestoreType.GetMethod("Collection", new[] { typeof(string) })
                .Invoke(firestore, new object[] { "users" });
            object document = users.GetType().GetMethod("Document", new[] { typeof(string) })
                .Invoke(users, new object[] { parentId });

            for (int index = 0; index < path.Length; index += 2)
            {
                object collection = document.GetType().GetMethod("Collection", new[] { typeof(string) })
                    .Invoke(document, new object[] { path[index] });
                document = collection.GetType().GetMethod("Document", new[] { typeof(string) })
                    .Invoke(collection, new object[] { path[index + 1] });
            }

            MethodInfo delete = document.GetType().GetMethods()
                .First(method => method.Name == "DeleteAsync" &&
                                 method.GetParameters().All(parameter => parameter.IsOptional));
            await (Task)delete.Invoke(document,
                delete.GetParameters().Select(parameter => parameter.DefaultValue).ToArray());
        }

        private static async Task SetFirestoreRulesAsync(string rules)
        {
            string url = "http://127.0.0.1:8080/emulator/v1/projects/habimon-808e2:securityRules";
            var payload = new FirestoreRulesPayload
            {
                rules = new FirestoreRulesFiles
                {
                    files = new[] { new FirestoreRulesFile { content = rules } }
                }
            };

            using (var client = new HttpClient())
            {
                using (var body = new StringContent(JsonUtility.ToJson(payload), Encoding.UTF8, "application/json"))
                using (HttpResponseMessage response = await client.PutAsync(url, body))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("Could not update local Firestore Emulator rules: " +
                            response.StatusCode + " " + await response.Content.ReadAsStringAsync());
                }
            }
        }

        [Serializable]
        private sealed class FirestoreRulesPayload
        {
            public FirestoreRulesFiles rules;
        }

        [Serializable]
        private sealed class FirestoreRulesFiles
        {
            public FirestoreRulesFile[] files;
        }

        [Serializable]
        private sealed class FirestoreRulesFile
        {
            public string content;
        }

        private static Type FindFirebaseType(string name)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(name, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Firebase type not loaded: " + name);
            return type;
        }

        private static Type FindProjectType(string name)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(name, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Project type not loaded: " + name);
            return type;
        }
    }
}
