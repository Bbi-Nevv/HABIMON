using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HABIMON.Core.Tests
{
    public class SceneAssetIntegrityTests
    {
        private const string HomeworkGuid = "8a340e82fd915ea449d84491cd50dca6";
        private const long HomeworkLocalId = -3401245754914414353L;

        [Test]
        public void ParentAndChildHabitIconListsResolveTheMultipleSpriteSubAsset()
        {
            AssertSceneIconReferences("Assets/_Scenes/Parent.unity", "MissionUI", "iconSprites", 4);
            AssertSceneIconReferences("Assets/_Scenes/Parent.unity", "AddHabitPopup", "habitIcons", 4);
            AssertSceneIconReferences("Assets/_Scenes/Child.unity", "DisplayMission", "iconSprites", 4);
        }

        [Test]
        public void ParentPopupsRemainActiveAfterTheirFirstInitialization()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Scenes/Parent.unity", OpenSceneMode.Additive);
            try
            {
                AssertPopupRemainsOpen(scene, "AddChild", "OpenPopup", false);
                AssertPopupRemainsOpen(scene, "AddHabitPopup", "OpenPopup", false);
                AssertPopupRemainsOpen(scene, "UpdateChild", "OpenPopup", true);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void HabitChartUsesConcreteTextComponentsParentsEveryBarAndCleansRedraws()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Scenes/Parent.unity", OpenSceneMode.Additive);
            try
            {
                MonoBehaviour chart = FindComponent(scene, "HabitLogUI");
                Assert.That(chart, Is.Not.Null);
                var serialized = new SerializedObject(chart);
                RectTransform chartRoot = serialized.FindProperty("chartRoot").objectReferenceValue as RectTransform;
                Assert.That(chartRoot, Is.Not.Null);

                Type pointType = chart.GetType().GetNestedType("ProgressPoint", BindingFlags.NonPublic);
                Type pointsType = typeof(List<>).MakeGenericType(pointType);
                IList points = (IList)Activator.CreateInstance(pointsType);
                points.Add(CreatePoint(pointType, "zero", 1, 0));
                points.Add(CreatePoint(pointType, "none", 0, 0));
                points.Add(CreatePoint(pointType, "full", 1, 1));
                MethodInfo render = chart.GetType().GetMethod("RenderChart", BindingFlags.Instance | BindingFlags.NonPublic);
                Type textMeshProType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
                Assert.That(textMeshProType, Is.Not.Null);

                render.Invoke(chart, new object[] { points });
                int firstDrawChildCount = chartRoot.childCount;
                Assert.That(firstDrawChildCount, Is.GreaterThan(3));

                bool foundZeroHeightBar = false;
                foreach (RectTransform child in chartRoot.GetComponentsInChildren<RectTransform>(true))
                {
                    if (child == chartRoot)
                        continue;
                    Image image = child.GetComponent<Image>();
                    if (image != null && child.name.StartsWith("Bar_", StringComparison.Ordinal))
                    {
                        Assert.That(child.parent, Is.EqualTo(chartRoot));
                        if (child.name == "Bar_0")
                        {
                            Assert.That(child.sizeDelta.y, Is.Zero);
                            foundZeroHeightBar = true;
                        }
                    }

                    if (child.name.StartsWith("Label_", StringComparison.Ordinal) ||
                        child.name.StartsWith("Value_", StringComparison.Ordinal))
                        Assert.That(child.GetComponent(textMeshProType), Is.Not.Null, child.name + " uses concrete TMP UI text");
                }
                Assert.That(foundZeroHeightBar, Is.True);

                render.Invoke(chart, new object[] { points });
                Assert.That(chartRoot.childCount, Is.EqualTo(firstDrawChildCount));

                for (int index = 0; index < 31; index++)
                    points.Add(CreatePoint(pointType, index.ToString(), 1, 0));
                render.Invoke(chart, new object[] { points });
                Assert.That(chartRoot.rect.width, Is.GreaterThanOrEqualTo(31f * 40f));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ChildModeDoesNotCreateChildProfileEditOrDeleteActions()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene childScene = EditorSceneManager.OpenScene("Assets/_Scenes/Child.unity", OpenSceneMode.Additive);
            try
            {
                Assert.That(SceneManager.SetActiveScene(childScene), Is.True);
                GameObject childItem = SpawnChildItem(childScene);
                Assert.That(childItem.transform.Find("DeleteButton")?.gameObject.activeSelf ?? false, Is.False);
                Assert.That(childItem.transform.Find("EditButton")?.gameObject.activeSelf ?? false, Is.False);
            }
            finally
            {
                if (previousScene.IsValid())
                    SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(childScene, true);
            }
        }

        [Test]
        public void ParentChildDeleteActionRequiresASeparateConfirmationClick()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene parentScene = EditorSceneManager.OpenScene("Assets/_Scenes/Parent.unity", OpenSceneMode.Additive);
            try
            {
                Assert.That(SceneManager.SetActiveScene(parentScene), Is.True);
                MonoBehaviour controller = FindComponent(parentScene, "Childrent");
                GameObject item = SpawnChildItem(parentScene);
                object child = CreateChildModel();
                MethodInfo delete = controller.GetType().GetMethod("OnDeleteChildClicked", BindingFlags.Instance | BindingFlags.NonPublic);

                delete.Invoke(controller, new[] { child, item, null });

                GameObject deleteButton = item.transform.Find("DeleteButton")?.gameObject;
                Assert.That(deleteButton, Is.Not.Null);
                Type tmpTextType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
                Component label = deleteButton.GetComponentInChildren(tmpTextType, true);
                Assert.That(label, Is.Not.Null);
                Assert.That((string)tmpTextType.GetProperty("text").GetValue(label), Is.EqualTo("Xác nhận xóa"));
                Assert.That(item != null, Is.True);
            }
            finally
            {
                if (previousScene.IsValid())
                    SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(parentScene, true);
            }
        }

        [Test]
        public void FirebaseAndroidAndDesktopConfigsMatchTheRegisteredApp()
        {
            string androidPath = Path.Combine(Application.dataPath, "Plugins/Android/google-services.json");
            string desktopPath = Path.Combine(Application.dataPath, "StreamingAssets/google-services-desktop.json");
            string staleDataPath = Path.Combine(Application.dataPath, "Data/google-services.json");

            Assert.That(File.Exists(staleDataPath), Is.False,
                "Keep one Android input config; the Firebase Unity resolver scans Assets for a matching app.");

            FirebaseConfig android = JsonUtility.FromJson<FirebaseConfig>(File.ReadAllText(androidPath));
            FirebaseConfig desktop = JsonUtility.FromJson<FirebaseConfig>(File.ReadAllText(desktopPath));
            Assert.That(android.project_info.project_id, Is.EqualTo("habimon-808e2"));
            Assert.That(desktop.project_info.project_id, Is.EqualTo(android.project_info.project_id));
            Assert.That(android.client, Has.Length.EqualTo(1));
            Assert.That(desktop.client, Has.Length.EqualTo(1));
            Assert.That(android.client[0].client_info.android_client_info.package_name, Is.EqualTo("com.GSAM.HABIMON"));
            Assert.That(desktop.client[0].client_info.android_client_info.package_name,
                Is.EqualTo(android.client[0].client_info.android_client_info.package_name));
            Assert.That(desktop.client[0].client_info.mobilesdk_app_id,
                Is.EqualTo(android.client[0].client_info.mobilesdk_app_id));
            Assert.That(android.client[0].oauth_client, Is.Not.Empty,
                "The Android source config must include the registered OAuth clients.");
        }

        [Test]
        public void LoginFailureMessagesAvoidCredentialEnumerationAndSeparateRecoverableFailures()
        {
            string userNotFound = GetLoginFailureMessage("UserNotFound");
            Assert.That(GetLoginFailureMessage("WrongPassword"), Is.EqualTo(userNotFound));
            Assert.That(GetLoginFailureMessage("InvalidEmail"), Is.EqualTo(userNotFound));

            string networkFailure = GetLoginFailureMessage("NetworkRequestFailed");
            Assert.That(networkFailure, Does.Contain("kết nối"));
            Assert.That(networkFailure, Is.Not.EqualTo(userNotFound));

            string rateLimited = GetLoginFailureMessage("TooManyRequests");
            Assert.That(rateLimited, Does.Contain("chờ"));
            Assert.That(rateLimited, Is.Not.EqualTo(userNotFound));
        }

        [Test]
        public void ChildPinEntryButtonRunsPinPopupResetBeforeShowing()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene childScene = EditorSceneManager.OpenScene("Assets/_Scenes/Child.unity", OpenSceneMode.Additive);
            try
            {
                MonoBehaviour pin = FindComponent(childScene, "Pin");
                Assert.That(pin, Is.Not.Null);

                Button openButton = null;
                foreach (GameObject root in childScene.GetRootGameObjects())
                {
                    foreach (Button button in root.GetComponentsInChildren<Button>(true))
                    {
                        for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
                        {
                            if (button.onClick.GetPersistentTarget(index) == pin &&
                                button.onClick.GetPersistentMethodName(index) == "OpenVerifyPopup")
                            {
                                openButton = button;
                                break;
                            }
                        }

                        if (openButton != null)
                            break;
                    }

                    if (openButton != null)
                        break;
                }

                Assert.That(openButton, Is.Not.Null, "The Child scene PIN entry button invokes Pin.OpenVerifyPopup");

                FieldInfo attemptsField = pin.GetType().GetField("failedAttempts", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo verifyingField = pin.GetType().GetField("isVerifying", BindingFlags.Instance | BindingFlags.NonPublic);
                Button verifyButton = (Button)pin.GetType().GetField("verifyPinButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pin);
                GameObject verifyPopup = (GameObject)pin.GetType().GetField("verifyPopup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pin);
                Assert.That(attemptsField, Is.Not.Null);
                Assert.That(verifyingField, Is.Not.Null);
                Assert.That(verifyButton, Is.Not.Null);
                Assert.That(verifyPopup, Is.Not.Null);

                attemptsField.SetValue(pin, 2);
                verifyingField.SetValue(pin, true);
                verifyButton.interactable = false;
                verifyPopup.SetActive(false);
                pin.GetType().GetMethod("OpenVerifyPopup").Invoke(pin, null);

                Assert.That((int)attemptsField.GetValue(pin), Is.EqualTo(2),
                    "Reopening the popup must not erase an in-flight verification attempt.");
                Assert.That(verifyButton.interactable, Is.False,
                    "The button must remain disabled while the previous verification is still running.");
                Assert.That(verifyPopup.activeSelf, Is.True);

                verifyingField.SetValue(pin, false);
                attemptsField.SetValue(pin, 3);
                pin.GetType().GetMethod("OpenVerifyPopup").Invoke(pin, null);

                Assert.That((int)attemptsField.GetValue(pin), Is.Zero);
                Assert.That(verifyButton.interactable, Is.True);
                Assert.That(verifyPopup.activeSelf, Is.True);
            }
            finally
            {
                if (previousScene.IsValid())
                    SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(childScene, true);
            }
        }

        private static void AssertPopupRemainsOpen(Scene scene, string componentName, string openMethodName, bool requiresChild)
        {
            MonoBehaviour component = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                component = root.GetComponentsInChildren<MonoBehaviour>(true)
                    .FirstOrDefault(candidate => candidate != null && candidate.GetType().Name == componentName);
                if (component != null)
                    break;
            }

            Assert.That(component, Is.Not.Null, componentName + " exists in the scene");
            FieldInfo panelField = component.GetType().GetField("popupPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(panelField, Is.Not.Null, componentName + ".popupPanel is serialized");
            GameObject panel = panelField.GetValue(component) as GameObject;
            Assert.That(panel, Is.Not.Null);

            MethodInfo open = component.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(method => method.Name == openMethodName && method.GetParameters().Length == (requiresChild ? 1 : 0));
            if (requiresChild)
            {
                Type childModel = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("ChildModel"))
                    .First(type => type != null);
                object child = Activator.CreateInstance(childModel);
                childModel.GetProperty("childId").SetValue(child, "test-child");
                childModel.GetProperty("name").SetValue(child, "Test");
                childModel.GetProperty("age").SetValue(child, 8);
                childModel.GetProperty("gender").SetValue(child, 0);
                open.Invoke(component, new[] { child });
                component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
            }
            else
            {
                open.Invoke(component, null);
                component.GetType().GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
            }

            Assert.That(panel.activeSelf, Is.True, componentName + " remains open after first initialization");
        }

        private static MonoBehaviour FindComponent(Scene scene, string typeName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component != null && component.GetType().Name == typeName)
                        return component;
                }
            }
            return null;
        }

        private static string GetLoginFailureMessage(string authErrorName)
        {
            Type authErrorType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Firebase.Auth.AuthError"))
                .FirstOrDefault(type => type != null);
            Type firebaseExceptionType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Firebase.FirebaseException"))
                .FirstOrDefault(type => type != null);
            Type firebaseManagerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("FirebaseManager"))
                .FirstOrDefault(type => type != null);

            Assert.That(authErrorType, Is.Not.Null);
            Assert.That(firebaseExceptionType, Is.Not.Null);
            Assert.That(firebaseManagerType, Is.Not.Null);

            int errorCode = Convert.ToInt32(Enum.Parse(authErrorType, authErrorName));
            ConstructorInfo exceptionConstructor = firebaseExceptionType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(string) },
                null);
            Assert.That(exceptionConstructor, Is.Not.Null);

            Exception exception = exceptionConstructor.Invoke(new object[] { errorCode, "Firebase test detail" }) as Exception;
            Assert.That(exception, Is.Not.Null);

            MethodInfo getMessage = firebaseManagerType.GetMethod(
                "GetLoginFailureMessage", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(getMessage, Is.Not.Null);
            return (string)getMessage.Invoke(null, new object[] { exception });
        }

        private static GameObject SpawnChildItem(Scene scene)
        {
            MonoBehaviour controller = FindComponent(scene, "Childrent");
            Assert.That(controller, Is.Not.Null);
            object child = CreateChildModel();

            FieldInfo prefabField = controller.GetType().GetField("childPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo contentField = controller.GetType().GetField("contentTransform", BindingFlags.Instance | BindingFlags.NonPublic);
            var prefab = prefabField.GetValue(controller) as GameObject;
            var content = contentField.GetValue(controller) as Transform;
            Assert.That(prefab, Is.Not.Null);
            Assert.That(content, Is.Not.Null);

            MethodInfo spawn = controller.GetType().GetMethod("SpawnChildItem", BindingFlags.Instance | BindingFlags.NonPublic);
            spawn.Invoke(controller, new[] { child, null });
            return content.GetChild(content.childCount - 1).gameObject;
        }

        private static object CreateChildModel()
        {
            Type childModelType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("ChildModel"))
                .First(type => type != null);
            object child = Activator.CreateInstance(childModelType);
            childModelType.GetProperty("childId").SetValue(child, "test-child");
            childModelType.GetProperty("name").SetValue(child, "Test");
            childModelType.GetProperty("age").SetValue(child, 8);
            childModelType.GetProperty("gender").SetValue(child, 0);
            return child;
        }

        [Serializable]
        private sealed class FirebaseConfig
        {
            public FirebaseProjectInfo project_info;
            public FirebaseClient[] client;
        }

        [Serializable]
        private sealed class FirebaseProjectInfo
        {
            public string project_id;
        }

        [Serializable]
        private sealed class FirebaseClient
        {
            public FirebaseClientInfo client_info;
            public FirebaseOAuthClient[] oauth_client;
        }

        [Serializable]
        private sealed class FirebaseClientInfo
        {
            public string mobilesdk_app_id;
            public FirebaseAndroidInfo android_client_info;
        }

        [Serializable]
        private sealed class FirebaseAndroidInfo
        {
            public string package_name;
        }

        [Serializable]
        private sealed class FirebaseOAuthClient
        {
            public string client_id;
            public int client_type;
        }

        private static object CreatePoint(Type pointType, string label, int total, int completed)
        {
            object point = Activator.CreateInstance(pointType, true);
            pointType.GetField("Label").SetValue(point, label);
            pointType.GetField("Total").SetValue(point, total);
            pointType.GetField("Completed").SetValue(point, completed);
            return point;
        }

        private static void AssertSceneIconReferences(string scenePath, string componentTypeName, string propertyName, int index)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (component == null || component.GetType().Name != componentTypeName)
                            continue;

                        var serialized = new SerializedObject(component);
                        SerializedProperty icons = serialized.FindProperty(propertyName);
                        Assert.That(icons, Is.Not.Null, $"{componentTypeName}.{propertyName} is serialized in {scenePath}");
                        Assert.That(icons.arraySize, Is.GreaterThan(index));
                        Sprite sprite = icons.GetArrayElementAtIndex(index).objectReferenceValue as Sprite;
                        Assert.That(sprite, Is.Not.Null, $"{componentTypeName}.{propertyName}[{index}] resolves");
                        Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localId), Is.True);
                        Assert.That(guid, Is.EqualTo(HomeworkGuid));
                        Assert.That(localId, Is.EqualTo(HomeworkLocalId));
                        return;
                    }
                }

                Assert.Fail($"Could not find {componentTypeName} in {scenePath}.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
