using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

// One-time migration uses AssetDatabase so the shared assets keep their serialized GUIDs.
public static class SilentProjectMaintenance
{
    [MenuItem("GCCD/Silent Study/Install Cached bHaptics SDK2")]
    public static void InstallSdk()
    {
        string package = Environment.GetEnvironmentVariable("GCCD_BHAPTICS_PACKAGE");
        if (string.IsNullOrEmpty(package)) package = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library/Unity/Asset Store-5.x/bHaptics Inc/ScriptingIntegration/bHaptics Haptic Plugin.unitypackage");
        if (!File.Exists(package)) throw new FileNotFoundException("Download bHaptics Haptic Plugin from My Assets, or set GCCD_BHAPTICS_PACKAGE", package);
        AssetDatabase.importPackageCompleted += Imported;
        AssetDatabase.ImportPackage(package, false);
    }
    static void Imported(string name)
    {
        AssetDatabase.importPackageCompleted -= Imported;
        AssetDatabase.DeleteAsset("Assets/Bhaptics/SDK2/Examples");
        EnableSdk();
    }
    [MenuItem("GCCD/Silent Study/Enable Installed SDK2")]
    public static void EnableSdk()
    {
        if (!File.Exists("Assets/Bhaptics/SDK2/Scripts/Core/BhapticsSDK2.cs")) throw new FileNotFoundException("Import bHaptics SDK2 first");
        const string source = "Assets/_Project/SilentResearch/Integration/BhapticsX40SdkBackend.cs";
        const string template = source + ".txt";
        if (File.Exists(source))
        {
            string error = AssetDatabase.MoveAsset(source, template);
            if (error != "") throw new Exception(error);
        }
        // SDK calls compile only inside the locally installed SDK. The repository template remains
        // readable when the licensed SDK is absent, even if this machine's define is still enabled.
        const string target = "Assets/Bhaptics/SDK2/GCCDIntegration/BhapticsX40SdkBackend.cs";
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        File.Copy(template, target, true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string symbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
        if (!symbols.Split(';').Contains("GCCD_BHAPTICS_SDK2")) PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, symbols + ";GCCD_BHAPTICS_SDK2");
        AssetDatabase.SaveAssets();
        UnityEngine.Debug.Log("bHaptics SDK2 enabled locally; credentials excluded from Git.");
    }
}
