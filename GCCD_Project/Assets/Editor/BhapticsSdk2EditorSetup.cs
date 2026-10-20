using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GCCD.Editor
{
    /// <summary>
    /// Asset Store SDK2 remains vendor-owned; this workflow only detects and references
    /// the genuine plugin imported by the developer. No App ID or API key is stored.
    /// </summary>
    public static class BhapticsSdk2EditorSetup
    {
        private const string Symbol = "GCCD_BHAPTICS_SDK2";
        private const string PrefabFolder = "Assets/Bhaptics/SDK2/Prefabs";

        private static bool HasSdkLibrary()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetType("Bhaptics.SDK2.BhapticsLibrary", false) != null) return true;
            return false;
        }

        private static GameObject FindInitializerPrefab()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && string.Equals(asset.name, "[bhaptics]", StringComparison.OrdinalIgnoreCase))
                    return asset;
            }
            return null;
        }

        // Called during scene generation; an SDK prefab is never fabricated.
        public static void AddInitializerToCurrentScene()
        {
            var prefab = FindInitializerPrefab();
            if (prefab == null)
            {
                Debug.Log("GCCD_SDK2_NOT_IMPORTED: official [bhaptics] prefab unavailable; simulated haptics only.");
                return;
            }
            if (!HasSdkLibrary())
            {
                Debug.LogWarning("GCCD_SDK2_INCOMPLETE: prefab found, but Bhaptics.SDK2.BhapticsLibrary is missing.");
                return;
            }
            if (GameObject.Find("[bhaptics]") == null)
            {
                PrefabUtility.InstantiatePrefab(prefab);
                Debug.Log("GCCD_SDK2_PREFAB_PLACED: SDK prefab initialized by official plugin.");
            }
        }

        [MenuItem("GCCD/bHaptics SDK2/Check Official SDK and Add Prefab")]
        public static void CheckAndAdd()
        {
            if (!HasSdkLibrary() || FindInitializerPrefab() == null)
            {
                EditorUtility.DisplayDialog("GCCD · bHaptics SDK2",
                    "Import the official bHaptics Haptic Plugin from Unity Asset Store first. " +
                    "Required: Assets/Bhaptics/SDK2/Prefabs/[bhaptics] and Bhaptics.SDK2.BhapticsLibrary. " +
                    "Then use bHaptics > Developer Window to link a deployed app with its App ID and API key. " +
                    "GCCD does not save your API credentials.", "OK");
                return;
            }
            AddInitializerToCurrentScene();
            EditorUtility.DisplayDialog("GCCD · bHaptics SDK2",
                "Official SDK2 detected. Initialization prefab added if missing. " +
                "Device model and motor-array layout must still be verified independently. " +
                "Physical trial modes remain locked until verified.", "OK");
        }

        [MenuItem("GCCD/bHaptics SDK2/Enable Compile-Time Adapter")]
        public static void EnableSdkAdapter()
        {
            if (!HasSdkLibrary() || FindInitializerPrefab() == null)
            {
                EditorUtility.DisplayDialog("SDK2 is not imported",
                    "First import the official bHaptics SDK2 Unity package and verify the initializer prefab.", "OK");
                return;
            }
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            string symbols = PlayerSettings.GetScriptingDefineSymbols(target);
            var tokens = symbols.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (!tokens.Contains(Symbol))
            {
                tokens.Add(Symbol);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", tokens));
            }
            Debug.Log("GCCD_SDK2_ADAPTER_COMPILATION_ENABLED. Not a claim of connected or verified physical X40 output.");
        }

        [MenuItem("GCCD/bHaptics SDK2/Disable Compile-Time Adapter")]
        public static void DisableSdkAdapter()
        {
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            var entries = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(entry => entry != Symbol)
                .ToArray();
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", entries));
            Debug.Log("GCCD_SDK2_ADAPTER_COMPILATION_DISABLED");
        }
    }
}
