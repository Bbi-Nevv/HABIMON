#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HABIMON.Editor
{
    /// <summary>Reproducibly wires the runtime chatbot host into Parent without manual Inspector work.</summary>
    public static class ChatbotSceneInstaller
    {
        private const string ParentScene = "Assets/_Scenes/Parent.unity";
        private const string SettingsPath = "Assets/Resources/HABIMONChatbotSettings.asset";
        private const string ResourceFolder = "Assets/Resources/Chatbot";
        private const string AssetFolder = "Assets/_Art/Chatbot";

        [MenuItem("HABIMON/Chatbot/Install Parent UI")]
        public static void Install()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(ResourceFolder);
            EnsureFolder(AssetFolder);
            string referenceRoot = System.Environment.GetEnvironmentVariable("HABIMON_DEMO_ASSETS");
            if (string.IsNullOrWhiteSpace(referenceRoot))
                referenceRoot = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads", "Demo");
            InstallReferenceAsset(Path.Combine(referenceRoot, "happy 2.png"), AssetFolder + "/habi-happy.png", ResourceFolder + "/habi-happy.png");
            InstallReferenceAsset(Path.Combine(referenceRoot, "thinking 2.png"), AssetFolder + "/habi-thinking.png", ResourceFolder + "/habi-thinking.png");

            if (!AssetDatabase.LoadAssetAtPath<ChatbotRuntimeSettings>(SettingsPath))
            {
                var settings = ScriptableObject.CreateInstance<ChatbotRuntimeSettings>();
                settings.Configure("https://habimon-chat-demo-261017-0ed006.azurewebsites.net", "free", 30, false);
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            Scene scene = EditorSceneManager.OpenScene(ParentScene, OpenSceneMode.Single);
            Canvas canvas = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas != null) break;
            }
            if (canvas == null) throw new System.InvalidOperationException("Parent scene Canvas was not found.");
            ChatbotRuntimeHost host = canvas.GetComponent<ChatbotRuntimeHost>();
            if (host == null) Undo.AddComponent<ChatbotRuntimeHost>(canvas.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new System.InvalidOperationException("Parent scene could not be saved after chatbot installation.");
            AssetDatabase.SaveAssets();
            Debug.Log("HABIMON chatbot settings, reference sprites, and Parent runtime host installed.");
        }

        private static void InstallReferenceAsset(string source, string assetDestination, string resourceDestination)
        {
            if (!File.Exists(assetDestination) && !File.Exists(resourceDestination) && !File.Exists(source))
                throw new FileNotFoundException("Required local HABIMON design reference was not found.", source);
            if (!File.Exists(assetDestination))
            {
                EnsureFolder(Path.GetDirectoryName(assetDestination)?.Replace('\\', '/'));
                File.Copy(File.Exists(resourceDestination) ? resourceDestination : source, assetDestination);
            }
            if (!File.Exists(resourceDestination))
            {
                EnsureFolder(Path.GetDirectoryName(resourceDestination)?.Replace('\\', '/'));
                File.Copy(File.Exists(assetDestination) ? assetDestination : source, resourceDestination);
            }
            foreach (string destination in new[] { assetDestination, resourceDestination })
            {
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(destination) as TextureImporter;
                if (importer == null) throw new System.InvalidOperationException("Expected PNG texture: " + destination);
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
