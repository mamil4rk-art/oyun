using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Oyunu bilgisayarda denemek için kısayollar:
///  - PC Shop → Oyunu Editörde Oyna : prototip sahnesini açıp Play'e basar.
///  - PC Shop → Windows İçin Derle (.exe) : Builds/Windows klasörüne çalıştırılabilir oyun üretir.
/// </summary>
public static class WindowsBuild
{
    const string ScenePath = "Assets/Scenes/PrototypeScene.unity";
    const string ExeName = "PC Dukkani Simulatoru.exe";

    [MenuItem("PC Shop/Oyunu Editörde Oyna", false, 100)]
    static void PlayInEditor()
    {
        if (!SceneOk()) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("PC Shop/Windows İçin Derle (.exe)", false, 101)]
    static void BuildWindows()
    {
        if (!SceneOk()) return;

        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
        {
            EditorUtility.DisplayDialog("Windows modülü yok",
                "Unity Hub → Installs → 6000.6 → ayar (dişli) → Add modules → " +
                "\"Windows Build Support (Mono)\" kutusunu işaretleyip kurun, sonra tekrar deneyin.", "Tamam");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Proje klasörünün yanındaki Builds/Windows (git'e girmez)
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Windows"));
        Directory.CreateDirectory(dir);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = Path.Combine(dir, ExeName),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        var sum = report.summary;

        if (sum.result == BuildResult.Succeeded)
        {
            Debug.Log($"[Derleme] Bitti: {sum.outputPath} ({sum.totalSize / (1024 * 1024)} MB, {sum.totalTime:mm\\:ss})");
            EditorUtility.RevealInFinder(sum.outputPath);
            EditorUtility.DisplayDialog("Derleme bitti",
                "Oyun hazır!\n\n" + sum.outputPath +
                "\n\nAçılan klasördeki \"" + ExeName + "\" dosyasına çift tıklayın.\n" +
                "Başkasına verecekseniz Windows klasörünün tamamını (zip'leyip) verin; .exe tek başına çalışmaz.", "Tamam");
        }
        else
        {
            EditorUtility.DisplayDialog("Derleme başarısız",
                $"Sonuç: {sum.result}, {sum.totalErrors} hata.\nConsole'daki kırmızı satırları kopyalayıp Claude'a yapıştırın.", "Tamam");
        }
    }

    static bool SceneOk()
    {
        if (File.Exists(Path.Combine(Application.dataPath, "..", ScenePath))) return true;
        EditorUtility.DisplayDialog("Sahne yok",
            "Önce üst menüden PC Shop → Prototip Sahnesini Kur'a tıklayın.", "Tamam");
        return false;
    }
}
