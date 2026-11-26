using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Build APK Android yang bisa langsung di-install di HP.
/// - Dari editor: menu Tools > Build > Build APK Android
/// - Dari terminal (Unity tertutup):
///   Unity -batchmode -quit -projectPath . -buildTarget Android
///         -executeMethod BuildApk.BuildFromCommandLine -logFile Builds/build.log
/// Hasil: Builds/ARPolbeng.apk (ditandatangani debug keystore bila keystore custom tidak diset).
/// </summary>
public static class BuildApk
{
    const string OutputDir = "Builds";
    const string ApkName = "ARPolbeng.apk";

    [MenuItem("Tools/Build/Build APK Android")]
    public static void BuildFromMenu()
    {
        var ok = Build(out var path);
        EditorUtility.DisplayDialog("Build APK", ok ? "Selesai:\n" + Path.GetFullPath(path) : "Build gagal, lihat Console.", "OK");
    }

    public static void BuildFromCommandLine()
    {
        EditorApplication.Exit(Build(out _) ? 0 : 1);
    }

    static bool Build(out string apkPath)
    {
        Directory.CreateDirectory(OutputDir);
        apkPath = Path.Combine(OutputDir, ApkName);

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.buildAppBundle = false; // .apk, bukan .aab

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Debug.Log($"[BuildApk] {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)} v{PlayerSettings.bundleVersion} " +
                  $"(code {PlayerSettings.Android.bundleVersionCode}) | API: {ApiConfig.BaseUrl} | scenes: {string.Join(", ", scenes)}");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.CleanBuildCache, // build bersih: hindari data scene rusak dari cache lama
        });

        var s = report.summary;
        Debug.Log($"[BuildApk] Result={s.result} Errors={s.totalErrors} Size={s.totalSize / (1024f * 1024f):F1} MB Time={s.totalTime} Output={s.outputPath}");
        return s.result == BuildResult.Succeeded;
    }
}
