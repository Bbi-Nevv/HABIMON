using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace HABIMON.Editor
{
    internal static class ChatbotVietnameseFontAssetBuilder
    {
        private const string SourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        private const string OutputPath = "Assets/Resources/HABIMONChatbotVietnameseFont.asset";
        private const int AtlasSize = 2048;

        [MenuItem("HABIMON/Build Chatbot Vietnamese Font Asset")]
        public static void Build()
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
                throw new InvalidOperationException("Could not load chatbot source font at " + SourceFontPath);

            FontEngine.InitializeFontEngine();
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 86, 9,
                GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            if (fontAsset == null)
                throw new InvalidOperationException("TextMeshPro could not create the chatbot font asset.");

            uint[] characters = BuildCharacterSet();
            if (!fontAsset.TryAddCharacters(characters, out uint[] missing, true))
            {
                string missingText = missing == null ? "unknown" : string.Join(", ", Array.ConvertAll(missing, value => "U+" + value.ToString("X4")));
                UnityEngine.Object.DestroyImmediate(fontAsset);
                throw new InvalidOperationException("Chatbot font is missing required Unicode characters: " + missingText);
            }

            fontAsset.name = "HABIMON Chatbot Vietnamese SDF";
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath) != null)
                AssetDatabase.DeleteAsset(OutputPath);

            AssetDatabase.CreateAsset(fontAsset, OutputPath);
            foreach (Texture2D atlas in fontAsset.atlasTextures)
            {
                if (atlas != null)
                    AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }

            if (fontAsset.material != null)
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Created static chatbot Vietnamese font asset with " + characters.Length + " supported characters at " + OutputPath);
        }

        private static uint[] BuildCharacterSet()
        {
            var characters = new HashSet<uint>();
            AddRange(characters, 0x20, 0x7E);
            AddRange(characters, 0xA0, 0xFF);
            AddRange(characters, 0x1EA0, 0x1EF9);

            uint[] vietnameseBaseLetters =
            {
                0x0102, 0x0103, // Ăă
                0x0110, 0x0111, // Đđ
                0x0128, 0x0129, // Ĩĩ
                0x0168, 0x0169, // Ũũ
                0x01A0, 0x01A1, // Ơơ
                0x01AF, 0x01B0  // Ưư
            };
            foreach (uint value in vietnameseBaseLetters)
                characters.Add(value);

            uint[] commonPunctuation =
            {
                0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D,
                0x2022, 0x2026, 0x20AC, 0x2122, 0x2192
            };
            foreach (uint value in commonPunctuation)
                characters.Add(value);

            var result = new uint[characters.Count];
            characters.CopyTo(result);
            Array.Sort(result);
            return result;
        }

        private static void AddRange(HashSet<uint> target, uint first, uint last)
        {
            for (uint value = first; value <= last; value++)
                target.Add(value);
        }
    }
}
