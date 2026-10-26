using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HABIMON.Scene.PlayMode.Tests
{
    public class PopupAndChartPlayModeTests
    {
        private const string WaterDropsKey = "Habimon.Attendance.WaterDrops";
        private const string LastCheckInDateKey = "Habimon.Attendance.LastCheckInDate";
        private const string TotalWaterUsedKey = "Habimon.Achievements.TotalWaterUsed";

        [UnityTest]
        public IEnumerator ChildWateringAnimationRunsItsParticleEventsAndReturnsToIdle()
        {
            string[] itemIds =
            {
                "c8ba19fb117924741b0b44f7c3c3f020",
                "b7c9dd92e76651f459a10d117e03748f",
                "9ccbdc7702b28884688df17190667e9b"
            };
            var keys = new List<string>
            {
                WaterDropsKey,
                LastCheckInDateKey,
                TotalWaterUsedKey,
                "Habimon.Achievements.Completed.6143ae3dac144003a5f10a0ecf64652d",
                "Habimon.Achievements.Completed.74a9ea84cf2f4c5688ea6a7fa4bf82eb",
                "Habimon.Achievements.Completed.91a8775e692e41fc9c8ffe62f5d8150d"
            };
            keys.AddRange(itemIds.Select(id => "Habimon.Inventory.Unlocked." + id));
            var previous = keys.ToDictionary(key => key,
                key => (PlayerPrefs.HasKey(key), PlayerPrefs.GetString(key, string.Empty), PlayerPrefs.GetInt(key, 0)));
            PlayerPrefs.SetInt(WaterDropsKey, 0);
            PlayerPrefs.SetString(LastCheckInDateKey, DateTime.Today.ToString("yyyy-MM-dd"));
            PlayerPrefs.SetInt(TotalWaterUsedKey, 0);
            foreach (string key in keys.Where(key => key.StartsWith("Habimon.Achievements.Completed.", StringComparison.Ordinal)
                                                       || key.StartsWith("Habimon.Inventory.Unlocked.", StringComparison.Ordinal)))
                PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();

            try
            {
                AsyncOperation load = SceneManager.LoadSceneAsync("Child", LoadSceneMode.Single);
                while (!load.isDone) yield return null;
                yield return null;
                MonoBehaviour watering = Resources.FindObjectsOfTypeAll<MonoBehaviour>().First(component =>
                    component != null && component.gameObject.scene.name == "Child" && component.GetType().Name == "Watering");
                Animator animator = GetField(watering, "animator") as Animator;
                ParticleSystem particle = GetField(watering, "waterParticle") as ParticleSystem;
                Assert.That(animator, Is.Not.Null);
                Assert.That(particle, Is.Not.Null);

                watering.GetType().GetMethod("StartWatering").Invoke(watering, null);
                Assert.That(animator.GetBool("isWatering"), Is.False, "Zero drops must not start watering.");
                Assert.That(PlayerPrefs.GetInt(WaterDropsKey), Is.Zero);
                PlayerPrefs.SetInt(WaterDropsKey, 1);
                watering.GetType().GetMethod("StartWatering").Invoke(watering, null);
                Assert.That(animator.GetBool("isWatering"), Is.False, "One drop must not start watering.");
                Assert.That(PlayerPrefs.GetInt(WaterDropsKey), Is.EqualTo(1));
                PlayerPrefs.SetInt(WaterDropsKey, 2);
                watering.GetType().GetMethod("StartWatering").Invoke(watering, null);
                Assert.That(animator.GetBool("isWatering"), Is.True, "Exactly two drops must start watering.");
                Assert.That(PlayerPrefs.GetInt(WaterDropsKey), Is.Zero);
                Assert.That(PlayerPrefs.GetInt(TotalWaterUsedKey), Is.EqualTo(2));
                watering.GetType().GetMethod("StartWatering").Invoke(watering, null);
                Assert.That(PlayerPrefs.GetInt(WaterDropsKey), Is.Zero, "A repeated click must not charge twice.");
                yield return new WaitForSeconds(3f);
                Assert.That(particle.isPlaying, Is.True, "The animation must invoke StartParticle.");
                yield return new WaitForSeconds(5f);
                Assert.That(animator.GetBool("isWatering"), Is.False, "The animation must invoke StopWatering.");
                Assert.That(particle.isPlaying, Is.False, "The animation must invoke StopParticle.");
            }
            finally
            {
                foreach (var entry in previous)
                {
                    if (!entry.Value.Item1)
                        PlayerPrefs.DeleteKey(entry.Key);
                    else if (entry.Key == LastCheckInDateKey)
                        PlayerPrefs.SetString(entry.Key, entry.Value.Item2);
                    else
                        PlayerPrefs.SetInt(entry.Key, entry.Value.Item3);
                }
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator AddChildPopupStaysOpenOnItsFirstActivation()
        {
            yield return LoadParentScene();
            MonoBehaviour controller = FindSceneComponent("AddChild");
            GameObject panel = GetPopupPanel(controller);

            panel.SetActive(true);
            yield return null;

            Assert.That(panel.activeSelf, Is.True);
            Assert.That(GetField(controller, "popupStatusText"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ChildListRefreshReportsFailureWhenFirebaseManagerIsUnavailable()
        {
            Type managerType = GetAssemblyType("FirebaseManager");
            PropertyInfo instanceProperty = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            MethodInfo instanceSetter = instanceProperty.GetSetMethod(true);
            object previousManager = instanceProperty.GetValue(null);
            GameObject holder = new GameObject("ChildrentRefreshTest");
            holder.SetActive(false);

            try
            {
                instanceSetter.Invoke(null, new object[] { null });
                Type childrentType = GetAssemblyType("Childrent");
                MonoBehaviour childrent = (MonoBehaviour)holder.AddComponent(childrentType);
                MethodInfo refresh = FindMethod(childrentType, "LoadChildrenAsync", 0);
                Task task = (Task)refresh.Invoke(childrent, null);
                while (!task.IsCompleted)
                    yield return null;

                PropertyInfo resultProperty = task.GetType().GetProperty("Result");
                Assert.That(resultProperty, Is.Not.Null,
                    "A refresh caller must be able to distinguish a failed list load from a completed one.");
                Assert.That(resultProperty.PropertyType, Is.EqualTo(typeof(bool)),
                    "The refresh result must report success rather than an empty Task result.");
                Assert.That((bool)resultProperty.GetValue(task), Is.False);
            }
            finally
            {
                instanceSetter.Invoke(null, new[] { previousManager });
                if (holder != null)
                    UnityEngine.Object.Destroy(holder);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedChildRefreshFailureKeepsTheSameOperationForSafeRetry()
        {
            yield return LoadParentScene();
            MonoBehaviour controller = FindSceneComponent("AddChild");
            FieldInfo operationIdField = controller.GetType().GetField("pendingOperationId", BindingFlags.Instance | BindingFlags.NonPublic);
            string operationId = Guid.NewGuid().ToString("N");
            operationIdField.SetValue(controller, operationId);

            MethodInfo completeRefresh = controller.GetType().GetMethod(
                "HandleChildListRefreshAfterSave", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(completeRefresh, Is.Not.Null,
                "A committed child must remain on the idempotent retry path until its list refresh succeeds.");
            if (completeRefresh == null)
                yield break;

            bool shouldClose = (bool)completeRefresh.Invoke(controller, new object[] { false });
            Assert.That(shouldClose, Is.False);
            Assert.That(operationIdField.GetValue(controller), Is.EqualTo(operationId));

            object status = GetField(controller, "popupStatusText");
            Type textType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            string statusMessage = (string)textType.GetProperty("text").GetValue(status);
            StringAssert.Contains("Đã lưu", statusMessage);

            var childName = GetField(controller, "inputChildName");
            Type inputType = childName.GetType();
            Assert.That((bool)inputType.GetProperty("interactable").GetValue(childName), Is.False);
            Button addButton = GetField(controller, "addChildButton") as Button;
            Component buttonLabel = addButton.GetComponentInChildren(textType, true);
            Assert.That((string)textType.GetProperty("text").GetValue(buttonLabel), Is.EqualTo("Thử lại"));

            shouldClose = (bool)completeRefresh.Invoke(controller, new object[] { true });
            Assert.That(shouldClose, Is.True);
            Assert.That(operationIdField.GetValue(controller), Is.Null);
            Assert.That((bool)inputType.GetProperty("interactable").GetValue(childName), Is.True);
        }

        [UnityTest]
        public IEnumerator MissionHabitReloadReportsFailureWhenFirebaseManagerIsUnavailable()
        {
            Type managerType = GetAssemblyType("FirebaseManager");
            PropertyInfo instanceProperty = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            MethodInfo instanceSetter = instanceProperty.GetSetMethod(true);
            object previousManager = instanceProperty.GetValue(null);
            GameObject holder = new GameObject("MissionReloadTest");
            holder.SetActive(false);

            try
            {
                instanceSetter.Invoke(null, new object[] { null });
                Type missionType = GetAssemblyType("Mission");
                MonoBehaviour mission = (MonoBehaviour)holder.AddComponent(missionType);
                MethodInfo reload = FindMethod(missionType, "LoadHabitsForCurrentUser", 0);
                Task task = (Task)reload.Invoke(mission, null);
                while (!task.IsCompleted)
                    yield return null;

                PropertyInfo resultProperty = task.GetType().GetProperty("Result");
                Assert.That(resultProperty, Is.Not.Null);
                Assert.That(resultProperty.PropertyType, Is.EqualTo(typeof(bool)));
                Assert.That((bool)resultProperty.GetValue(task), Is.False);
            }
            finally
            {
                instanceSetter.Invoke(null, new[] { previousManager });
                if (holder != null)
                    UnityEngine.Object.Destroy(holder);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator AddHabitPopupRetainsOperationIdWhenSubmitCannotRefreshTheList()
        {
            yield return LoadParentScene();
            MonoBehaviour controller = FindSceneComponent("AddHabitPopup");
            FieldInfo operationIdField = controller.GetType().GetField("pendingOperationId", BindingFlags.Instance | BindingFlags.NonPublic);
            string operationId = Guid.NewGuid().ToString("N");
            operationIdField.SetValue(controller, operationId);

            MethodInfo handleResult = controller.GetType().GetMethod(
                "HandleSubmitResult", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(handleResult, Is.Not.Null,
                "A failed post-save refresh must keep the same habit operation available for retry.");
            if (handleResult == null)
                yield break;

            string savedButRefreshFailed = "Đã lưu thói quen nhưng chưa thể làm mới danh sách.";
            bool shouldClose = (bool)handleResult.Invoke(controller, new object[] { false, savedButRefreshFailed });
            Assert.That(shouldClose, Is.False);
            Assert.That(operationIdField.GetValue(controller), Is.EqualTo(operationId));

            object status = GetField(controller, "popupStatusText");
            Type textType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            Assert.That((string)textType.GetProperty("text").GetValue(status), Is.EqualTo(savedButRefreshFailed));

            object titleInput = GetField(controller, "inputTitle");
            Type inputType = titleInput.GetType();
            Assert.That((bool)inputType.GetProperty("interactable").GetValue(titleInput), Is.False);
            shouldClose = (bool)handleResult.Invoke(controller, new object[] { true, "Đã lưu thói quen." });
            Assert.That(shouldClose, Is.True);
            Assert.That(operationIdField.GetValue(controller), Is.Null);
            Assert.That((bool)inputType.GetProperty("interactable").GetValue(titleInput), Is.True);
        }

        [UnityTest]
        public IEnumerator AddHabitPopupStaysOpenOnItsFirstActivation()
        {
            yield return LoadParentScene();
            MonoBehaviour controller = FindSceneComponent("AddHabitPopup");
            GameObject panel = GetPopupPanel(controller);
            FindMethod(controller.GetType(), "OpenPopup", 0).Invoke(controller, null);

            yield return null;

            Assert.That(panel.activeSelf, Is.True);
            object status = GetField(controller, "popupStatusText");
            Type textType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            StringAssert.Contains("chờ phụ huynh duyệt", (string)textType.GetProperty("text").GetValue(status));
        }

        [UnityTest]
        public IEnumerator UpdateChildPopupStaysOpenWhenAwakeRunsDuringFirstOpen()
        {
            yield return LoadParentScene();
            MonoBehaviour controller = FindSceneComponent("UpdateChild");
            GameObject panel = GetPopupPanel(controller);
            Type childType = GetAssemblyType("ChildModel");
            object child = Activator.CreateInstance(childType);
            childType.GetProperty("childId").SetValue(child, "playmode-child");
            childType.GetProperty("name").SetValue(child, "Play Mode");
            childType.GetProperty("age").SetValue(child, 8);
            childType.GetProperty("gender").SetValue(child, 0);
            MethodInfo open = FindMethod(controller.GetType(), "OpenPopup", 1);

            open.Invoke(controller, new[] { child });
            yield return null;

            Assert.That(panel.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator ChartRedrawKeepsAllElementsUnderContentAndDoesNotAccumulate()
        {
            yield return LoadParentScene();
            MonoBehaviour chart = FindSceneComponent("HabitLogUI");
            RectTransform chartRoot = GetField(chart, "chartRoot") as RectTransform;
            WeeklyChartFixture fixture = CreateWeeklyChartFixture(chart, 5);
            MethodInfo render = FindMethod(chart.GetType(), "RenderChart", 2);

            render.Invoke(chart, new object[] { fixture.days, fixture.children });
            yield return null;

            Assert.That(chartRoot.childCount, Is.EqualTo(1));
            RectTransform generatedChart = chartRoot.Find("WeeklyChart") as RectTransform;
            Assert.That(generatedChart, Is.Not.Null);
            RectTransform zeroFill = generatedChart.Find($"Day_0/Child_{fixture.childIds[0]}/Completed") as RectTransform;
            Assert.That(zeroFill, Is.Not.Null);
            Assert.That(zeroFill.sizeDelta.y, Is.Zero);
            RectTransform dayZero = generatedChart.Find("Day_0") as RectTransform;
            string[] countLabels = dayZero.GetComponentsInChildren<RectTransform>(true)
                .Where(transform => transform.name == "Count")
                .Select(transform => GetText(transform.gameObject)).ToArray();
            Assert.That(countLabels, Does.Contain("—"));
            Assert.That(countLabels, Does.Contain("?"));
            Assert.That(countLabels, Does.Contain("0/1 !"));

            Type textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            foreach (RectTransform child in generatedChart.GetComponentsInChildren<RectTransform>(true))
            {
                if (child.name == "Count" || child.name == "Date" || child.name == "Percentage" || child.name == "ChildrenLegend")
                {
                    Assert.That(child.IsChildOf(chartRoot), Is.True);
                    Assert.That(child.GetComponent(textType), Is.Not.Null);
                }
            }

            int childCount = chartRoot.childCount;
            int generatedCount = generatedChart.GetComponentsInChildren<RectTransform>(true).Length;
            render.Invoke(chart, new object[] { fixture.days, fixture.children });
            yield return null;
            Assert.That(chartRoot.childCount, Is.EqualTo(childCount));
            Assert.That(generatedChart.GetComponentsInChildren<RectTransform>(true).Length, Is.EqualTo(generatedCount));

            WeeklyChartFixture widerFixture = CreateWeeklyChartFixture(chart, 8);
            render.Invoke(chart, new object[] { widerFixture.days, widerFixture.children });
            yield return null;
            Assert.That(chartRoot.rect.width, Is.GreaterThanOrEqualTo(62f + 7f * (8f * 34f + 16f)));
        }

        [UnityTest]
        public IEnumerator ParentSceneShowsAndOpensPendingHabitApprovalPanel()
        {
            yield return LoadParentScene();
            MonoBehaviour controller = FindSceneComponent("ParentConfirm");
            GameObject panel = GetField(controller, "popupRoot") as GameObject;
            Button openButton = GetField(controller, "openButton") as Button;

            Assert.That(panel, Is.Not.Null);
            Assert.That(openButton, Is.Not.Null);
            Assert.That(panel.activeSelf, Is.False);

            openButton.onClick.Invoke();
            yield return null;

            Assert.That(panel.activeSelf, Is.True);
        }

        private static IEnumerator LoadParentScene()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Parent", LoadSceneMode.Single);
            while (!load.isDone)
                yield return null;
            yield return null;
        }

        private static MonoBehaviour FindSceneComponent(string typeName)
        {
            foreach (MonoBehaviour component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (component != null && component.gameObject.scene.IsValid() &&
                    component.gameObject.scene.name == "Parent" && component.GetType().Name == typeName)
                    return component;
            }

            Assert.Fail("Could not find " + typeName + " in the Parent scene.");
            return null;
        }

        private static GameObject GetPopupPanel(MonoBehaviour component)
        {
            Assert.That(component, Is.Not.Null);
            return GetField(component, "popupPanel") as GameObject;
        }

        private static Type GetAssemblyType(string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .First(type => type != null);
        }

        private static object GetField(object instance, string fieldName)
        {
            return instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(instance);
        }

        private static MethodInfo FindMethod(Type type, string methodName, int parameterCount)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name == methodName && method.GetParameters().Length == parameterCount)
                    return method;
            }

            Assert.Fail("Could not find " + methodName + " on " + type.Name + ".");
            return null;
        }

        private sealed class WeeklyChartFixture
        {
            public Array days;
            public IList children;
            public string[] childIds;
        }

        private static WeeklyChartFixture CreateWeeklyChartFixture(MonoBehaviour chart, int childCount)
        {
            Type stateType = chart.GetType().GetNestedType("ChildProgress", BindingFlags.NonPublic);
            Type pointType = chart.GetType().GetNestedType("ProgressPoint", BindingFlags.NonPublic);
            Type stateListType = typeof(List<>).MakeGenericType(stateType);
            Type pointListType = typeof(List<>).MakeGenericType(pointType);
            IList children = (IList)Activator.CreateInstance(stateListType);
            string[] childIds = new string[childCount];
            object[] states = new object[childCount];
            for (int index = 0; index < childCount; index++)
            {
                string id = index switch
                {
                    0 => "zero",
                    1 => "none",
                    2 => "full",
                    3 => "unknown",
                    4 => "conflict",
                    _ => "child-" + index
                };
                childIds[index] = id;
                object state = Activator.CreateInstance(stateType, true);
                SetField(state, "child", CreateChildModel(id));
                SetField(state, "hasHabits", true);
                SetField(state, "hasLogs", true);
                children.Add(state);
                states[index] = state;
            }

            Array days = Array.CreateInstance(pointListType, 7);
            for (int day = 0; day < 7; day++)
            {
                IList points = (IList)Activator.CreateInstance(pointListType);
                for (int index = 0; index < childCount; index++)
                {
                    object point = Activator.CreateInstance(pointType, true);
                    SetField(point, "owner", states[index]);
                    SetField(point, "total", index == 1 || index == 3 ? 0 : 1);
                    SetField(point, "completed", index == 2 ? 1 : 0);
                    SetField(point, "hasUnknownCoverage", index == 3);
                    SetField(point, "hasConflictingRecords", index == 4);
                    points.Add(point);
                }
                days.SetValue(points, day);
            }
            return new WeeklyChartFixture { days = days, children = children, childIds = childIds };
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + fieldName);
            field.SetValue(target, value);
        }

        private static object CreateChildModel(string id)
        {
            Type type = GetAssemblyType("ChildModel");
            object child = Activator.CreateInstance(type);
            type.GetProperty("childId").SetValue(child, id);
            type.GetProperty("name").SetValue(child, id);
            return child;
        }

        private static string GetText(GameObject gameObject)
        {
            Type textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Component text = gameObject.GetComponent(textType);
            return (string)textType.GetProperty("text").GetValue(text);
        }
    }
}
