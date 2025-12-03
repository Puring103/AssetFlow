using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental;
using UnityEngine;

/// <summary>
/// 自动应用ImporterTemplate的资源修改处理器
/// 当资源发生变化时，自动查找并应用对应的ImporterTemplate设置
/// </summary>
public class AutoImporterTemplateProcessor : AssetsModifiedProcessor
{
    /// <summary>
    /// 当资源被修改时调用
    /// </summary>
    protected override void OnAssetsModified(string[] changedAssets, string[] addedAssets, string[] deletedAssets, AssetMoveInfo[] movedAssets)
    {
        // 收集所有需要处理的资源路径
        var assetsToProcess = new HashSet<string>();
        // 收集所有变化的模板
        var templatesChanged = new HashSet<ScriptableObject>();

        // 合并所有需要检查的资源
        var allModifiedAssets = new List<string>();
        if (addedAssets != null) allModifiedAssets.AddRange(addedAssets);
        if (changedAssets != null) allModifiedAssets.AddRange(changedAssets);

        // 处理新增和修改的资源
        foreach (var assetPath in allModifiedAssets)
        {
            if (string.IsNullOrEmpty(assetPath))
                continue;

            // 检查是否是模板文件
            if (IsTemplateAsset(assetPath))
            {
                var template = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (template != null)
                {
                    templatesChanged.Add(template);
                    Debug.Log($"[AutoImporterTemplate] 检测到 Template 变化: {assetPath}");
                }
            }
            // 否则检查是否是需要处理的普通资源
            else if (ShouldProcessAsset(assetPath))
            {
                assetsToProcess.Add(assetPath);
            }
        }

        // 处理移动的资源
        if (movedAssets != null && movedAssets.Length > 0)
        {
            foreach (var moveInfo in movedAssets)
            {
                // 检查移动的是否是模板文件
                if (IsTemplateAsset(moveInfo.destinationAssetPath))
                {
                    var template = AssetDatabase.LoadAssetAtPath<ScriptableObject>(moveInfo.destinationAssetPath);
                    if (template != null)
                    {
                        templatesChanged.Add(template);
                        Debug.Log($"[AutoImporterTemplate] 检测到 Template 移动: {moveInfo.destinationAssetPath}");
                    }
                }
                // 资源移动后，需要检查新位置是否有对应的模板
                else if (ShouldProcessAsset(moveInfo.destinationAssetPath))
                {
                    assetsToProcess.Add(moveInfo.destinationAssetPath);
                }
            }
        }

        // 如果有模板发生变化，将模板应用到所有受影响的资源
        if (templatesChanged.Count > 0)
        {
            ProcessTemplateChanges(templatesChanged);
        }

        // 处理普通资源的变化，应用对应的模板
        if (assetsToProcess.Count > 0)
        {
            ProcessAssetChanges(assetsToProcess);
        }
    }

    /// <summary>
    /// 判断是否是模板资源
    /// </summary>
    private bool IsTemplateAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".asset"))
            return false;

        return assetPath.Contains("ImporterTemplate");
    }

    /// <summary>
    /// 判断资源是否需要处理
    /// </summary>
    private bool ShouldProcessAsset(string assetPath)
    {
        // 跳过空路径
        if (string.IsNullOrEmpty(assetPath))
            return false;

        // 跳过模板文件本身
        if (IsTemplateAsset(assetPath))
            return false;

        // 跳过 meta 文件
        if (assetPath.EndsWith(".meta"))
            return false;

        // 检查是否是支持的资源类型
        var importer = AssetImporter.GetAtPath(assetPath);
        return ImporterTemplateUtility.IsSupportedImporter(importer);
    }

    /// <summary>
    /// 处理模板变化，将模板应用到所有受影响的资源
    /// </summary>
    private void ProcessTemplateChanges(HashSet<ScriptableObject> templates)
    {
        int totalProcessed = 0;
        int totalSuccess = 0;

        foreach (var template in templates)
        {
            if (template == null)
                continue;

            try
            {
                // 获取受该模板影响的所有资源
                var affectedAssets = GetAffectedAssetsFromTemplate(template);
                if (affectedAssets == null || affectedAssets.Count == 0)
                {
                    Debug.Log($"[AutoImporterTemplate] 模板 {template.name} 没有受影响的资源");
                    continue;
                }

                Debug.Log($"[AutoImporterTemplate] 模板 {template.name} 影响 {affectedAssets.Count} 个资源，开始应用...");

                int successCount = 0;
                foreach (var asset in affectedAssets)
                {
                    var assetPath = AssetDatabase.GetAssetPath(asset);
                    var importer = AssetImporter.GetAtPath(assetPath);
                    
                    if (importer != null && ApplyTemplate(template, importer, assetPath))
                    {
                        importer.SaveAndReimport();
                        successCount++;
                        totalSuccess++;
                    }
                    totalProcessed++;
                }

                Debug.Log($"[AutoImporterTemplate] 模板 {template.name} 成功应用到 {successCount}/{affectedAssets.Count} 个资源");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AutoImporterTemplate] 处理模板失败 [{template.name}]: {e.Message}");
            }
        }

        if (totalProcessed > 0)
        {
            Debug.Log($"[AutoImporterTemplate] 模板变化处理完成: {totalSuccess}/{totalProcessed} 个资源成功应用");
        }
    }

    /// <summary>
    /// 处理资源变化，应用对应的模板
    /// </summary>
    private void ProcessAssetChanges(HashSet<string> assetPaths)
    {
        int successCount = 0;
        int failCount = 0;

        foreach (var assetPath in assetPaths)
        {
            try
            {
                // 查找对应的模板
                var template = ImporterTemplateUtility.GetTemplateForAsset(assetPath);
                if (template == null)
                    continue;

                // 获取资源的导入器
                var importer = AssetImporter.GetAtPath(assetPath);
                if (importer == null)
                    continue;

                // 应用模板设置
                if (ApplyTemplate(template, importer, assetPath))
                {
                    // 保存并重新导入资源
                    importer.SaveAndReimport();
                    successCount++;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AutoImporterTemplate] 处理资源失败 [{assetPath}]: {e.Message}");
                failCount++;
            }
        }

        // 输出处理结果统计
        if (successCount > 0)
        {
            Debug.Log($"[AutoImporterTemplate] 资源变化: 成功应用模板到 {successCount} 个资源");
        }
        if (failCount > 0)
        {
            Debug.LogWarning($"[AutoImporterTemplate] 资源变化: {failCount} 个资源处理失败");
        }
    }

    /// <summary>
    /// 获取模板影响的所有资源
    /// </summary>
    private List<UnityEngine.Object> GetAffectedAssetsFromTemplate(ScriptableObject template)
    {
        // 使用反射调用泛型方法，或者根据具体类型调用
        if (template is TextureImporterTemplate textureTemplate)
        {
            return textureTemplate.AffectedAssetPaths;
        }
        else if (template is AudioImporterTemplate audioTemplate)
        {
            return audioTemplate.AffectedAssetPaths;
        }
        else if (template is ModelImporterTemplate modelTemplate)
        {
            return modelTemplate.AffectedAssetPaths;
        }

        return new List<UnityEngine.Object>();
    }

    /// <summary>
    /// 应用模板设置到导入器
    /// </summary>
    private bool ApplyTemplate(ScriptableObject template, AssetImporter importer, string assetPath)
    {
        if (template == null || importer == null)
            return false;

        try
        {
            // 根据导入器类型应用对应的模板
            if (importer is TextureImporter textureImporter && 
                template is TextureImporterTemplate textureTemplate)
            {
                if (textureTemplate.Importer != null)
                {
                    EditorUtility.CopySerialized(textureTemplate.Importer, textureImporter);
                    Debug.Log($"[AutoImporterTemplate] TextureImporter: {assetPath}");
                    return true;
                }
            }
            else if (importer is AudioImporter audioImporter && 
                     template is AudioImporterTemplate audioTemplate)
            {
                if (audioTemplate.Importer != null)
                {
                    EditorUtility.CopySerialized(audioTemplate.Importer, audioImporter);
                    Debug.Log($"[AutoImporterTemplate] AudioImporter: {assetPath}");
                    return true;
                }
            }
            else if (importer is ModelImporter modelImporter && 
                     template is ModelImporterTemplate modelTemplate)
            {
                if (modelTemplate.Importer != null)
                {
                    EditorUtility.CopySerialized(modelTemplate.Importer, modelImporter);
                    Debug.Log($"[AutoImporterTemplate] ModelImporter: {assetPath}");
                    return true;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AutoImporterTemplate] 应用模板失败 [{assetPath}]: {e.Message}");
        }

        return false;
    }
}

