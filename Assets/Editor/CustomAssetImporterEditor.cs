using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

/// <summary>
/// TextureImporter 的自定义编辑器
/// </summary>
[CustomEditor(typeof(AssetImporter))]
[CanEditMultipleObjects]
public class AssetImporterEditor : Editor
{
    private Editor defaultEditor;
    private TextureImporter targetImporter;
    private ScriptableObject managingTemplate;
    
    private bool showCustomImporter=false;
    
    private void OnEnable()
    {
        targetImporter = target as TextureImporter;
        if (targetImporter != null)
        {
            string assetPath = targetImporter.assetPath;
            managingTemplate = ImporterTemplateUtility.GetTemplateForAsset(assetPath);
            
            // 如果没有被模板托管，创建默认编辑器
            if (managingTemplate == null)
            {
                showCustomImporter = true;
            }
        }
    }
    
    private void OnDisable()
    {
        if (defaultEditor != null)
        {
            DestroyImmediate(defaultEditor);
        }
    }
    
    public override void OnInspectorGUI()
    {
        if (managingTemplate != null)
        {
            // 资源被模板托管，显示托管信息
            DrawManagedByTemplateUI();
        }
        else
        {
            // 显示原始的导入器界面
            if (defaultEditor != null)
            {
                defaultEditor.OnInspectorGUI();
            }
            
            // 添加创建模板按钮
            GUILayout.Space(10);
            DrawCreateTemplateButton();
        }
    }
    
    private void DrawManagedByTemplateUI()
    {
        // 使用醒目的颜色背景
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.7f, 1f, 0.3f);
        
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = originalColor;
        
        GUILayout.Space(10);
        
        // 大标题
        var titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 14;
        titleStyle.normal.textColor = new Color(0.2f, 0.5f, 1f);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        
        GUILayout.Label("⚙ 此资源由导入器模板托管", titleStyle);
        
        GUILayout.Space(10);
        
        // 分隔线
        DrawSeparator();
        
        GUILayout.Space(10);
        
        // 显示 Template 对象字段
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("管理模板:", EditorStyles.boldLabel, GUILayout.Width(70));
        
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField(managingTemplate, typeof(ScriptableObject), false);
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        // 提示信息框
        EditorGUILayout.HelpBox(
            "该资源的导入设置由上述模板统一管理。\n\n" +
            "• 所有导入设置修改请在模板中进行\n" +
            "• 直接修改此资源的设置可能会被模板覆盖\n" +
            "• 点击下方按钮可快速跳转到模板进行编辑",
            MessageType.Info
        );
        
        GUILayout.Space(10);
        
        // 按钮行
        GUILayout.BeginHorizontal();
        
        // 编辑模板按钮
        var buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 12;
        buttonStyle.fontStyle = FontStyle.Bold;
        
        if (GUILayout.Button("✏ 编辑模板", buttonStyle, GUILayout.Height(30)))
        {
            Selection.activeObject = managingTemplate;
            EditorGUIUtility.PingObject(managingTemplate);
        }
        
        // 查看所有托管资源按钮
        if (GUILayout.Button("📋 查看所有托管资源", buttonStyle, GUILayout.Height(30)))
        {
            Selection.activeObject = managingTemplate;
        }
        
        GUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        GUILayout.EndVertical();
        
        GUILayout.Space(20);
        
        // 显示资源预览（如果有）
        if (targetImporter != null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(targetImporter.assetPath);
            if (asset != null)
            {
                GUILayout.Label("资源预览:", EditorStyles.boldLabel);
                var rect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true));
                EditorGUI.DrawPreviewTexture(rect, asset, null, ScaleMode.ScaleToFit);
            }
        }
    }
    
    private void DrawCreateTemplateButton()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("创建导入器模板", GUILayout.Width(150), GUILayout.Height(25)))
        {
            CreateTemplateForCurrentAsset();
        }
        
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }
    
    private void CreateTemplateForCurrentAsset()
    {
        if (targetImporter == null)
            return;
        
        string assetPath = targetImporter.assetPath;
        ImporterTemplateCreator.CreateImporterTemplate<TextureImporterTemplate, TextureImporter>(assetPath, targetImporter);
        
        // 刷新编辑器
        managingTemplate = ImporterTemplateUtility.GetTemplateForAsset(assetPath);
        if (managingTemplate != null && defaultEditor != null)
        {
            DestroyImmediate(defaultEditor);
            defaultEditor = null;
        }
    }
    
    private void DrawSeparator()
    {
        var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
    }
}


/// <summary>
/// 用于创建 ImporterTemplate 的辅助类
/// </summary>
public static class ImporterTemplateCreator
{
    public static void CreateImporterTemplate<TTemplate, TImporter>(string assetPath, TImporter importer)
        where TTemplate : ImporterTemplate<TImporter>
        where TImporter : AssetImporter
    {
        // 获取当前资源所在的文件夹
        string folderPath = System.IO.Path.GetDirectoryName(assetPath);
        
        // 生成新文件的路径
        string templateTypeName = typeof(TTemplate).Name;
        string templateFileName = $"{templateTypeName}.asset";
        string templatePath = System.IO.Path.Combine(folderPath, templateFileName);
        templatePath = AssetDatabase.GenerateUniqueAssetPath(templatePath);
        
        // 创建模板实例
        TTemplate templateAsset = ScriptableObject.CreateInstance<TTemplate>();
        
        // 复制 Importer 设置
        TImporter newImporter = Object.Instantiate(importer);
        newImporter.name = $"{typeof(TImporter).Name}";
        
        // 创建主资源文件
        AssetDatabase.CreateAsset(templateAsset, templatePath);
        
        // 将复制的 importer 作为子资源添加到 template 中
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

