using System.IO;
using StickerHunt;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildIos
{
    private const string ScenePath = "Assets/Scenes/StickerHunt.unity";
    private const string OutputPath = "Build/iOS";

    [MenuItem("tagtag/Build iOS")]
    public static void Build()
    {
        EnsureScene();
        PlayerSettings.companyName = "tagtag";
        PlayerSettings.productName = "tagtag";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.kenk.tagtag");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        AssetDatabase.SaveAssets();

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputPath,
            target = BuildTarget.iOS,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded ||
            !File.Exists(Path.Combine(OutputPath, "Unity-iPhone.xcodeproj/project.pbxproj")))
        {
            throw new System.Exception("iOS export did not produce an Xcode project: " + report.summary.result);
        }
        Debug.Log("iOS Xcode project exported to " + Path.GetFullPath(OutputPath));
    }

    private static void EnsureScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        cameraObject.GetComponent<Camera>().backgroundColor = new Color32(15, 21, 37, 255);
        new GameObject("tagtag", typeof(StickerHuntScreen));
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}
