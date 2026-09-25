using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

public static class BuildIosSimulator
{
    private const string ScenePath = "Assets/Scenes/Tagtag.unity";
    private const string OutputPath = "Build/iOSSimulator";
    private const string ReviewMarker = ".tagtag-simulator-review";
    private const string ArKitLoaderType = "UnityEngine.XR.ARKit.ARKitLoader";
    private const string ArKitDefine = "UNITY_XR_ARKIT_LOADER_ENABLED";
    private const string FaceTrackingDefine = "UNITY_XR_ARKIT_FACE_TRACKING_ENABLED";

    public static void Prepare()
    {
        RequireReviewProject();
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            throw new InvalidOperationException("Select iOS as the active Unity build target before preparing the simulator project.");

        BuildIos.ConfigureAppMetadata();
        var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.iOS);
        var manager = general?.AssignedSettings;
        if (manager == null)
            throw new InvalidOperationException("The copied project has no iOS XR manager settings.");

        foreach (var loader in new List<XRLoader>(manager.activeLoaders))
        {
            if (loader != null && loader.GetType().FullName == ArKitLoaderType && !manager.TryRemoveLoader(loader))
                throw new InvalidOperationException("Could not remove the ARKit loader from the simulator project.");
        }
        general.InitManagerOnStart = false;
        manager.automaticLoading = false;
        manager.automaticRunning = false;

        var defines = new List<string>(PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS)
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
        defines.RemoveAll(value => value == ArKitDefine || value == FaceTrackingDefine);
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, string.Join(";", defines));
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
        PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        EditorUtility.SetDirty(general);
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
        Debug.Log("tagtag simulator project prepared; start a new Unity invocation for export.");
    }

    public static void Build()
    {
        RequireReviewProject();
#if UNITY_XR_ARKIT_LOADER_ENABLED
        throw new InvalidOperationException("The simulator player still has the ARKit loader define. Run Prepare, then start a new Unity invocation.");
#endif
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS ||
            PlayerSettings.iOS.sdkVersion != iOSSdkVersion.SimulatorSDK ||
            PlayerSettings.iOS.simulatorSdkArchitecture != AppleMobileArchitectureSimulator.ARM64)
            throw new InvalidOperationException("Prepare the arm64 iOS Simulator settings before export.");
        var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.iOS);
        if (general?.AssignedSettings == null)
            throw new InvalidOperationException("The simulator project has no iOS XR manager settings.");
        foreach (var loader in general.AssignedSettings.activeLoaders)
            if (loader != null && loader.GetType().FullName == ArKitLoaderType)
                throw new InvalidOperationException("The simulator project still has an ARKit loader.");
        if (!File.Exists(Path.Combine(Application.dataPath, "Scenes/Tagtag.unity")))
            throw new FileNotFoundException("The tagtag startup scene is missing.", ScenePath);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputPath,
            target = BuildTarget.iOS,
            options = BuildOptions.Development
        });
        string projectPath = Path.Combine(OutputPath, "Unity-iPhone.xcodeproj/project.pbxproj");
        if (report.summary.result != BuildResult.Succeeded || !File.Exists(projectPath))
            throw new InvalidOperationException("iOS Simulator export failed: " + report.summary.result);

        string project = File.ReadAllText(projectPath);
        if (project.Contains("libUnityARKit.a") || project.Contains("UnityARKit.m"))
            throw new InvalidOperationException("The simulator export still links ARKit device binaries.");
        foreach (string name in new[] { "TagtagIdentity.mm", "TagtagMap.mm" })
            if (!project.Contains(name))
                throw new InvalidOperationException("The simulator export is missing the native bridge " + name);
        Debug.Log("tagtag iOS Simulator Xcode project exported to " + Path.GetFullPath(OutputPath));
    }

    private static void RequireReviewProject()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!projectRoot.StartsWith("/private/tmp/", StringComparison.Ordinal) ||
            !File.Exists(Path.Combine(projectRoot, ReviewMarker)))
            throw new InvalidOperationException("Simulator builds must run in a marked review project under /private/tmp.");
    }
}
