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
            Type pointType = chart.GetType().GetNestedType("ProgressPoint", BindingFlags.NonPublic);
            Type listType = typeof(List<>).MakeGenericType(pointType);
            var points = (IList)Activator.CreateInstance(listType);
            points.Add(CreatePoint(pointType, "zero", 1, 0));
            points.Add(CreatePoint(pointType, "none", 0, 0));
            points.Add(CreatePoint(pointType, "full", 1, 1));
            MethodInfo render = FindMethod(chart.GetType(), "RenderChart", 1);

            render.Invoke(chart, new object[] { points });
            yield return null;

            int generatedCount = 0;
            bool zeroHeightBarFound = false;
            Type textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            foreach (RectTransform child in chartRoot.GetComponentsInChildren<RectTransform>(true))
            {
                if (child == chartRoot)
                    continue;
                if (child.name.StartsWith("Bar_", StringComparison.Ordinal))
                {
                    generatedCount++;
                    Assert.That(child.parent, Is.EqualTo(chartRoot));
                    if (child.name == "Bar_0")
                    {
                        Assert.That(child.sizeDelta.y, Is.Zero);
                        zeroHeightBarFound = true;
                    }
                }
                else if (child.name.StartsWith("Label_", StringComparison.Ordinal) ||
                         child.name.StartsWith("Value_", StringComparison.Ordinal))
                {
                    generatedCount++;
                    Assert.That(child.parent, Is.EqualTo(chartRoot));
                    Assert.That(child.GetComponent(textType), Is.Not.Null);
                }
            }

            Assert.That(generatedCount, Is.EqualTo(9));
            Assert.That(zeroHeightBarFound, Is.True);
            Assert.That(GetText(chartRoot.Find("Value_1").gameObject), Is.EqualTo("—"));

            int childCount = chartRoot.childCount;
            render.Invoke(chart, new object[] { points });
            yield return null;
            Assert.That(chartRoot.childCount, Is.EqualTo(childCount));
        }

        [UnityTest]
        public IEnumerator ParentSceneShowsAndOpensPendingHabitApprovalPanel()
        {
            yield return LoadParentScene();
            MonoBehaviour controller = FindSceneComponent("HabitApprovalUI");
            GameObject panel = GetField(controller, "panelRoot") as GameObject;
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

        private static object CreatePoint(Type pointType, string label, int total, int completed)
        {
            object point = Activator.CreateInstance(pointType, true);
            pointType.GetField("Label").SetValue(point, label);
            pointType.GetField("Total").SetValue(point, total);
            pointType.GetField("Completed").SetValue(point, completed);
            return point;
        }

        private static string GetText(GameObject gameObject)
        {
            Type textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Component text = gameObject.GetComponent(textType);
            return (string)textType.GetProperty("text").GetValue(text);
        }
    }
}
