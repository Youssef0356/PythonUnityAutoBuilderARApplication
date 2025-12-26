using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using MaintainanceApp.Core.Models.Api; // For App DTOs
using MaintainanceApp.Core.Models.AR; // For ARButton, DescriptionItem

public class AutomatedBuilder
{
    private const bool UsePerformanceOptimizedSettings = true;
    // ------------------------------------------------------------------------
    // CLI Entry Point
    // ------------------------------------------------------------------------
    public static void BuildFromCommandLine()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        string maquettePath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-maquettePath" && i + 1 < args.Length)
            {
                maquettePath = args[i + 1];
                break;
            }
        }

        if (string.IsNullOrEmpty(maquettePath))
        {
            Debug.LogError("[AutomatedBuilder] Error: -maquettePath argument is missing.");
            EditorApplication.Exit(1);
            return;
        }

        if (!Directory.Exists(maquettePath))
        {
            Debug.LogError($"[AutomatedBuilder] Error: Maquette path does not exist: {maquettePath}");
            EditorApplication.Exit(1);
            return;
        }

        BuildMaquette(maquettePath);
    }

    // ------------------------------------------------------------------------
    // Menu Entry Point (For testing in Editor)
    // ------------------------------------------------------------------------
    [MenuItem("MaintenanceAR/Build Maquette APK...")]
    public static void BuildFromMenu()
    {
        string path = EditorUtility.OpenFolderPanel("Select Maquette Data Folder", "", "");
        if (!string.IsNullOrEmpty(path))
        {
            BuildMaquette(path);
        }
    }

    // ------------------------------------------------------------------------
    // Core Build Logic
    // ------------------------------------------------------------------------
    public static void BuildMaquette(string sourcePath)
    {
        string maquetteName = new DirectoryInfo(sourcePath).Name;
        Debug.Log($"[AutomatedBuilder] Starting build for: {maquetteName}");

        try
        {
            // 1. Prepare Assets (Inject Data) using shared utility
            try 
            {
                AssetDatabase.StartAssetEditing();
                MaquetteBuilderUtils.PrepareMaquetteAssets(sourcePath, maquetteName);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // 2. Update Project Settings
            UpdateProjectSettings(maquetteName);

            // 3. Build immediately
            Debug.Log($"[AutomatedBuilder] Asset import complete. Starting APK build...");
            PerformBuild(maquetteName);
            Debug.Log($"[AutomatedBuilder] Build Success: {maquetteName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AutomatedBuilder] Build Failed: {e.Message}\n{e.StackTrace}");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }

    // ------------------------------------------------------------------------
    // Step 2: Update Project Settings
    // ------------------------------------------------------------------------
    private static void UpdateProjectSettings(string maquetteName)
    {
        // Only switch if we are not already on Android to save time/re-imports
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log("[AutomatedBuilder] Switching active build target to Android...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }
        
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

        PlayerSettings.productName = maquetteName;
        PlayerSettings.applicationIdentifier = $"com.maintenance.{maquetteName.Replace(" ", "").Replace("_", "").ToLower()}";
        
        // Enforce critical Android settings for AR
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)29; // Vuforia requires 10.0+ (API Level 29)
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        
        // Disable Vulkan if present, enforce OpenGLES3
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new UnityEngine.Rendering.GraphicsDeviceType[] { 
            UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 
        });
        
        // Ensure we are allowed to use unsafe code if needed (often needed for some plugins)
        PlayerSettings.allowUnsafeCode = true;
        if (UsePerformanceOptimizedSettings)
        {
            PlayerSettings.SetMobileMTRendering(UnityEditor.Build.NamedBuildTarget.Android, true);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.antiAliasing = 0;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowDistance = 15f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.globalTextureMipmapLimit = 1;
            QualitySettings.vSyncCount = 0;
        }
    }

    // ------------------------------------------------------------------------
    // Step 3: Perform Build
    // ------------------------------------------------------------------------
    private static void PerformBuild(string maquetteName)
    {
        string[] scenes = new string[]
        {
            "Assets/Features/MainMenu/Views/MainMenu.unity",
            "Assets/Features/AR/Views/ARScene.unity",
            "Assets/Features/Parameters/Views/ParametreRA.unity"
        };

        var existingScenes = scenes.Where(s => File.Exists(s)).ToArray();
        
        if (existingScenes.Length == 0)
        {
            throw new System.Exception("No valid scenes found to build!");
        }
        
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = existingScenes;
        buildPlayerOptions.locationPathName = $"{maquetteName}.apk";
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.options = BuildOptions.Development | BuildOptions.AllowDebugging;

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[AutomatedBuilder] Build succeeded: {summary.totalSize} bytes");
        }

        if (summary.result == BuildResult.Failed)
        {
            throw new System.Exception($"Build failed with {summary.totalErrors} errors.");
        }
    }
}
