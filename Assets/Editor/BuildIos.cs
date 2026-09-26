using System;
using System.Collections.Generic;
using System.IO;
using Tagtag;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEditor.iOS.Xcode;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

public static class BuildIos
{
    private const string ScenePath = "Assets/Scenes/Tagtag.unity";
    private const string OutputPath = "Build/iOS";
    private const string AppName = "tagtag";
    private const string AppIconPath = "Assets/Tagtag/Branding/AppIcon.png";
    private const string CameraUsageDescription = "tagtag uses your camera to place and discover AR stickers around you.";
    private const string LocationUsageDescription = "tagtag uses your location to show nearby stickers and confirm you’re close enough to place or collect them.";

    [MenuItem("tagtag/Configure App Metadata")]
    public static void ConfigureAppMetadata()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
        if (icon == null)
            throw new InvalidOperationException("App icon is missing at " + AppIconPath);

        PlayerSettings.statusBarHidden = false;
        PlayerSettings.companyName = AppName;
        PlayerSettings.productName = AppName;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.kenk.tagtag");
        PlayerSettings.iOS.cameraUsageDescription = CameraUsageDescription;
        PlayerSettings.iOS.locationUsageDescription = LocationUsageDescription;
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
            foreach (var slot in icons)
                slot.SetTexture(icon);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("tagtag/Prepare ARKit")]
    public static void PrepareArKit()
    {
        ConfigureAppMetadata();
        var ids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
        XRGeneralSettingsPerBuildTarget settings;
        if (ids.Length == 0)
        {
            Directory.CreateDirectory("Assets/XR");
            settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(settings, "Assets/XR/TagtagXRSettings.asset");
        }
        else settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(ids[0]));
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
        if (!settings.HasManagerSettingsForBuildTarget(BuildTargetGroup.iOS))
            settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.iOS);
        var manager = settings.ManagerSettingsForBuildTarget(BuildTargetGroup.iOS);
        if (!XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.ARKit.ARKitLoader", BuildTargetGroup.iOS))
            throw new InvalidOperationException("Could not assign the ARKit loader to iOS.");
        manager.automaticLoading = true;
        manager.automaticRunning = true;
        const string arKitDefine = "UNITY_XR_ARKIT_LOADER_ENABLED";
        var defines = new List<string>(PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS)
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
        if (!defines.Contains(arKitDefine))
        {
            defines.Add(arKitDefine);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, string.Join(";", defines));
        }
        var serialized = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
        serialized.FindProperty("activeInputHandler").intValue = 2;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("tagtag/Build iOS")]
    public static void Build()
    {
        PrepareArKit();
#if !UNITY_XR_ARKIT_LOADER_ENABLED
        throw new InvalidOperationException("ARKit was just configured. Run BuildIos.Build in a new Unity invocation so its native build processor is compiled.");
#endif
        EnsureScene();
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
            throw new InvalidOperationException("iOS export did not produce an Xcode project: " + report.summary.result);
        }
        Debug.Log("iOS Xcode project exported to " + Path.GetFullPath(OutputPath));
    }

    private static void EnsureScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (UnityEngine.Object.FindFirstObjectByType<TagtagApplication>() == null)
        {
            new GameObject("tagtag", typeof(TagtagApplication));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    [PostProcessBuild(995)]
    public static void ConfigureNativeProject(BuildTarget target, string output)
    {
        if (target != BuildTarget.iOS) return;
        var path = PBXProject.GetPBXProjectPath(output);
        var project = new PBXProject();
        project.ReadFromFile(path);
        var arKitLibraries = Path.Combine(output, "Libraries/com.unity.xr.arkit");
        if (PlayerSettings.iOS.sdkVersion != iOSSdkVersion.SimulatorSDK &&
            (!Directory.Exists(arKitLibraries) ||
                Directory.GetFiles(arKitLibraries, "libUnityARKit.a", SearchOption.AllDirectories).Length == 0 ||
                !File.ReadAllText(path).Contains("libUnityARKit.a") ||
                !File.ReadAllText(path).Contains("ARKit.framework")))
            throw new InvalidOperationException("ARKit native libraries are missing. Run BuildIos.PrepareArKit in a separate Unity invocation before BuildIos.Build.");
        var main = project.GetUnityMainTargetGuid();
        var framework = project.GetUnityFrameworkTargetGuid();
        project.AddFrameworkToProject(framework, "MapKit.framework", false);
        project.AddFrameworkToProject(framework, "AVFoundation.framework", false);
        project.AddFrameworkToProject(framework, "CoreLocation.framework", false);
        project.AddFrameworkToProject(framework, "AuthenticationServices.framework", false);
        project.AddFrameworkToProject(framework, "Security.framework", false);
        project.SetBuildProperty(main, "DEVELOPMENT_TEAM", "5Y6QUA9GA6");
        project.SetBuildProperty(framework, "CLANG_ENABLE_MODULES", "YES");
        // MapKit lives above Unity's renderer, so bundle the same die-cut artwork for UIKit.
        var mapResources = Path.Combine(output, "TagtagMapResources");
        Directory.CreateDirectory(mapResources);
        for (var pose = 1; pose <= 4; pose++)
        {
            var filename = "taggi-" + pose + ".png";
            File.Copy(Path.Combine("Assets/Resources/Tagtag/Presets", filename), Path.Combine(mapResources, filename), true);
            var resourceGuid = project.AddFile("TagtagMapResources/" + filename, "TagtagMapResources/" + filename, PBXSourceTree.Source);
            project.AddFileToBuild(main, resourceGuid);
        }
        const string clusterFont = "InstrumentSemibold.ttf";
        File.Copy(Path.Combine("Assets/Resources/Tagtag/Fonts", clusterFont), Path.Combine(mapResources, clusterFont), true);
        var fontGuid = project.AddFile("TagtagMapResources/" + clusterFont, "TagtagMapResources/" + clusterFont, PBXSourceTree.Source);
        project.AddFileToBuild(main, fontGuid);
        project.WriteToFile(path);

        var infoPath = Path.Combine(output, "Info.plist");
        var info = new PlistDocument();
        info.ReadFromFile(infoPath);
        info.root.SetString("CFBundleDisplayName", AppName);
        info.root.SetString("NSCameraUsageDescription", CameraUsageDescription);
        info.root.SetString("NSLocationWhenInUseUsageDescription", LocationUsageDescription);
        info.root.SetBoolean("UIRequiresFullScreen", true);
        info.root.SetBoolean("UIStatusBarHidden", false);
        info.root.SetBoolean("UIViewControllerBasedStatusBarAppearance", true);
        info.root.SetString("UIStatusBarStyle", "UIStatusBarStyleDarkContent");
        var bundledFonts = info.root.values.ContainsKey("UIAppFonts")
            ? info.root["UIAppFonts"].AsArray() : info.root.CreateArray("UIAppFonts");
        bundledFonts.AddString(clusterFont);
        var googleScheme = Environment.GetEnvironmentVariable("TAGTAG_GOOGLE_REVERSED_CLIENT_ID");
        if (string.IsNullOrWhiteSpace(googleScheme))
        {
            const string configPath = "Assets/Resources/Tagtag/ServiceConfiguration.json";
            if (File.Exists(configPath))
            {
                try
                {
                    var config = JsonUtility.FromJson<ServiceConfiguration>(File.ReadAllText(configPath));
                    googleScheme = config?.googleReversedClientId;
                }
                catch (Exception) { Debug.LogWarning("tagtag service configuration could not be read for iOS URL schemes."); }
            }
        }
        if (!string.IsNullOrWhiteSpace(googleScheme))
        {
            var urls = info.root.values.ContainsKey("CFBundleURLTypes")
                ? info.root["CFBundleURLTypes"].AsArray() : info.root.CreateArray("CFBundleURLTypes");
            urls.AddDict().CreateArray("CFBundleURLSchemes").AddString(googleScheme.Trim());
        }
        info.WriteToFile(infoPath);
        var capabilities = new ProjectCapabilityManager(path, "Tagtag.entitlements", null, main);
        capabilities.AddSignInWithApple();
        capabilities.WriteToFile();
    }
}
