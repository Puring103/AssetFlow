using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

public class HumanParkAssetImporter : AssetPostprocessor
{
    void OnPreprocessAsset()
    {
        // 过滤条件：确保资源路径以 "Assets/" 开头，排除脚本、预设和FolderSettings
        if (assetPath.StartsWith("Assets/")
            && !assetPath.EndsWith(".cs")
            && !assetPath.EndsWith(".preset")
            && !assetPath.Contains(FolderSettings.FileName))
        {
            var assetFolder = Path.GetDirectoryName(assetPath);
            // 递归查找FolderSettings并应用preset
            FindSettingsAndApplyPresetsRecursively(assetFolder, assetFolder);
        }
    }

    /// <summary>
    /// 递归查找FolderSettings并应用preset
    /// 先向上递归查找FolderSettings，然后在返回时按从父到子的顺序应用preset
    /// </summary>
    bool FindSettingsAndApplyPresetsRecursively(string currentFolder, string assetFolder)
    {
        // 递归终止条件
        if (!FolderSettingsUtility.IsValidAssetFolder(currentFolder))
            return false;

        bool isAssetFolder = FolderSettingsUtility.NormalizePath(currentFolder) ==
                                FolderSettingsUtility.NormalizePath(assetFolder);

        // 检查当前文件夹是否有FolderSettings
        FolderSettings settings = null;
        if (FolderSettingsUtility.HasFolderSettings(currentFolder))
        {
            settings = FolderSettingsUtility.GetFolderSettings(currentFolder);
        }

        if (settings != null)
        {
            // 找到了FolderSettings
            bool isApplicable = isAssetFolder || settings.applyToSubfolders;

            if (isApplicable)
            {
                // FolderSettings适用，运行校验器并应用当前文件夹的preset
                RunValidators(settings);
                ApplyPresetsFromFolder(currentFolder);
                return true;
            }
            else
            {
                // FolderSettings不适用于子文件夹，只应用资源文件夹的preset
                if (isAssetFolder)
                {
                    ApplyPresetsFromFolder(currentFolder);
                }
                return false;
            }
        }
        else
        {
            // 没找到FolderSettings，继续向上递归
            bool foundValidSettings = FindSettingsAndApplyPresetsRecursively(
                Path.GetDirectoryName(currentFolder), assetFolder);

            if (foundValidSettings)
            {
                // 父级找到了有效的FolderSettings，应用当前文件夹的preset（从父到子）
                ApplyPresetsFromFolder(currentFolder);
            }
            else if (isAssetFolder)
            {
                // 没有找到有效的FolderSettings，只应用资源文件夹的preset
                ApplyPresetsFromFolder(currentFolder);
            }

            return foundValidSettings;
        }
    }

    /// <summary>
    /// 从指定文件夹应用预设
    /// </summary>
    void ApplyPresetsFromFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;

        // 使用工具类获取预设路径
        foreach (var presetPath in FolderSettingsUtility.GetPresetPathsInFolder(folder))
        {
            var preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);
            if (preset != null)
            {
                preset.ApplyTo(assetImporter);
            }
        }
    }

    /// <summary>
    /// 运行校验器
    /// </summary>
    void RunValidators(FolderSettings folderSettings)
    {
        var path = assetPath;
        EditorApplication.delayCall += () =>
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset != null && !folderSettings.Validate(asset, out var errorMessages))
            {
                foreach (var error in errorMessages)
                {
                    Debug.LogError($"[资源校验失败] {path}: {error}");
                }
            }
        };
    }
}
