using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 导入器模板工具类
/// </summary>
public static class ImporterTemplateUtility
{
    // 导入器类型到模板类型的映射（不再需要扩展名）
    private static readonly Dictionary<Type, Type> ImporterToTemplateMap = new()
    {
        { typeof(TextureImporter), typeof(TextureImporterTemplate) },
        { typeof(ModelImporter), typeof(ModelImporterTemplate) },
        { typeof(AudioImporter), typeof(AudioImporterTemplate) }
    };

    /// <summary>
    /// 获取template生效范围内的所有资源文件（优化版：直接判断资源类型，无需扩展名）
    /// </summary>
    public static List<UnityEngine.Object> GetAffectedAssets<T>(ImporterTemplate<T> template) where T : AssetImporter
    {
        var affectedAssets = new List<UnityEngine.Object>();
        if (template == null) return affectedAssets;

        var templatePath = AssetDatabase.GetAssetPath(template);
        if (string.IsNullOrEmpty(templatePath)) return affectedAssets;

        var templateFolder = NormalizePath(Path.GetDirectoryName(templatePath));
        var importerType = typeof(T);

        // 递归收集资源
        CollectAffectedAssets(templateFolder, importerType, template.includeSubfolders, affectedAssets);
        return affectedAssets;
    }

    private static void CollectAffectedAssets(string folderPath, Type importerType, bool includeSubfolders,
        List<UnityEngine.Object> results)
    {
        if (!Directory.Exists(folderPath)) return;

        // 获取当前文件夹所有文件，使用Unity判断类型
        foreach (var file in Directory.GetFiles(folderPath))
        {
            if (file.EndsWith(".meta")) continue;

            var assetPath = NormalizePath(file);
            var importer = AssetImporter.GetAtPath(assetPath);

            if (importer != null && importer.GetType() == importerType)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset != null) results.Add(asset);
            }
        }

        // 递归子文件夹（如果需要且子文件夹没有同类型模板）
        if (includeSubfolders)
        {
            foreach (var subFolder in Directory.GetDirectories(folderPath))
            {
                var subFolderPath = NormalizePath(subFolder);
                if (!HasTemplateOfType(subFolderPath, importerType))
                {
                    CollectAffectedAssets(subFolderPath, importerType, true, results);
                }
            }
        }
    }

    /// <summary>
    /// 获取指定资源文件所属的template（泛型版本 - 优化版）
    /// </summary>
    public static ImporterTemplate<T> GetTemplateForAsset<T>(string assetPath) where T : AssetImporter
    {
        if (string.IsNullOrEmpty(assetPath)) return null;

        assetPath = NormalizePath(assetPath);
        var importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null || !(importer is T)) return null;

        var assetFolder = NormalizePath(Path.GetDirectoryName(assetPath));
        var currentFolder = assetFolder;
        var importerType = typeof(T);

        // 从资源所在文件夹向上遍历，查找最近的模板
        while (!string.IsNullOrEmpty(currentFolder) && currentFolder.StartsWith("Assets"))
        {
            var template = FindTemplateInFolder<T>(currentFolder);
            if (template != null)
            {
                var templateFolder = NormalizePath(Path.GetDirectoryName(AssetDatabase.GetAssetPath(template)));

                // 检查双向关系：
                // 1. 如果template在资源的同一文件夹，直接返回
                // 2. 如果template在父文件夹，必须满足：
                //    - template支持子文件夹（includeSubfolders=true）
                //    - 资源和template之间没有其他同类型的template
                if (templateFolder == assetFolder)
                {
                    return template;
                }
                else if (template.includeSubfolders && !HasTemplateInBetween(assetPath, templateFolder, importerType))
                {
                    return template;
                }
            }

            currentFolder = NormalizePath(Path.GetDirectoryName(currentFolder));
        }

        return null;
    }

    /// <summary>
    /// 获取指定资源文件所属的template（非泛型版本 - 优化版）
    /// </summary>
    public static ScriptableObject GetTemplateForAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;

        var importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null) return null;

        // 直接类型匹配，避免反射开销
        if (importer is TextureImporter) return GetTemplateForAsset<TextureImporter>(assetPath);
        if (importer is ModelImporter) return GetTemplateForAsset<ModelImporter>(assetPath);
        if (importer is AudioImporter) return GetTemplateForAsset<AudioImporter>(assetPath);

        return null;
    }

    /// <summary>
    /// 检查是否支持该导入器类型
    /// </summary>
    public static bool IsSupportedImporter(AssetImporter importer)
    {
        return importer != null && ImporterToTemplateMap.ContainsKey(importer.GetType());
    }

    /// <summary>
    /// 获取导入器对应的模板类型
    /// </summary>
    public static Type GetTemplateType(Type importerType)
    {
        return ImporterToTemplateMap.TryGetValue(importerType, out var templateType) ? templateType : null;
    }

    // ============ 私有辅助方法 ============

    private static bool HasTemplateOfType(string folderPath, Type importerType)
    {
        if (!ImporterToTemplateMap.TryGetValue(importerType, out var templateType)) return false;
        return FindAssetInFolder(folderPath, templateType) != null;
    }

    private static ImporterTemplate<T> FindTemplateInFolder<T>(string folderPath) where T : AssetImporter
    {
        if (!ImporterToTemplateMap.TryGetValue(typeof(T), out var templateType)) return null;
        return FindAssetInFolder(folderPath, templateType) as ImporterTemplate<T>;
    }

    private static ScriptableObject FindAssetInFolder(string folderPath, Type assetType)
    {
        if (!Directory.Exists(folderPath)) return null;

        folderPath = NormalizePath(folderPath);
        var guids = AssetDatabase.FindAssets($"t:{assetType.Name}", new[] { folderPath });

        foreach (var guid in guids)
        {
            var assetPath = NormalizePath(AssetDatabase.GUIDToAssetPath(guid));
            // 确保资源在当前文件夹，不在子文件夹
            if (NormalizePath(Path.GetDirectoryName(assetPath)) == folderPath)
            {
                return AssetDatabase.LoadAssetAtPath(assetPath, assetType) as ScriptableObject;
            }
        }

        return null;
    }

    private static bool HasTemplateInBetween(string assetPath, string templateFolder, Type importerType)
    {
        var currentFolder = NormalizePath(Path.GetDirectoryName(assetPath));
        templateFolder = NormalizePath(templateFolder);

        while (!string.IsNullOrEmpty(currentFolder) && currentFolder != templateFolder)
        {
            if (HasTemplateOfType(currentFolder, importerType)) return true;
            currentFolder = NormalizePath(Path.GetDirectoryName(currentFolder));
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        return path?.Replace("\\", "/");
    }
}