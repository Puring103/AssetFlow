using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

/// <summary>
/// FolderSettings 相关的工具方法
/// </summary>
public static class FolderSettingsUtility
{
    #region FolderSettings 操作

    /// <summary>
    /// 给定一个文件夹路径，尝试获取其中的 FolderSettings
    /// </summary>
    public static FolderSettings GetFolderSettings(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath)) return null;

        string settingsPath = GetFolderSettingsPath(folderPath);
        return AssetDatabase.LoadAssetAtPath<FolderSettings>(settingsPath);
    }

    /// <summary>
    /// 获取 FolderSettings 文件在指定文件夹中的路径
    /// </summary>
    public static string GetFolderSettingsPath(string folderPath)
    {
        return Path.Combine(folderPath, FolderSettings.FileName);
    }

    /// <summary>
    /// 检查指定文件夹是否存在 FolderSettings 文件
    /// </summary>
    public static bool HasFolderSettings(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath)) return false;
        return File.Exists(GetFolderSettingsPath(folderPath));
    }

    /// <summary>
    /// 向上递归查找有效的 FolderSettings
    /// 在起始文件夹找到时直接返回，在父文件夹找到时需要 applyToSubfolders 为 true 才返回
    /// </summary>
    public static (FolderSettings settings, string settingsPath) FindFolderSettingsUpward(string startFolder)
    {
        var currentFolder = startFolder;
        bool isStartFolder = true;

        while (IsValidAssetFolder(currentFolder))
        {
            var settingsPath = GetFolderSettingsPath(currentFolder);

            if (File.Exists(settingsPath))
            {
                var settings = AssetDatabase.LoadAssetAtPath<FolderSettings>(settingsPath);
                if (settings != null)
                {
                    // 在起始文件夹找到时直接返回
                    // 在父文件夹找到时需要 applyToSubfolders 为 true
                    if (isStartFolder || settings.applyToSubfolders)
                    {
                        return (settings, settingsPath);
                    }
                }
            }

            currentFolder = Path.GetDirectoryName(currentFolder);
            isStartFolder = false;
        }

        return (null, null);
    }

    /// <summary>
    /// 在指定文件夹创建 FolderSettings
    /// </summary>
    public static FolderSettings CreateFolderSettings(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath)) return null;

        var newSettings = ScriptableObject.CreateInstance<FolderSettings>();
        string assetPath = GetFolderSettingsPath(folderPath);
        AssetDatabase.CreateAsset(newSettings, assetPath);
        AssetDatabase.SaveAssets();
        return newSettings;
    }

    #endregion

    #region Preset 操作

    /// <summary>
    /// 给定一个 FolderSettings，获取其管辖范围内的所有 Preset
    /// </summary>
    public static List<Preset> GetPresetsForFolderSettings(FolderSettings settings)
    {
        var presets = new List<Preset>();
        if (settings == null) return presets;

        string settingsPath = AssetDatabase.GetAssetPath(settings);
        if (string.IsNullOrEmpty(settingsPath)) return presets;

        string folderPath = Path.GetDirectoryName(settingsPath);
        CollectPresetsRecursively(folderPath, folderPath, settings.applyToSubfolders, presets);

        return presets;
    }

    /// <summary>
    /// 给定一个文件夹，获取其中所有的 Preset 文件（仅当前文件夹，不包含子文件夹）
    /// </summary>
    public static List<Preset> GetPresetsInFolder(string folderPath)
    {
        var presets = new List<Preset>();
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            return presets;
        }

        foreach (var file in Directory.GetFiles(folderPath, "*.preset"))
        {
            var preset = AssetDatabase.LoadAssetAtPath<Preset>(NormalizePath(file));
            if (preset != null)
            {
                presets.Add(preset);
            }
        }

        return presets;
    }

    /// <summary>
    /// 获取文件夹中的 Preset 文件路径列表（已排序）
    /// </summary>
    public static IEnumerable<string> GetPresetPathsInFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.EnumerateFiles(folderPath, "*.preset", SearchOption.TopDirectoryOnly)
            .Select(NormalizePath)
            .OrderBy(p => p);
    }

    /// <summary>
    /// 递归收集 Preset
    /// </summary>
    private static void CollectPresetsRecursively(string currentFolder, string rootFolder, bool includeSubfolders, List<Preset> presets)
    {
        if (!Directory.Exists(currentFolder)) return;

        // 如果不是根文件夹，检查是否有自己的 FolderSettings（则跳过）
        if (currentFolder != rootFolder && HasFolderSettings(currentFolder))
        {
            return;
        }

        // 收集当前文件夹的 Preset
        presets.AddRange(GetPresetsInFolder(currentFolder));

        // 如果包含子文件夹，递归处理
        if (includeSubfolders)
        {
            foreach (var subDir in Directory.GetDirectories(currentFolder))
            {
                CollectPresetsRecursively(subDir, rootFolder, true, presets);
            }
        }
    }

    #endregion

    #region 路径相关

    /// <summary>
    /// 给定一个资源文件路径，查询其所属的有效 FolderSettings
    /// 并返回从 FolderSettings 所在文件夹到资源文件所在文件夹的路径列表
    /// </summary>
    public static List<string> GetFolderSettingsAndPathForAsset(string assetPath, out FolderSettings folderSettings)
    {
        folderSettings = null;
        if (string.IsNullOrEmpty(assetPath)) return new List<string>();

        string assetFolder = GetParentFolder(assetPath);
        if (string.IsNullOrEmpty(assetFolder)) return new List<string>();

        var (settings, settingsPath) = FindFolderSettingsUpward(assetFolder);
        if (settings == null) return new List<string>();

        folderSettings = settings;
        string settingsFolder = GetParentFolder(settingsPath);

        return CollectPathFromAncestorToDescendant(settingsFolder, assetFolder);
    }

    /// <summary>
    /// 收集从祖先文件夹到后代文件夹的路径列表
    /// </summary>
    public static List<string> CollectPathFromAncestorToDescendant(string ancestorFolder, string descendantFolder)
    {
        var folders = new List<string>();
        var normalizedAncestor = NormalizePath(ancestorFolder);

        var currentFolder = descendantFolder;
        while (IsValidAssetFolder(currentFolder))
        {
            folders.Add(currentFolder);

            if (NormalizePath(currentFolder) == normalizedAncestor)
            {
                break;
            }

            currentFolder = Path.GetDirectoryName(currentFolder);
        }

        folders.Reverse();
        return folders;
    }

    /// <summary>
    /// 获取父文件夹路径
    /// </summary>
    public static string GetParentFolder(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        return NormalizePath(Path.GetDirectoryName(path));
    }

    /// <summary>
    /// 规范化路径（将反斜杠替换为正斜杠）
    /// </summary>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        return path.Replace('\\', '/');
    }

    /// <summary>
    /// 检查是否是有效的 Assets 文件夹路径
    /// </summary>
    public static bool IsValidAssetFolder(string folderPath)
    {
        return !string.IsNullOrEmpty(folderPath) && folderPath.StartsWith("Assets");
    }

    #endregion

    #region 选择和 Project Browser 相关

    /// <summary>
    /// 从当前选择获取文件夹路径
    /// </summary>
    public static string GetFolderPathFromSelection()
    {
        if (Selection.activeObject == null)
        {
            return GetProjectBrowserFolder() ?? "Assets";
        }

        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (AssetDatabase.IsValidFolder(path))
        {
            return path;
        }

        if (!string.IsNullOrEmpty(path))
        {
            return GetParentFolder(path);
        }

        return GetProjectBrowserFolder() ?? "Assets";
    }

    /// <summary>
    /// 获取 Project 面板当前浏览的文件夹
    /// </summary>
    public static string GetProjectBrowserFolder()
    {
        var projectBrowserType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
        if (projectBrowserType == null) return null;

        var projectBrowser = EditorWindow.GetWindow(projectBrowserType, false, null, false);
        if (projectBrowser == null) return null;

        var folderMethod = projectBrowserType.GetMethod("GetActiveFolderPath", BindingFlags.NonPublic | BindingFlags.Instance);
        return folderMethod?.Invoke(projectBrowser, null) as string;
    }

    #endregion
}
