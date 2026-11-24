using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Menu: Tools > Build > Siapkan Pengaturan Android
/// Menyetel identitas aplikasi untuk APK.
/// </summary>
public static class AndroidBuildSetup
{
    const string PackageName = "com.polbeng.arpolbeng";

    [MenuItem("Tools/Build/Siapkan Pengaturan Android")]
    public static void Apply()
    {
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
        AssetDatabase.SaveAssets();
        string msg = $"Package name: {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}\n" +
                     $"Versi: {PlayerSettings.bundleVersion} (code {PlayerSettings.Android.bundleVersionCode})\n" +
                     $"Min SDK: {PlayerSettings.Android.minSdkVersion}\n" +
                     $"Arsitektur: {PlayerSettings.Android.targetArchitectures}\n" +
                     $"Server API: {ApiConfig.BaseUrl}";
        Debug.Log("[Build] " + msg.Replace("\n", " | "));
        EditorUtility.DisplayDialog("Pengaturan Android", msg, "OK");
    }
}
