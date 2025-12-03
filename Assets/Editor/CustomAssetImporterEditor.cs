using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

/// <summary>
/// 资源导入器的统一管理编辑器
/// 使用 InitializeOnLoad 在所有资源 Inspector 头部添加模板管理功能
/// </summary>
[InitializeOnLoad]
public class CustomAssetImporterEditor
{
    static CustomAssetImporterEditor()
    {
        Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
    }

    private static void OnPostHeaderGUI(Editor editor)
    {
        // 检查是否是单个资源对象
        if (editor.targets.Length != 1 || editor.target == null)
            return;

        // 获取资源路径
        var assetPath = AssetDatabase.GetAssetPath(editor.target);
        if (string.IsNullOrEmpty(assetPath))
            return;

        // 获取资源的 AssetImporter
        var importer = AssetImporter.GetAtPath(assetPath);
        if (!ImporterTemplateUtility.IsSupportedImporter(importer))
            return;

        // 检查是否已有模板管理
        var existingTemplate = ImporterTemplateUtility.GetTemplateForAsset(assetPath);
        
        if (existingTemplate != null)
        {
            DrawManagedByTemplateUI(existingTemplate);
        }
        else
        {
            DrawCreateTemplateButton(assetPath, importer);
        }
        
        GUILayout.Space(5);
    }
    /// <summary>
    /// 绘制"由 Template 托管"的 UI（用于头部显示）
    /// </summary>
    private static void DrawManagedByTemplateUI(ScriptableObject template)
    {
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.7f, 1f, 0.3f);
        
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = originalColor;
        
        // 标题
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        var titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(0.2f, 0.5f, 1f);
        
        GUILayout.Label("⚙ 此资源由导入器模板托管", titleStyle);
        
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        
        GUILayout.Space(5);
        
        // 显示模板对象字段
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("管理模板:", GUILayout.Width(70));
        
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField(template, typeof(ScriptableObject), false);
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(5);
        
        // 操作按钮
        GUILayout.BeginHorizontal();
        
        if (GUILayout.Button("编辑模板", GUILayout.Height(25)))
        {
            Selection.activeObject = template;
            EditorGUIUtility.PingObject(template);
        }
        
        if (GUILayout.Button("查看所有托管资源", GUILayout.Height(25)))
        {
            Selection.activeObject = template;
        }
        
        GUILayout.EndHorizontal();
        
        GUILayout.Space(3);
        
        // 提示信息
        var hintStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter
        };
        hintStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
        
        GUILayout.Label("该资源的导入设置由上述模板统一管理，直接修改导入设置可能会被模板覆盖。", hintStyle);
        
        GUILayout.EndVertical();
    }
    
    /// <summary>
    /// 绘制创建模板按钮
    /// </summary>
    private static void DrawCreateTemplateButton(string assetPath, AssetImporter importer)
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("创建导入器模板", GUILayout.Width(150)))
        {
            CreateImporterTemplate(assetPath, importer);
        }
        
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// 根据导入器类型创建对应的模板
    /// </summary>
    private static void CreateImporterTemplate(string assetPath, AssetImporter importer)
    {
        if (importer is TextureImporter textureImporter)
        {
            CreateImporterTemplate<TextureImporterTemplate, TextureImporter>(assetPath, textureImporter);
        }
        else if (importer is ModelImporter modelImporter)
        {
            CreateImporterTemplate<ModelImporterTemplate, ModelImporter>(assetPath, modelImporter);
        }
        else if (importer is AudioImporter audioImporter)
        {
            CreateImporterTemplate<AudioImporterTemplate, AudioImporter>(assetPath, audioImporter);
        }
    }

    /// <summary>
    /// 泛型方法：创建导入器模板
    /// </summary>
    private static void CreateImporterTemplate<TTemplate, TImporter>(string assetPath, TImporter importer)
        where TTemplate : ImporterTemplate<TImporter>
        where TImporter : AssetImporter
    {
        // 获取资源所在文件夹
        var folderPath = Path.GetDirectoryName(assetPath);
        
        // 生成模板文件路径
        var templateTypeName = typeof(TTemplate).Name;
        var templateFileName = $"{templateTypeName}.asset";
        if (folderPath != null)
        {
            var templatePath = Path.Combine(folderPath, templateFileName);
            templatePath = AssetDatabase.GenerateUniqueAssetPath(templatePath);
        
            // 创建模板实例
            var templateAsset = ScriptableObject.CreateInstance<TTemplate>();

            // 复制导入器设置
            var newImporter = Object.Instantiate(importer);
            newImporter.name = typeof(TImporter).Name;
        
            // 创建主资源
            AssetDatabase.CreateAsset(templateAsset, templatePath);
        
            // 将导入器作为子资源添加
            AssetDatabase.AddObjectToAsset(newImporter, templateAsset);
        
            // 设置导入器引用
            templateAsset.Importer = newImporter;
            EditorUtility.SetDirty(templateAsset);
        
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        
            // 选中新创建的模板
            Selection.activeObject = templateAsset;
            EditorGUIUtility.PingObject(templateAsset);
        
            Debug.Log($"已创建 {templateTypeName}: {templatePath}");
        }
    }
}