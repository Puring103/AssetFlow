using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 在 Inspector 顶部嵌入 FolderSettings 面板
/// </summary>
[InitializeOnLoad]
public static class FolderSettingsInspectorHeader
{
    private const string ContainerName = "FolderSettingsPanel";

    // 缓存数据
    private static string currentFolderPath;
    private static FolderSettings folderSettings;
    private static string folderSettingsSourcePath;
    private static bool isInheritedSettings;
    private static UnityEditor.Editor folderSettingsEditor;
    private static List<Preset> presets = new List<Preset>();
    private static bool foldoutExpanded = true;
    private static bool presetsFoldout = true;
    private static string lastProjectFolder;
    
    // GUI 样式
    private static GUIStyle boxStyle;
    private static bool stylesInitialized = false;

    static FolderSettingsInspectorHeader()
    {
        EditorApplication.delayCall += TryAddPanelToInspector;
        EditorApplication.update += EnsurePanelExists;
        EditorApplication.update += Refresh;
        Selection.selectionChanged += Refresh;
    }

    private static void TryAddPanelToInspector()
    {
        AddPanelToAllInspectors();
        Refresh();
    }

    private static double lastCheckTime;
    private static void EnsurePanelExists()
    {
        if (EditorApplication.timeSinceStartup - lastCheckTime > 1.0)
        {
            lastCheckTime = EditorApplication.timeSinceStartup;
            AddPanelToAllInspectors();
            CheckProjectBrowserFolder();
        }
    }

    private static void CheckProjectBrowserFolder()
    {
        if (Selection.activeObject != null) return;

        var projectFolder = FolderSettingsUtility.GetProjectBrowserFolder();
        if (projectFolder != lastProjectFolder)
        {
            lastProjectFolder = projectFolder;
            Refresh();
        }
    }

    private static void AddPanelToAllInspectors()
    {
        var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        if (inspectorType == null) return;

        var allInspectors = Resources.FindObjectsOfTypeAll(inspectorType);
        foreach (var inspector in allInspectors)
        {
            var window = inspector as EditorWindow;
            if (window == null) continue;

            var root = window.rootVisualElement;
            if (root == null) continue;

            if (root.Q<IMGUIContainer>(ContainerName) != null) continue;

            var imguiContainer = new IMGUIContainer(DrawFolderSettingsPanel)
            {
                name = ContainerName,
                style =
                {
                    backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingTop = 4,
                    paddingBottom = 4
                }
            };

            root.Insert(0, imguiContainer);
        }
    }

    private static void InitializeStyles()
    {
        if (stylesInitialized && boxStyle != null) return;
        
        boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(4, 4, 4, 4),
            margin = new RectOffset(0, 0, 0, 0)
        };
        
        stylesInitialized = true;
    }

    private static void DrawFolderSettingsPanel()
    {
        InitializeStyles();
        
        EditorGUILayout.BeginVertical(boxStyle);
        
        foldoutExpanded = EditorGUILayout.Foldout(foldoutExpanded, "AssetFlow", true);

        if (foldoutExpanded)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.HelpBox($"路径: {currentFolderPath ?? "未选择"}", MessageType.Info);

            GUILayout.Space(4);

            if (folderSettings != null)
            {
                if (isInheritedSettings)
                {
                    EditorGUILayout.HelpBox($"继承自: {folderSettingsSourcePath}", MessageType.Warning);
                    GUILayout.Space(2);

                    if (GUILayout.Button("创建本地 FolderSettings (覆盖继承)", GUILayout.Height(20)))
                    {
                        FolderSettingsUtility.CreateFolderSettings(currentFolderPath);
                        Refresh();
                    }
                    GUILayout.Space(4);
                }

                if (folderSettingsEditor == null || folderSettingsEditor.target != folderSettings)
                {
                    if (folderSettingsEditor != null)
                    {
                        Object.DestroyImmediate(folderSettingsEditor);
                    }
                    folderSettingsEditor = UnityEditor.Editor.CreateEditor(folderSettings);
                }

                if (folderSettingsEditor != null)
                {
                    EditorGUI.BeginChangeCheck();
                    folderSettingsEditor.OnInspectorGUI();
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorUtility.SetDirty(folderSettings);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(currentFolderPath))
            {
                if (GUILayout.Button("创建 FolderSettings", GUILayout.Height(20)))
                {
                    FolderSettingsUtility.CreateFolderSettings(currentFolderPath);
                    Refresh();
                }
            }

            GUILayout.Space(4);

            presetsFoldout = EditorGUILayout.Foldout(presetsFoldout, $"Preset 列表 ({presets.Count})", true);
            if (presetsFoldout && presets.Count > 0)
            {
                EditorGUI.indentLevel++;
                foreach (var preset in presets)
                {
                    EditorGUILayout.ObjectField(preset, typeof(Preset), false);
                }
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(4);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private static void Refresh()
    {
        currentFolderPath = FolderSettingsUtility.GetFolderPathFromSelection();

        var (newFolderSettings, settingsPath) = FolderSettingsUtility.FindFolderSettingsUpward(currentFolderPath);

        if (newFolderSettings != null && !string.IsNullOrEmpty(settingsPath))
        {
            var settingsFolder = FolderSettingsUtility.GetParentFolder(settingsPath);
            isInheritedSettings = FolderSettingsUtility.NormalizePath(settingsFolder) !=
                                    FolderSettingsUtility.NormalizePath(currentFolderPath);
            folderSettingsSourcePath = settingsPath;
        }
        else
        {
            isInheritedSettings = false;
            folderSettingsSourcePath = null;
        }

        if (folderSettings != newFolderSettings)
        {
            if (folderSettingsEditor != null)
            {
                Object.DestroyImmediate(folderSettingsEditor);
                folderSettingsEditor = null;
            }
            folderSettings = newFolderSettings;
        }

        presets = folderSettings != null
            ? FolderSettingsUtility.GetPresetsForFolderSettings(folderSettings)
            : FolderSettingsUtility.GetPresetsInFolder(currentFolderPath);

        RepaintAllInspectors();
    }

    private static void RepaintAllInspectors()
    {
        var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        if (inspectorType == null) return;

        foreach (var inspector in Resources.FindObjectsOfTypeAll(inspectorType))
        {
            (inspector as EditorWindow)?.Repaint();
        }
    }
}
