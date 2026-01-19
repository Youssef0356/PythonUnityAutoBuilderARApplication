using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using MaintainanceApp.Core.Models.Api;
using MaintainanceApp.Core.Models.AR;
using Newtonsoft.Json;

public static class MaquetteBuilderUtils
{
    // Cache for file lookups: FileName -> List of full paths
    private static Dictionary<string, List<string>> _fileCache;

    // Helper to ensure paths are always forward slashes
    private static string NormalizePath(string path) => path?.Replace("\\", "/").TrimStart('/');

    /// <summary>
    /// Clears all content in StreamingAssets folder before importing new data
    /// </summary>
    private static void ClearStreamingAssets()
    {
        string streamingAssetsPath = Application.streamingAssetsPath;
        
        if (!Directory.Exists(streamingAssetsPath))
        {
            Debug.Log("[MaquetteBuilderUtils] StreamingAssets folder does not exist, skipping clear");
            return;
        }
        
        Debug.Log("[MaquetteBuilderUtils] Clearing StreamingAssets folder...");
        
        // Delete all directories
        foreach (string dir in Directory.GetDirectories(streamingAssetsPath))
        {
            try
            {
                Directory.Delete(dir, true);
                Debug.Log($"[MaquetteBuilderUtils] Deleted directory: {Path.GetFileName(dir)}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MaquetteBuilderUtils] Failed to delete directory {dir}: {e.Message}");
            }
        }
        
        // Delete all files (except .meta files)
        foreach (string file in Directory.GetFiles(streamingAssetsPath))
        {
            if (!file.EndsWith(".meta"))
            {
                try
                {
                    File.Delete(file);
                    Debug.Log($"[MaquetteBuilderUtils] Deleted file: {Path.GetFileName(file)}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MaquetteBuilderUtils] Failed to delete file {file}: {e.Message}");
                }
            }
        }
        
        Debug.Log("[MaquetteBuilderUtils] StreamingAssets folder cleared successfully");
    }
    
    private static void BuildFileCache(string rootPath)
    {
        Debug.Log($"[MaquetteBuilderUtils] Building file index for {rootPath}...");
        _fileCache = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // Get all files recursively - ONE TIME COST
        string[] allFiles = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories);
        
        foreach (var file in allFiles)
        {
            string fileName = Path.GetFileName(file);
            if (!_fileCache.ContainsKey(fileName))
            {
                _fileCache[fileName] = new List<string>();
            }
            _fileCache[fileName].Add(file);
        }
        
        Debug.Log($"[MaquetteBuilderUtils] File index built. Indexed {allFiles.Length} files.");
    }

    public static void PrepareMaquetteAssets(string sourcePath, string maquetteName)
    {
        // WIN: Build cache once
        BuildFileCache(sourcePath);
        
        // CLEAR StreamingAssets before importing new data
        ClearStreamingAssets();
        
        // Copy all asset files
        PrepareMaquetteAssetsWithoutClear(sourcePath, maquetteName);

        AssetDatabase.Refresh();
        
        // Clear cache to free memory
        _fileCache = null;
    }

    public static void GenerateConfigForTesting(string sourcePath)
    {
        // Build cache for testing too
        BuildFileCache(sourcePath);

        // CLEAR StreamingAssets before importing new test data
        ClearStreamingAssets();
        
        // Now copy the new test maquette files
        string maquetteName = new DirectoryInfo(sourcePath).Name;
        
        // Copy all assets using the existing prepare method
        PrepareMaquetteAssetsWithoutClear(sourcePath, maquetteName);
        
        AssetDatabase.Refresh();
        Debug.Log($"[MaquetteBuilderUtils] Generated equipment_config.json for editor testing");
        
        _fileCache = null;
    }
    
    /// <summary>
    /// Internal method to prepare assets without clearing (used after manual clear)
    /// </summary>
    private static void PrepareMaquetteAssetsWithoutClear(string sourcePath, string maquetteName)
    {
        string streamingAssetsPath = Application.streamingAssetsPath;
        
        string modelsPath = Path.Combine(streamingAssetsPath, "Models");
        string mediaPath = Path.Combine(streamingAssetsPath, "Media");
        string configPath = Path.Combine(streamingAssetsPath, "equipment_config.json");

        // Ensure directories exist
        Directory.CreateDirectory(modelsPath);
        Directory.CreateDirectory(mediaPath);

        // REMOVED: Redundant QR copy. We now handle this inside TransformAndSaveConfig to ensure it matches the data path.

        // Copy Assets folder from Source to StreamingAssets/Media
        // This is a bulk copy, cache doesn't help much here as we want structure
        string sourceAssetsPath = Path.Combine(sourcePath, "Assets");
        string sourceMediaPath = Path.Combine(sourceAssetsPath, "Media");

        if (Directory.Exists(sourceMediaPath))
        {
            HashSet<string> excluded = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "video" };
            CopyDirectory(sourceMediaPath, mediaPath, excluded);
        }
        else if (Directory.Exists(sourceAssetsPath))
        {
            HashSet<string> excluded = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "video" };
            CopyDirectory(sourceAssetsPath, mediaPath, excluded);
        }

        // REMOVED: Redundant GLB copy. We now handle this inside TransformAndSaveConfig to ensure it respects the folder structure.

        // Transform JSON - Data.json to equipment_config.json
        string sourceJsonPath = Path.Combine(sourcePath, "Data.json");
        if (File.Exists(sourceJsonPath))
        {
            string jsonContent = File.ReadAllText(sourceJsonPath);
            TransformAndSaveConfig(jsonContent, configPath, maquetteName, sourcePath);
        }
        else
        {
            Debug.LogWarning("[MaquetteBuilderUtils] Data.json not found in source path.");
        }
    }

    private static void TransformAndSaveConfig(string jsonContent, string destPath, string maquetteName, string sourcePath)
    {
        // Hack: Find the first '[' and last ']' to get the array.
        int firstBracket = jsonContent.IndexOf('[');
        int lastBracket = jsonContent.LastIndexOf(']');

        if (firstBracket == -1 || lastBracket == -1)
        {
            throw new System.Exception("Invalid JSON format: Could not find array brackets.");
        }

        string arrayContent = jsonContent.Substring(firstBracket, lastBracket - firstBracket + 1);
        string wrappedJson = "{\"items\":" + arrayContent + "}";
        
        ServerDataWrapper serverData = JsonConvert.DeserializeObject<ServerDataWrapper>(wrappedJson);
        
        AppConfigWrapper appConfig = new AppConfigWrapper();
        appConfig.equipment = new List<AREquipmentFullDto>();
        string mediaPath = Path.Combine(Application.streamingAssetsPath, "Media");

        foreach (var item in serverData.items)
        {
            AREquipmentFullDto equipment = new AREquipmentFullDto();
            equipment.id = item.name.Replace(" ", "_").ToLower();
            equipment.tag = "QR_" + equipment.id.ToUpper();
            equipment.name = item.name;
            equipment.qr_image_url = FixPath(item.qr_image_url);

            // Copy QR image from source - FAST LOOKUP
                string qrSourceRel = item.qr_image_url ?? string.Empty;
                string qrFileName = Path.GetFileName(qrSourceRel);
                if (!string.IsNullOrEmpty(qrFileName))
                {
                     // Destination: StreamingAssets/Media/ModelInfos/QRCode/QRCode.png (matches FixPath)
                     // FixPath returns "Media/" + relative. 
                     // Here we want to copy the file to that location.
                     string qrDestRel = FixPath(qrSourceRel);
                     string qrFullDest = Path.Combine(Application.streamingAssetsPath, qrDestRel);
                     
                     CopyFileToDest(sourcePath, qrSourceRel, qrFullDest);
                }

            equipment.model = new ModelDataDto();
            equipment.model.id = item.name;
            equipment.model.name = item.name;
            
            // Handle Model File Copying (Structured)
            string modelSourceRel = item.modelFileUrl ?? string.Empty;
            string modelDestRel = "Models/" + NormalizePath(modelSourceRel); // e.g. Models/ModelInfos/3DMODEL/Name.glb
            equipment.model.modelFileUrl = modelDestRel;

            if (!string.IsNullOrEmpty(modelSourceRel))
            {
                CopyFileToDest(sourcePath, modelSourceRel, Path.Combine(Application.streamingAssetsPath, modelDestRel));
            }
            
            // Extract Description
            List<ServerDescription> descriptions = null;
            if (item.Description != null && item.Description.Count > 0)
            {
                descriptions = item.Description;
            }
            else if (item.parts != null && item.parts.Count > 0 && item.parts[0].description != null)
            {
                descriptions = item.parts[0].description;
            }
            
            equipment.model.descriptionItems = new List<DescriptionItem>();
            if (descriptions != null)
            {
                foreach (var desc in descriptions)
                {
                    equipment.model.descriptionItems.Add(new DescriptionItem { key = desc.key, value = desc.value });
                }
            }
            equipment.model.description = string.Join("\n", equipment.model.descriptionItems.Select(d => $"{d.key}: {d.value}"));

            // Extract Buttons
            List<ServerButton> buttons = null;
            if (item.Buttons != null && item.Buttons.Count > 0)
            {
                buttons = item.Buttons;
            }
            else if (item.parts != null && item.parts.Count > 0 && item.parts[0].buttons != null)
            {
                buttons = item.parts[0].buttons;
            }
            
            equipment.model.buttons = new List<ARButton>();
            if (buttons != null)
            {
                foreach (var btn in buttons)
                {
                    string normalizedBtnPath = (btn.imagePath ?? string.Empty).Replace("\\", "/").TrimStart('/');
                    equipment.model.buttons.Add(new ARButton
                    {
                        id = btn.name,
                        imageFileName = FixPath(btn.imagePath)
                    });
                    if (!normalizedBtnPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    {
                        CopyFileFromSource(sourcePath, btn.imagePath, mediaPath);
                    }
                }
            }
            
            // Extract video
            string videoPath = null;
            if (!string.IsNullOrEmpty(item.video))
            {
                videoPath = item.video;
            }
            else if (item.parts != null && item.parts.Count > 0 && !string.IsNullOrEmpty(item.parts[0].video))
            {
                videoPath = item.parts[0].video;
            }
            equipment.model.video = !string.IsNullOrEmpty(videoPath) ? FixPath(videoPath) : string.Empty;

            if (!string.IsNullOrEmpty(videoPath))
            {
                 // Destination: StreamingAssets/Media/ModelInfos/Video/... 
                 string videoDestRel = FixPath(videoPath);
                 string videoFullDest = Path.Combine(Application.streamingAssetsPath, videoDestRel);
                 CopyFileToDest(sourcePath, videoPath, videoFullDest);
            }
            
            // Extract datasheet
            string datasheetPath = null;
            if (!string.IsNullOrEmpty(item.datasheet))
            {
                datasheetPath = item.datasheet;
            }
            else if (item.parts != null && item.parts.Count > 0 && !string.IsNullOrEmpty(item.parts[0].datasheet))
            {
                datasheetPath = item.parts[0].datasheet;
            }
            equipment.model.datasheetUrl = datasheetPath ?? string.Empty;

            // Map parts
            if (item.parts != null && item.parts.Count > 0)
            {
                if (item.parts.Count == 1 && 
                    item.parts[0].name == item.name && 
                    item.parts[0].parts != null && 
                    item.parts[0].parts.Count > 0)
                {
                    equipment.model.parts = MapParts(item.parts[0].parts, sourcePath, mediaPath);
                }
                else
                {
                    equipment.model.parts = MapParts(item.parts, sourcePath, mediaPath);
                }
            }
            else
            {
                equipment.model.parts = new List<ModelDataDto>();
            }

            appConfig.equipment.Add(equipment);
        }

        string outputJson = JsonConvert.SerializeObject(appConfig, Formatting.Indented);
        File.WriteAllText(destPath, outputJson);
    }

    private static List<ModelDataDto> MapParts(List<ServerPart> serverParts, string sourcePath, string mediaPath)
    {
        if (serverParts == null) return new List<ModelDataDto>();
        List<ModelDataDto> parts = new List<ModelDataDto>();
        
        foreach (var sp in serverParts)
        {
            ModelDataDto p = new ModelDataDto();
            p.id = sp.name;
            p.name = sp.name;

            // Map description items
            p.descriptionItems = new List<DescriptionItem>();
            if (sp.description != null)
            {
                foreach (var desc in sp.description)
                {
                    p.descriptionItems.Add(new DescriptionItem { key = desc.key, value = desc.value });
                }
            }
            p.description = string.Join("\n", p.descriptionItems.Select(d => $"{d.key}: {d.value}"));

            p.video = !string.IsNullOrEmpty(sp.video) ? FixPath(sp.video) : string.Empty;

            if (!string.IsNullOrEmpty(sp.video))
            {
                 string videoDestRel = FixPath(sp.video);
                 string videoFullDest = Path.Combine(Application.streamingAssetsPath, videoDestRel);
                 CopyFileToDest(sourcePath, sp.video, videoFullDest);
            }

            p.datasheetUrl = sp.datasheet;

            // Map image buttons
            p.buttons = new List<ARButton>();
            if (sp.buttons != null)
            {
                foreach (var btn in sp.buttons)
                {
                    string normalizedBtnPath = (btn.imagePath ?? string.Empty).Replace("\\", "/").TrimStart('/');
                    p.buttons.Add(new ARButton
                    {
                        id = btn.name,
                        imageFileName = FixPath(btn.imagePath)
                    });
                    if (!normalizedBtnPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    {
                        CopyFileFromSource(sourcePath, btn.imagePath, mediaPath);
                    }
                }
            }
            
            p.parts = MapParts(sp.parts, sourcePath, mediaPath);
            parts.Add(p);
        }
        return parts;
    }

    private static void CopyDirectory(string sourceDir, string destDir, HashSet<string> excludedDirs = null)
    {
        DirectoryInfo dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) return;

        DirectoryInfo[] dirs = dir.GetDirectories();
        Directory.CreateDirectory(destDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string tempPath = Path.Combine(destDir, file.Name);
            file.CopyTo(tempPath, true);
        }

        foreach (DirectoryInfo subdir in dirs)
        {
            if (excludedDirs != null && excludedDirs.Contains(subdir.Name))
            {
                continue;
            }

            string tempPath = Path.Combine(destDir, subdir.Name);
            CopyDirectory(subdir.FullName, tempPath, excludedDirs);
        }
    }

    private static string FixPath(string originalPath)
    {
        if (string.IsNullOrEmpty(originalPath)) return "";

        string relative = originalPath.Replace("\\", "/");

        int assetsIndex = relative.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
        if (assetsIndex == -1)
        {
            if (relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                assetsIndex = 0;
            }
        }

        if (assetsIndex != -1)
        {
            relative = relative.Substring(assetsIndex + 7); 
        }
        else if (relative.Contains(":/") && Path.IsPathRooted(relative))
        {
            relative = Path.GetFileName(relative);
        }

        while (relative.StartsWith("/"))
        {
            relative = relative.Substring(1);
        }

        if (relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative.Substring("Assets/".Length);
        }
        if (relative.StartsWith("Media/", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative.Substring("Media/".Length);
        }

        if (relative.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            relative = "Videos/" + relative.Substring("video/".Length);
        }

        try
        {
            if (relative.Contains("/"))
            {
                string mediaRoot = Path.Combine(Application.streamingAssetsPath, "Media");
                int slashIndex = relative.IndexOf('/');
                string firstSegment = relative.Substring(0, slashIndex);
                string rest = relative.Substring(slashIndex);

                string firstSegmentPath = Path.Combine(mediaRoot, firstSegment);
                if (!Directory.Exists(firstSegmentPath))
                {
                    string normalizedFirst = firstSegment.Replace(" ", string.Empty);
                    if (!string.Equals(normalizedFirst, firstSegment, StringComparison.Ordinal))
                    {
                        string normalizedPath = Path.Combine(mediaRoot, normalizedFirst);
                        if (Directory.Exists(normalizedPath))
                        {
                            relative = normalizedFirst + rest;
                        }
                    }
                }
            }
        }
        catch { }

        return "Media/" + relative;
    }
    
    private static void CopyFileFromSource(string sourcePath, string relativePath, string destFolder)
    {
        if (string.IsNullOrEmpty(relativePath)) return;

        string normalizedRelative = relativePath.Replace("\\", "/");
        string fileName = Path.GetFileName(normalizedRelative);

        // FAST LOOKUP
        string sourceFile = null;
        string candidate = Path.Combine(sourcePath, fileName);
        if (File.Exists(candidate))
        {
            sourceFile = candidate;
        }
        else
        {
            // USE CACHE INSTEAD OF RECURSIVE SEARCH
            sourceFile = FindFileInCache(fileName);
        }

        if (string.IsNullOrEmpty(sourceFile))
        {
            return;
        }

        // Logic to determine subfolder
        string destSubDir = string.Empty;
        try
        {
            string sourceDir = Path.GetDirectoryName(sourceFile) ?? string.Empty;
            string relativeSourceDir = GetRelativePathSafe(sourcePath, sourceDir);
            string normRelativeSourceDir = (relativeSourceDir ?? string.Empty).Replace("\\", "/").TrimStart('/');

            if (!string.IsNullOrEmpty(normRelativeSourceDir))
            {
                if (normRelativeSourceDir.StartsWith("Assets/Media/", StringComparison.OrdinalIgnoreCase))
                {
                    destSubDir = normRelativeSourceDir.Substring("Assets/Media/".Length);
                }
                else if (normRelativeSourceDir.Equals("Assets/Media", StringComparison.OrdinalIgnoreCase))
                {
                    destSubDir = string.Empty;
                }
                else if (normRelativeSourceDir.StartsWith("Media/", StringComparison.OrdinalIgnoreCase))
                {
                    destSubDir = normRelativeSourceDir.Substring("Media/".Length);
                }
                else if (normRelativeSourceDir.Equals("Media", StringComparison.OrdinalIgnoreCase))
                {
                    destSubDir = string.Empty;
                }
                else
                {
                    destSubDir = normRelativeSourceDir;
                }
            }
        }
        catch
        {
            destSubDir = string.Empty;
        }

        string destDir = destFolder;
        if (!string.IsNullOrEmpty(destSubDir))
        {
            destDir = Path.Combine(destFolder, destSubDir);
        }

        try
        {
            Directory.CreateDirectory(destDir);
        }
        catch { }

        string destPath = Path.Combine(destDir, fileName);
        if (!File.Exists(destPath)) // Optimization: Don't copy if already exists (though we cleared folder)
        {
             File.Copy(sourceFile, destPath, true);
        }
    }

    private static string FindFileInCache(string fileName)
    {
        if (_fileCache != null && _fileCache.TryGetValue(fileName, out List<string> candidates))
        {
            if (candidates != null && candidates.Count > 0)
            {
                return candidates[0]; // Return first match
            }
        }
        return null;
    }

    private static string GetRelativePathSafe(string basePath, string fullPath)
    {
        if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fullPath))
        {
            return fullPath ?? string.Empty;
        }

        try
        {
            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !basePath.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                basePath += Path.DirectorySeparatorChar;
            }

            Uri baseUri = new Uri(basePath, UriKind.Absolute);
            Uri fullUri = new Uri(fullPath, UriKind.Absolute);
            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }
        catch
        {
            return fullPath;
        }
    }

    // ------------------------------------------------------------------------
    // DTOs
    // ------------------------------------------------------------------------
    
    [System.Serializable]
    private class ServerDataWrapper
    {
        public List<ServerItem> items;
    }

    [System.Serializable]
    private class ServerItem
    {
        public string name;
        public string qr_image_url;
        public string modelFileUrl;
        public string video;
        public string datasheet;
        public List<ServerDescription> Description;
        public List<ServerButton> Buttons;
        public List<ServerPart> parts;
    }

    [System.Serializable]
    private class ServerDescription
    {
        public string key;
        public string value;
    }

    [System.Serializable]
    private class ServerButton
    {
        public string name;
        public string imagePath;
    }

    [System.Serializable]
    private class ServerPart
    {
        public string name;
        public string video;
        public string datasheet;
        public List<ServerDescription> description;
        public List<ServerButton> buttons;
        public List<ServerPart> parts;
    }

    [System.Serializable]
    private class AppConfigWrapper
    {
        public List<AREquipmentFullDto> equipment;
    }

    // Rewrite CopyFileFromSource to be more generic helper
    private static void CopyFileToDest(string sourceRoot, string sourceRelPath, string fullDestPath)
    {
        if (string.IsNullOrEmpty(sourceRelPath)) return;
        
        // Find source file 
        string sourceFile = Path.Combine(sourceRoot, sourceRelPath);
        if (!File.Exists(sourceFile))
        {
            // Try cache fallback
            sourceFile = FindFileInCache(Path.GetFileName(sourceRelPath));
        }

        if (!File.Exists(sourceFile)) return;

        try 
        {
            string dir = Path.GetDirectoryName(fullDestPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            
            if (!File.Exists(fullDestPath))
            {
                File.Copy(sourceFile, fullDestPath, true);
                // Debug.Log($"[MaquetteBuilderUtils] Copied {Path.GetFileName(sourceFile)} -> {fullDestPath}");
            }
        }
        catch (Exception e)
        {
             Debug.LogWarning($"[MaquetteBuilderUtils] Failed to copy {sourceRelPath}: {e.Message}");
        }
    }
}
