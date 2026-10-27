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
using TMPro;

namespace HABIMON.Core.Tests
{
    public class SceneAssetIntegrityTests
    {
        private const string HomeworkGuid = "8a340e82fd915ea449d84491cd50dca6";
        private const long HomeworkLocalId = -3401245754914414353L;

        [Test]
        public void BuildScenesAndApprovalPrefabsImportWithoutMissingScripts()
        {
            foreach (string scenePath in new[] { "Assets/_Scenes/Menu.unity", "Assets/_Scenes/Parent.unity", "Assets/_Scenes/Child.unity" })
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero,
                                scenePath + ": " + child.name);
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            foreach (string path in new[] { "Assets/_Prefab/ParentConfirm.prefab", "Assets/_Prefab/ParentConfirmPopup.prefab", "Assets/_Prefab/ItemAffiliate .prefab" })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero, path + ": " + child.name);
            }
        }

        [Test]
        public void CompletionSaveSettlementRefreshesReplacementItemsAfterReload()
        {
            Type managerType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("FirebaseManager")).First(x => x != null);
            Type displayType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DisplayMission")).First(x => x != null);
            PropertyInfo instance = managerType.GetProperty("Instance");
            object previous = instance.GetValue(null);
            var managerRoot = new GameObject("SaveSettlementManager");
            var displayRoot = new GameObject("SaveSettlementDisplay");
            var toggleRoot = new GameObject("ReplacementToggle", typeof(RectTransform), typeof(Toggle));
            try
            {
                Component manager = managerRoot.AddComponent(managerType);
                instance.GetSetMethod(true).Invoke(null, new object[] { manager });
                Component display = displayRoot.AddComponent(displayType);
                BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
                displayType.GetField("subscribedManager", fields).SetValue(display, manager);
                displayType.GetField("loadedGeneration", fields).SetValue(display, managerType.GetProperty("SessionGeneration").GetValue(manager));
                displayType.GetField("habitLoadVersion", fields).SetValue(display, 2);
                displayType.GetField("hasLogSnapshot", fields).SetValue(display, true);
                Type habitLogType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("HabitLog")).First(x => x != null);
                displayType.GetField("loadedDateKey", fields).SetValue(display, habitLogType.GetMethod("GetTodayDateKey").Invoke(null, null));
                string key = (string)displayType.GetMethod("GetPendingChangeKey", fields).Invoke(display, new object[] { "habit" });
                IDictionary pending = (IDictionary)displayType.GetField("pendingChanges", fields).GetValue(display);
                pending.Add(key, true);
                object view = Activator.CreateInstance(displayType.GetNestedType("MissionItem", BindingFlags.NonPublic), true);
                Toggle toggle = toggleRoot.GetComponent<Toggle>();
                toggle.interactable = false;
                view.GetType().GetField("toggle").SetValue(view, toggle);
                ((IDictionary)displayType.GetField("visibleItems", fields).GetValue(display)).Add("habit", view);
                MethodInfo settle = displayType.GetMethod("FinishPendingChange", fields);
                Assert.That(settle, Is.Not.Null, "Settlement must reconcile replacement items rather than an obsolete request version.");
                settle.Invoke(display, new object[] { key });
                Assert.That(pending.Count, Is.Zero);
                Assert.That(toggle.interactable, Is.True, "The replacement toggle must leave the saving state after the old write settles.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(displayRoot);
                UnityEngine.Object.DestroyImmediate(managerRoot);
                UnityEngine.Object.DestroyImmediate(toggleRoot);
                instance.GetSetMethod(true).Invoke(null, new[] { previous });
            }
        }

        [Test]
        public void ParentUsesOneApprovalFlowAndItsHomeworkIconResolves()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Scenes/Parent.unity", OpenSceneMode.Additive);
            try
            {
                Assert.That(FindComponent(scene, "ParentConfirm"), Is.Not.Null);
                Assert.That(FindComponent(scene, "HabitApprovalUI"), Is.Null,
                    "Two independent approval flows must not run in the same scene.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            AssertSceneIconReferences("Assets/_Scenes/Parent.unity", "ParentConfirm", "iconSprites", 4);
        }

        [Test]
        public void MissionItemsCanSpawnWithoutAnOptionalAddButton()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("MissionUI"))
                .First(t => t != null);
            var root = new GameObject("MissionUiFixture", typeof(RectTransform));
            var prefab = new GameObject("MissionItemFixture", typeof(RectTransform));
            try
            {
                Component ui = root.AddComponent(type);
                type.GetField("contentTransform", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, root.transform);
                type.GetField("itemPrefab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, prefab);
                object habit = Activator.CreateInstance(AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("HabitModel")).First(t => t != null));
                Assert.DoesNotThrow(() => type.GetMethod("SpawnHabitItem")
                    .Invoke(ui, new object[] { habit, "habit", null, null }));
                Assert.That(root.transform.childCount, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(prefab); }
        }

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

                WeeklyChartFixture fixture = CreateWeeklyChartFixture(chart, 5);
                MethodInfo render = chart.GetType().GetMethod("RenderChart", BindingFlags.Instance | BindingFlags.NonPublic);
                Type textMeshProType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
                Assert.That(textMeshProType, Is.Not.Null);
                Assert.That(render.GetParameters().Length, Is.EqualTo(2));

                render.Invoke(chart, new object[] { fixture.days, fixture.children });
                int firstDrawChildCount = chartRoot.childCount;
                Assert.That(firstDrawChildCount, Is.EqualTo(1));
                Transform generatedChart = chartRoot.Find("WeeklyChart");
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

                foreach (RectTransform child in generatedChart.GetComponentsInChildren<RectTransform>(true))
                {
                    if (child.name == "Count" || child.name == "Date" || child.name == "Percentage" || child.name == "ChildrenLegend")
                        Assert.That(child.GetComponent(textMeshProType), Is.Not.Null, child.name + " uses concrete TMP UI text");
                }

                int firstGeneratedCount = generatedChart.GetComponentsInChildren<RectTransform>(true).Length;
                render.Invoke(chart, new object[] { fixture.days, fixture.children });
                Assert.That(chartRoot.childCount, Is.EqualTo(firstDrawChildCount));
                Assert.That(generatedChart.GetComponentsInChildren<RectTransform>(true).Length, Is.EqualTo(firstGeneratedCount));

                WeeklyChartFixture widerFixture = CreateWeeklyChartFixture(chart, 8);
                render.Invoke(chart, new object[] { widerFixture.days, widerFixture.children });
                Assert.That(chartRoot.rect.width, Is.GreaterThanOrEqualTo(62f + 7f * (8f * 34f + 16f)));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void HabitLogPanelStartsClosedAndPinPopupsAreCanvasRootOverlays()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Scenes/Parent.unity", OpenSceneMode.Additive);
            try
            {
                GameObject panel = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .First(transform => transform.name == "HabitLogPanel").gameObject;
                Assert.That(panel.activeSelf, Is.False);

                MonoBehaviour pin = FindComponent(scene, "Pin");
                Assert.That(pin, Is.Not.Null);
                var pinFields = new SerializedObject(pin);
                GameObject canvas = scene.GetRootGameObjects().First(root => root.name == "Canvas");
                foreach (string fieldName in new[] { "changePinPopup", "verifyPopup" })
                {
                    GameObject popup = pinFields.FindProperty(fieldName).objectReferenceValue as GameObject;
                    Assert.That(popup, Is.Not.Null, fieldName);
                    Assert.That(popup.activeSelf, Is.False, fieldName);
                    Assert.That(popup.transform.parent, Is.EqualTo(canvas.transform), fieldName + " is above inactive tabs");
                    RectTransform rect = popup.transform as RectTransform;
                    Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
                    Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ChildAchievementWaterSpriteResolvesTheSingleImportedSprite()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Scenes/Child.unity", OpenSceneMode.Additive);
            try
            {
                MonoBehaviour view = FindComponent(scene, "AchievementProgressView");
                Assert.That(view, Is.Not.Null);
                Sprite sprite = new SerializedObject(view).FindProperty("waterSprite").objectReferenceValue as Sprite;
                Assert.That(sprite, Is.Not.Null);
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localId), Is.True);
                Assert.That(guid, Is.EqualTo("b45c3c4a0908bcd47a81e4f5443615bd"));
                Assert.That(localId, Is.EqualTo(21300000));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void LegacyEquippedRewardItemIsMigratedWithoutUnlockingOtherRewards()
        {
            string[] legacyIds =
            {
                "c8ba19fb117924741b0b44f7c3c3f020",
                "b7c9dd92e76651f459a10d117e03748f",
                "9ccbdc7702b28884688df17190667e9b"
            };
            var keys = new List<string> { "Habimon.Equipment.0" };
            keys.AddRange(legacyIds.Select(id => "Habimon.Inventory.Unlocked." + id));
            var previous = keys.ToDictionary(key => key,
                key => (PlayerPrefs.HasKey(key), PlayerPrefs.GetString(key, string.Empty), PlayerPrefs.GetInt(key, 0)));
            Scene scene = default;

            try
            {
                PlayerPrefs.SetString("Habimon.Equipment.0", legacyIds[0]);
                foreach (string id in legacyIds)
                    PlayerPrefs.SetInt("Habimon.Inventory.Unlocked." + id, 0);
                PlayerPrefs.Save();

                scene = EditorSceneManager.OpenScene("Assets/_Scenes/Child.unity", OpenSceneMode.Additive);
                MonoBehaviour equipment = FindComponent(scene, "SaveEquipment");
                Assert.That(equipment, Is.Not.Null);
                IList legacyItems = (IList)GetPrivateField(equipment, "legacyStartingItems");
                Assert.That(legacyItems.Cast<object>().Select(GetItemId), Is.EqualTo(legacyIds));

                MethodInfo migrate = equipment.GetType().GetMethod("MigrateLegacyEquippedItems", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(migrate, Is.Not.Null);
                migrate.Invoke(equipment, null);

                string migratedKey = "Habimon.Inventory.Unlocked." + legacyIds[0];
                Assert.That(PlayerPrefs.GetInt(migratedKey, 0), Is.EqualTo(1));
                object inventory = GetPrivateField(equipment, "inventory");
                Assert.That(inventory, Is.Not.Null);
                IList ownedItems = (IList)inventory.GetType().GetProperty("Items").GetValue(inventory);
                Assert.That(ownedItems.Contains(legacyItems[0]), Is.True);
                for (int index = 1; index < legacyIds.Length; index++)
                {
                    Assert.That(PlayerPrefs.GetInt("Habimon.Inventory.Unlocked." + legacyIds[index], 0), Is.Zero);
                    Assert.That(ownedItems.Contains(legacyItems[index]), Is.False);
                }
            }
            finally
            {
                foreach (var entry in previous)
                {
                    if (!entry.Value.Item1)
                        PlayerPrefs.DeleteKey(entry.Key);
                    else if (entry.Key.StartsWith("Habimon.Equipment.", StringComparison.Ordinal))
                        PlayerPrefs.SetString(entry.Key, entry.Value.Item2);
                    else
                        PlayerPrefs.SetInt(entry.Key, entry.Value.Item3);
                }
                PlayerPrefs.Save();
                if (scene.IsValid())
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
        public void ChildPinEntryButtonKeepsFailedAttemptLockWhenPopupReopens()
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
                verifyButton.interactable = false;
                pin.GetType().GetMethod("OpenVerifyPopup").Invoke(pin, null);

                Assert.That((int)attemptsField.GetValue(pin), Is.EqualTo(3),
                    "Closing and reopening the popup must not reset the failed-attempt lock.");
                Assert.That(verifyButton.interactable, Is.False);
                Assert.That(verifyPopup.activeSelf, Is.True);
            }
            finally
            {
                if (previousScene.IsValid())
                    SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(childScene, true);
            }
        }

        [Test]
        public void ParentSceneHasAFirstPinSetupGateThatCannotBeDismissedBeforeSaving()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene parentScene = EditorSceneManager.OpenScene("Assets/_Scenes/Parent.unity", OpenSceneMode.Additive);
            try
            {
                MonoBehaviour pin = FindComponent(parentScene, "Pin");
                Assert.That(pin, Is.Not.Null);

                FieldInfo popupField = pin.GetType().GetField("changePinPopup", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo backButtonField = pin.GetType().GetField("changePinPopupBackButton", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo currentPinField = pin.GetType().GetField("currentPinInput", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo newPinField = pin.GetType().GetField("newPinInput", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo confirmPinField = pin.GetType().GetField("confirmPinInput", BindingFlags.Instance | BindingFlags.NonPublic);
                GameObject popup = (GameObject)popupField.GetValue(pin);
                Button backButton = (Button)backButtonField.GetValue(pin);
                TMP_InputField currentPin = (TMP_InputField)currentPinField.GetValue(pin);
                TMP_InputField newPin = (TMP_InputField)newPinField.GetValue(pin);
                TMP_InputField confirmPin = (TMP_InputField)confirmPinField.GetValue(pin);
                Assert.That(popup, Is.Not.Null);
                Assert.That(backButton, Is.Not.Null);
                Assert.That(popup.activeSelf, Is.False);

                pin.GetType().GetMethod("OpenInitialPinSetupGate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(pin, null);

                Assert.That(popup.activeSelf, Is.True);
                Assert.That(backButton.interactable, Is.False,
                    "The first-time setup gate must stay open until the new PIN is saved.");
                Assert.That(currentPin.gameObject.activeSelf, Is.False,
                    "First-time PIN setup must not display a current PIN field.");
                Assert.That(newPin.gameObject.activeSelf, Is.True);
                Assert.That(confirmPin.gameObject.activeSelf, Is.True);
                Assert.That(newPin.GetComponent<RectTransform>().anchoredPosition.y, Is.EqualTo(518f));
                Assert.That(confirmPin.GetComponent<RectTransform>().anchoredPosition.y, Is.EqualTo(351f));
                TMP_Text status = (TMP_Text)pin.GetType().GetField("changeStatusText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pin);
                Assert.That(status.text, Does.Contain("để trống ô PIN hiện tại"));
                Assert.That(status.fontSize, Is.EqualTo(30f));
                Assert.That(status.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
                Assert.That(status.rectTransform.sizeDelta.x, Is.GreaterThanOrEqualTo(800f));
            }
            finally
            {
                if (previousScene.IsValid())
                    SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(parentScene, true);
            }
        }

        [Test]
        public void ChatbotTextAndComposerUseStaticVietnameseFontAsset()
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("HABIMONChatbotVietnameseFont");
            Assert.That(font, Is.Not.Null, "The dedicated chatbot font must be included in Resources.");
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(font.fallbackFontAssetTable == null || font.fallbackFontAssetTable.Count == 0,
                "Chatbot text must not depend on a dynamic fallback font atlas.");
            Assert.That(font.HasCharacters("Dữ liệu HABIMON: ă â ê ô ơ ư đ ạ ế ệ ỡ ữ • – ‘ ’ →"), Is.True);

            System.Reflection.Assembly appAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp");
            Assert.That(appAssembly, Is.Not.Null);
            System.Type factory = appAssembly.GetType("ChatbotUiFactory");
            Assert.That(factory, Is.Not.Null);
            var textMethod = factory.GetMethod("Text", BindingFlags.Static | BindingFlags.NonPublic);
            var inputMethod = factory.GetMethod("Input", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(textMethod, Is.Not.Null);
            Assert.That(inputMethod, Is.Not.Null);

            var parent = new GameObject("ChatbotFontTest", typeof(RectTransform));
            try
            {
                TMP_Text text = (TMP_Text)textMethod.Invoke(null, new object[]
                {
                    "FontTest", parent.transform, "Dữ liệu", 18f, Color.white, TextAlignmentOptions.MidlineLeft
                });
                TMP_InputField input = (TMP_InputField)inputMethod.Invoke(null, new object[] { parent.transform });

                Assert.That(text.font, Is.SameAs(font));
                Assert.That(input.textComponent.font, Is.SameAs(font));
                Assert.That(((TMP_Text)input.placeholder).font, Is.SameAs(font));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
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
                SetPrivateField(state, "child", CreateNamedChildModel(id, id));
                SetPrivateField(state, "hasHabits", true);
                SetPrivateField(state, "hasLogs", true);
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
                    SetPrivateField(point, "owner", states[index]);
                    SetPrivateField(point, "total", index == 1 || index == 3 ? 0 : 1);
                    SetPrivateField(point, "completed", index == 2 ? 1 : 0);
                    SetPrivateField(point, "hasUnknownCoverage", index == 3);
                    SetPrivateField(point, "hasConflictingRecords", index == 4);
                    points.Add(point);
                }
                days.SetValue(points, day);
            }
            return new WeeklyChartFixture { days = days, children = children, childIds = childIds };
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + fieldName);
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + fieldName);
            field.SetValue(target, value);
        }

        private static object CreateNamedChildModel(string id, string name)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("ChildModel"))
                .First(found => found != null);
            object child = Activator.CreateInstance(type);
            type.GetProperty("childId").SetValue(child, id);
            type.GetProperty("name").SetValue(child, name);
            return child;
        }

        private static string GetItemId(object item)
        {
            return (string)item.GetType().GetProperty("ItemId").GetValue(item);
        }

        private static string GetText(GameObject gameObject)
        {
            TMP_Text text = gameObject.GetComponent<TMP_Text>();
            Assert.That(text, Is.Not.Null, gameObject.name + " has TMP UI text");
            return text.text;
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
