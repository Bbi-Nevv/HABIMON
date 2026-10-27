using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HABIMON.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HABIMON.Core.Tests
{
    public sealed class ChatbotSceneIntegrationTests
    {
        [Test]
        public void ParentSceneContainsRunnableChatbotHostAndFiveUsableNavigationEntries()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene parent = EditorSceneManager.OpenScene("Assets/_Scenes/Parent.unity", OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(parent);
                MonoBehaviour host = FindComponent(parent, "ChatbotRuntimeHost");
                Assert.That(host, Is.Not.Null, "The chatbot host must be wired into the Parent scene.");
                Transform taskbar = FindTransform(parent, "Canvas/Taskbar");
                Assert.That(taskbar, Is.Not.Null);
                Button[] originalTabs = taskbar.GetComponentsInChildren<Button>(true);
                int[] originalPersistentCalls = originalTabs.Select(button => button.onClick.GetPersistentEventCount()).ToArray();
                MethodInfo initialize = host.GetType().GetMethod("InitializeParentScene", BindingFlags.NonPublic | BindingFlags.Instance);
                initialize.Invoke(host, null);
                Transform chatbotTab = taskbar.Find("ChatbotTab");
                Assert.That(chatbotTab, Is.Not.Null);
                Transform settingsTab = taskbar.Find("Settings");
                Assert.That(settingsTab, Is.Not.Null);
                Assert.That(chatbotTab.GetSiblingIndex(), Is.EqualTo(settingsTab.GetSiblingIndex() - 1),
                    "The chatbot tab is placed immediately before Settings.");
                Assert.That(chatbotTab.GetComponent<Button>(), Is.Not.Null);
                Component label = chatbotTab.GetComponentsInChildren<Component>(true)
                    .First(component => component != null && component.GetType().Name == "TextMeshProUGUI");
                Assert.That(label.GetType().GetProperty("text").GetValue(label), Is.EqualTo("Trợ lý"));
                Assert.That(taskbar.childCount, Is.EqualTo(5), "Existing four navigation entries remain alongside chat.");
                for (int index = 0; index < originalTabs.Length; index++)
                    Assert.That(originalTabs[index].onClick.GetPersistentEventCount(), Is.EqualTo(originalPersistentCalls[index]),
                        "Installing chat must preserve each existing tab UnityEvent.");

                Transform screen = FindTransform(parent, "Canvas/ChatbotScreen");
                Assert.That(screen, Is.Not.Null);
                Assert.That(FindTransform(parent, "Canvas/ChatbotScreen/ConversationBody/MessageScroll"), Is.Not.Null);
                Assert.That(FindTransform(parent, "Canvas/ChatbotScreen/Composer/MessageInput"), Is.Not.Null);
                Assert.That(FindTransform(parent, "Canvas/ChatbotScreen/HistoryPanel"), Is.Not.Null);
                Assert.That(FindTransform(parent, "Canvas/ChatbotScreen/DeleteConversationDialog"), Is.Not.Null);
                Assert.That(FindTransform(parent, "Canvas/ChatbotScreen/ChildPickerPanel"), Is.Not.Null);
                Assert.That(FindTransform(parent, "Canvas/ChatbotScreen/Header/TitleGroup/ChildSelectorButton")
                    .GetComponent<Button>(), Is.Not.Null, "Parents can choose the supported child from chat.");
                Assert.That(FindTransform(parent, "Canvas/ChatbotScreen/ConversationBody/EmptyState/WelcomeDescription"), Is.Null,
                    "The redundant welcome description is removed.");
                Assert.That(FindTransform(parent, "Canvas/ChatbotScreen/Composer/SendButton"), Is.Not.Null);
                Assert.That(FindTransform(parent, "Canvas/ChatbotScreen/Composer/StopButton"), Is.Null,
                    "The composer exposes Send only; there is no user-facing stop/cancel action.");
                Assert.That(FindTransform(parent, "Canvas/ChatbotScreen/Composer/CancelButton"), Is.Null);
                Button send = FindTransform(parent, "Canvas/ChatbotScreen/Composer/SendButton").GetComponent<Button>();
                Component historyLabel = FindTransform(parent, "Canvas/ChatbotScreen/Header/HistoryButton")
                    .GetComponentsInChildren<Component>(true).First(component =>
                        component != null && component.GetType().Name == "TextMeshProUGUI");
                Component newLabel = FindTransform(parent, "Canvas/ChatbotScreen/Header/NewConversationButton")
                    .GetComponentsInChildren<Component>(true).First(component =>
                        component != null && component.GetType().Name == "TextMeshProUGUI");
                PropertyInfo wrapping = historyLabel.GetType().GetProperty("textWrappingMode");
                Assert.That(wrapping.GetValue(historyLabel).ToString(), Is.EqualTo("NoWrap"));
                Assert.That(wrapping.GetValue(newLabel).ToString(), Is.EqualTo("NoWrap"));
                LayoutElement inputLayout = FindTransform(parent, "Canvas/ChatbotScreen/Composer/MessageInput")
                    .GetComponent<LayoutElement>();
                LayoutElement sendLayout = send.GetComponent<LayoutElement>();
                Assert.That(inputLayout.flexibleWidth, Is.GreaterThan(0f), "The composer spans its available width.");
                Assert.That(inputLayout.preferredHeight, Is.EqualTo(sendLayout.preferredHeight).Within(0.01f),
                    "An empty composer matches the reduced Send button height.");
                MethodInfo setControls = host.GetType().GetMethod("SetControlsEnabled", BindingFlags.NonPublic | BindingFlags.Instance);
                setControls.Invoke(host, new object[] { false });
                Assert.That(send.interactable, Is.False, "Send locks while an operation is active.");
                setControls.Invoke(host, new object[] { true });
                Assert.That(send.interactable, Is.False, "Send stays locked until a child profile is selected.");

                Type settingsType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("ChatbotRuntimeSettings"))
                    .First(type => type != null);
                UnityEngine.Object config = AssetDatabase.LoadAssetAtPath("Assets/Resources/HABIMONChatbotSettings.asset", settingsType);
                Assert.That(config, Is.Not.Null);
                Assert.That(settingsType.GetProperty("BaseUrl").GetValue(config), Does.StartWith("https://"));
                Assert.That(settingsType.GetProperty("Plan").GetValue(config), Is.EqualTo("free"));

                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Chatbot/habi-happy.png"), Is.Not.Null);
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Chatbot/habi-thinking.png"), Is.Not.Null);
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(parent, true);
            }
        }

        [Test]
        public void ChatOwnerCredentialIsStablePerUidAndIsolatedAcrossAccounts()
        {
            Type store = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("ChatbotCredentialStore"))
                .First(type => type != null);
            MethodInfo getOrCreate = store.GetMethod("GetOrCreate", BindingFlags.Public | BindingFlags.Static);
            string accountA = "synthetic-chatbot-parent-a";
            string accountB = "synthetic-chatbot-parent-b";
            string tokenA = (string)getOrCreate.Invoke(null, new object[] { accountA });
            string tokenAAgain = (string)getOrCreate.Invoke(null, new object[] { accountA });
            string tokenB = (string)getOrCreate.Invoke(null, new object[] { accountB });

            Assert.That(tokenA.Length, Is.GreaterThanOrEqualTo(40));
            Assert.That(tokenA, Is.EqualTo(tokenAAgain));
            Assert.That(tokenB, Is.Not.EqualTo(tokenA));
            Assert.That(tokenA, Does.Not.Contain(accountA));
            Assert.That(tokenB, Does.Not.Contain(accountB));
        }

        private static MonoBehaviour FindComponent(Scene scene, string typeName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null && component.GetType().Name == typeName) return component;
            return null;
        }

        private static Transform FindTransform(Scene scene, string path)
        {
            string[] names = path.Split('/');
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != names[0]) continue;
                Transform current = root.transform;
                for (int index = 1; index < names.Length && current != null; index++)
                    current = current.Find(names[index]);
                if (current != null) return current;
            }
            return null;
        }
    }
}
