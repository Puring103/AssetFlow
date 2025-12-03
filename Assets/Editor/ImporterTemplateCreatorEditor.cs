using System;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Presets;
using UnityEngine;

[InitializeOnLoad]
public class ImporterTemplateCreatorEditor
{
    static ImporterTemplateCreatorEditor()
    {
        Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
    }

    private static void OnPostHeaderGUI(Editor editor)
    {
        // 检查是否是资源对象
        if (editor.targets.Length != 1)
            return;

        var target = editor.target;
        if (target == null)
            return;

        // 获取资源路径
        string assetPath = AssetDatabase.GetAssetPath(target);
        if (string.IsNullOrEmpty(assetPath))
            return;

        // 获取该资源的 AssetImporter
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
            return;

        // 检查是否支持该导入器类型
        bool isSupported = importer is TextureImporter || 
                          importer is ModelImporter || 
                          importer is AudioImporter;
        
        if (!isSupported)
            return;

        // 检查是否已经有所属的 ImporterTemplate
        ScriptableObject existingTemplate = ImporterTemplateUtility.GetTemplateForAsset(assetPath);
        
        if (existingTemplate != null)
        {
            // 资源已被 Template 托管，显示托管信息
            DrawManagedByTemplateUI(existingTemplate, assetPath);
        }
        else
        {
            // 资源未被托管，显示创建按钮
            DrawCreateTemplateButton(assetPath, importer);
        }
        
        GUILayout.Space(5);
    }
    
    /// <summary>
    /// 绘制"由 Template 托管"的 UI
    /// </summary>
    private static void DrawManagedByTemplateUI(ScriptableObject template, string assetPath)
    {
        // 使用醒目的颜色背景
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.7f, 1f, 0.3f); // 浅蓝色背景
        
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = originalColor;
        
        // 标题：醒目的提示
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        var titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 12;
        titleStyle.normal.textColor = new Color(0.2f, 0.5f, 1f);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        
        GUILayout.Label("⚙ 此资源由导入器模板托管", titleStyle);
        
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        
        GUILayout.Space(5);
        
        // 显示 Template 对象字段
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("管理模板:", GUILayout.Width(70));
        
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField(template, typeof(ScriptableObject), false);
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(5);
        
        // 按钮行
        GUILayout.BeginHorizontal();
        
        // 编辑模板按钮
        if (GUILayout.Button("编辑模板", GUILayout.Height(25)))
        {
            Selection.activeObject = template;
            EditorGUIUtility.PingObject(template);
        }
        
        // 查看受影响的资源按钮
        if (GUILayout.Button("查看所有托管资源", GUILayout.Height(25)))
        {
            Selection.activeObject = template;
        }
        
        GUILayout.EndHorizontal();
        
        GUILayout.Space(3);
        
        // 提示信息
        var hintStyle = new GUIStyle(EditorStyles.miniLabel);
        hintStyle.wordWrap = true;
        hintStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
        hintStyle.alignment = TextAnchor.MiddleCenter;
        
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

    private static void CreateImporterTemplate(string assetPath, AssetImporter importer)
    {
        // 根据导入器类型调用相应的泛型方法
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

    private static void CreateImporterTemplate<TTemplate, TImporter>(string assetPath, TImporter importer)
        where TTemplate : ImporterTemplate<TImporter>
        where TImporter : AssetImporter
    {
        // 获取当前资源所在的文件夹
        string folderPath = Path.GetDirectoryName(assetPath);
        
        // 生成新文件的路径
        string templateTypeName = typeof(TTemplate).Name;
        string templateFileName = $"{templateTypeName}.asset";
        string templatePath = Path.Combine(folderPath, templateFileName);
        templatePath = AssetDatabase.GenerateUniqueAssetPath(templatePath);
        
        // 创建模板实例
        TTemplate templateAsset = ScriptableObject.CreateInstance<TTemplate>();
        
        // 复制 Importer 设置（创建独立副本）
        TImporter newImporter = UnityEngine.Object.Instantiate(importer);
        newImporter.name = $"{typeof(TImporter).Name}";
        
        // 创建主资源文件
        AssetDatabase.CreateAsset(templateAsset, templatePath);
        
        // 将复制的 importer 作为子资源添加到 template 中
        // 这样可以确保 importer 能够正确持久化保存
        AssetDatabase.AddObjectToAsset(newImporter, templateAsset);
        
        // 将复制的设置赋值给 template
        templateAsset.Importer = newImporter;
        EditorUtility.SetDirty(templateAsset);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // 选中新创建的资源
        Selection.activeObject = templateAsset;
        EditorGUIUtility.PingObject(templateAsset);
        
        Debug.Log($"已创建 {templateTypeName}: {templatePath}");
    }
}

