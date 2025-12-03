using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 导入器模板工具类
/// </summary>
public static class ImporterTemplateUtility
{
    /// <summary>
    /// 获取template生效范围内的所有资源文件
    /// </summary>
    /// <param name="template">模板对象</param>
    /// <returns>受影响的资源对象列表</returns>
    public static List<UnityEngine.Object> GetAffectedAssets<T>(ImporterTemplate<T> template) where T : AssetImporter
    {
        var affectedAssets = new List<UnityEngine.Object>();
        
        if (template == null)
            return affectedAssets;
        
        // 获取template所在的文件夹路径
        string templatePath = AssetDatabase.GetAssetPath(template);
        if (string.IsNullOrEmpty(templatePath))
            return affectedAssets;
        
        string templateFolder = Path.GetDirectoryName(templatePath);
        
        // 获取该类型template对应的资源文件扩展名
        var extensions = GetExtensionsForImporterType(typeof(T));
        
        // 收集符合条件的资源路径
        var assetPaths = new List<string>();
        CollectAssetsInFolder(templateFolder, extensions, template.includeSubfolders, typeof(T), assetPaths);
        
        // 将路径转换为资源对象
        foreach (var path in assetPaths)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null)
            {
                affectedAssets.Add(asset);
            }
        }
        
        return affectedAssets;
    }
    
    /// <summary>
    /// 获取指定资源文件所属的template（泛型版本）
    /// </summary>
    /// <typeparam name="T">导入器类型</typeparam>
    /// <param name="assetPath">资源路径</param>
    /// <returns>该资源对应的template，如果没有则返回null</returns>
    public static ImporterTemplate<T> GetTemplateForAsset<T>(string assetPath) where T : AssetImporter
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;
        
        // 标准化路径（统一使用正斜杠）
        assetPath = assetPath.Replace("\\", "/");
        
        // 获取资源的导入器
        var importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null || !(importer is T))
            return null;
        
        Type importerType = typeof(T);
        
        // 从资源所在文件夹开始，逐级向上查找template
        string currentFolder = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        string assetsFolder = "Assets";
        
        while (!string.IsNullOrEmpty(currentFolder) && currentFolder.StartsWith(assetsFolder))
        {
            // 在当前文件夹查找匹配类型的template
            var template = FindTemplateInFolderGeneric<T>(currentFolder);
            
            if (template != null)
            {
                // 标准化 template 路径
                string templatePath = AssetDatabase.GetAssetPath(template)?.Replace("\\", "/");
                string templateFolder = Path.GetDirectoryName(templatePath)?.Replace("\\", "/");
                string assetFolder = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
                
                // 如果template在资源的直接父文件夹，直接返回
                if (templateFolder == assetFolder)
                {
                    return template;
                }
                
                // 如果template在上级文件夹，需要检查includeSubfolders
                if (template.includeSubfolders)
                {
                    // 还需要确保在资源和template之间没有其他同类型的template
                    if (!HasTemplateInBetween(assetPath, templateFolder, importerType))
                    {
                        return template;
                    }
                }
            }
            
            // 向上移动到父文件夹
            currentFolder = Path.GetDirectoryName(currentFolder)?.Replace("\\", "/");
        }
        
        return null;
    }
    
    /// <summary>
    /// 获取指定资源文件所属的template（非泛型版本，自动推断类型）
    /// </summary>
    /// <param name="assetPath">资源路径</param>
    /// <returns>该资源对应的template，如果没有则返回null</returns>
    public static ScriptableObject GetTemplateForAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;
        
        // 获取资源的导入器类型
        var importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
            return null;
        
        // 根据导入器类型调用相应的泛型方法
        if (importer is TextureImporter)
        {
            return GetTemplateForAsset<TextureImporter>(assetPath);
        }
        else if (importer is ModelImporter)
        {
            return GetTemplateForAsset<ModelImporter>(assetPath);
        }
        else if (importer is AudioImporter)
        {
            return GetTemplateForAsset<AudioImporter>(assetPath);
        }
        
        return null;
    }
    
    /// <summary>
    /// 在文件夹中收集符合条件的资源文件
    /// </summary>
    private static void CollectAssetsInFolder(string folderPath, string[] extensions, bool includeSubfolders, 
        Type templateType, List<string> results)
    {
        if (!Directory.Exists(folderPath))
            return;
        
        // 收集当前文件夹中符合类型的资源
        foreach (var extension in extensions)
        {
            string[] files = Directory.GetFiles(folderPath, $"*{extension}");
            foreach (var file in files)
            {
                string assetPath = file.Replace("\\", "/");
                results.Add(assetPath);
            }
        }
        
        // 如果需要递归子文件夹
        if (includeSubfolders)
        {
            string[] subFolders = Directory.GetDirectories(folderPath);
            foreach (var subFolder in subFolders)
            {
                string subFolderPath = subFolder.Replace("\\", "/");
                
                // 检查子文件夹中是否有同类型的template
                if (!HasTemplateOfType(subFolderPath, templateType))
                {
                    // 如果没有，继续递归
                    CollectAssetsInFolder(subFolderPath, extensions, true, templateType, results);
                }
                // 如果有同类型template，则跳过该文件夹及其子文件夹
            }
        }
    }
    
    /// <summary>
    /// 检查文件夹中是否存在指定类型的template
    /// </summary>
    private static bool HasTemplateOfType(string folderPath, Type templateType)
    {
        if (templateType == typeof(TextureImporter))
        {
            return FindAssetInFolder<TextureImporterTemplate>(folderPath) != null;
        }
        else if (templateType == typeof(ModelImporter))
        {
            return FindAssetInFolder<ModelImporterTemplate>(folderPath) != null;
        }
        else if (templateType == typeof(AudioImporter))
        {
            return FindAssetInFolder<AudioImporterTemplate>(folderPath) != null;
        }
        
        return false;
    }
    
    /// <summary>
    /// 在文件夹中查找指定类型的template（泛型版本）
    /// </summary>
    private static ImporterTemplate<T> FindTemplateInFolderGeneric<T>(string folderPath) where T : AssetImporter
    {
        Type importerType = typeof(T);
        
        if (importerType == typeof(TextureImporter))
        {
            return FindAssetInFolder<TextureImporterTemplate>(folderPath) as ImporterTemplate<T>;
        }
        else if (importerType == typeof(ModelImporter))
        {
            return FindAssetInFolder<ModelImporterTemplate>(folderPath) as ImporterTemplate<T>;
        }
        else if (importerType == typeof(AudioImporter))
        {
            return FindAssetInFolder<AudioImporterTemplate>(folderPath) as ImporterTemplate<T>;
        }
        
        return null;
    }
    
    /// <summary>
    /// 在文件夹中查找指定类型的template
    /// </summary>
    private static ScriptableObject FindTemplateInFolder(string folderPath, Type importerType)
    {
        if (importerType == typeof(TextureImporter))
        {
            return FindAssetInFolder<TextureImporterTemplate>(folderPath);
        }
        else if (importerType == typeof(ModelImporter))
        {
            return FindAssetInFolder<ModelImporterTemplate>(folderPath);
        }
        else if (importerType == typeof(AudioImporter))
        {
            return FindAssetInFolder<AudioImporterTemplate>(folderPath);
        }
        
        return null;
    }
    
    /// <summary>
    /// 在文件夹中查找指定类型的资源
    /// </summary>
    private static T FindAssetInFolder<T>(string folderPath) where T : ScriptableObject
    {
        if (!Directory.Exists(folderPath))
            return null;
        
        // 标准化文件夹路径
        folderPath = folderPath?.Replace("\\", "/");
        
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folderPath });
        
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid)?.Replace("\\", "/");
            string assetFolder = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            
            // 确保资源就在当前文件夹，而不是子文件夹
            if (assetFolder == folderPath)
            {
                return AssetDatabase.LoadAssetAtPath<T>(assetPath);
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 检查template是否启用了includeSubfolders
    /// </summary>
    private static bool HasIncludeSubfolders(ScriptableObject template)
    {
        if (template is TextureImporterTemplate textureTemplate)
        {
            return textureTemplate.includeSubfolders;
        }
        else if (template is ModelImporterTemplate modelTemplate)
        {
            return modelTemplate.includeSubfolders;
        }
        else if (template is AudioImporterTemplate audioTemplate)
        {
            return audioTemplate.includeSubfolders;
        }
        
        return false;
    }
    
    /// <summary>
    /// 检查在资源和template之间是否存在其他同类型的template
    /// </summary>
    private static bool HasTemplateInBetween(string assetPath, string templateFolder, Type importerType)
    {
        // 标准化路径
        assetPath = assetPath?.Replace("\\", "/");
        templateFolder = templateFolder?.Replace("\\", "/");
        
        string currentFolder = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        
        while (!string.IsNullOrEmpty(currentFolder) && currentFolder != templateFolder)
        {
            // 检查当前文件夹是否有同类型template
            if (HasTemplateOfType(currentFolder, importerType))
            {
                return true;
            }
            
            currentFolder = Path.GetDirectoryName(currentFolder)?.Replace("\\", "/");
        }
        
        return false;
    }
    
    /// <summary>
    /// 根据导入器类型获取对应的文件扩展名
    /// </summary>
    private static string[] GetExtensionsForImporterType(Type importerType)
    {
        if (importerType == typeof(TextureImporter))
        {
            return new[] { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tiff", ".bmp", ".gif", ".exr" };
        }
        else if (importerType == typeof(ModelImporter))
        {
            return new[] { ".fbx", ".obj", ".dae", ".blend", ".3ds", ".max", ".mb", ".ma" };
        }
        else if (importerType == typeof(AudioImporter))
        {
            return new[] { ".mp3", ".wav", ".ogg", ".aiff", ".aif", ".mod", ".it", ".s3m", ".xm" };
        }
        
        return new string[0];
    }
}

