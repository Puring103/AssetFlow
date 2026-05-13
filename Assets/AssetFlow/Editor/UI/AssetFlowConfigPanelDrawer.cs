using System;
using System.Collections.Generic;
using System.Linq;
using AssetFlow.Editor.Core;
using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.UI
{
    internal sealed class AssetFlowConfigPanelDrawer : IDisposable
    {
        private const float HandlerRowHeight = 38f;
        private const float HandlerRemoveButtonWidth = 22f;
        private const float HandlerTargetBadgeWidth = 92f;
        private static readonly string[] TabLabels = { "Pre Import", "Post Import", "Validators" };
        private static readonly string[] TabIds = { PreImportTab, PostImportTab, ValidatorsTab };
        private const string PreImportTab = "PreImport";
        private const string PostImportTab = "PostImport";
        private const string ValidatorsTab = "Validators";

        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle mutedStyle;
        private GUIStyle badgeStyle;
        private GUIStyle sectionBoxStyle;
        private GUIStyle handlerRowStyle;
        private UnityEngine.Object templateImporterEditorTarget;
        private UnityEditor.Editor templateImporterEditor;
        private readonly Dictionary<string, List<Type>> addableHandlerTypesByKey = new Dictionary<string, List<Type>>();
        private string selectedTab = PreImportTab;
        private string expandedHandlerKey = string.Empty;

        public bool Draw(
            AssetFlowConfig config,
            SerializedObject serializedObject,
            string title,
            string subtitle,
            int managedCount,
            int staleCount,
            int issueCount,
            bool showApplyButton,
            Action apply)
        {
            EnsureStyles();
            var changed = false;

            serializedObject.Update();
            DrawHeader(title, subtitle);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.VerticalScope(sectionBoxStyle))
            {
                var includeSubfolders = serializedObject.FindProperty("includeSubfolders");
                if (includeSubfolders != null)
                    EditorGUILayout.PropertyField(includeSubfolders, new GUIContent("Include Subfolders"));
            }

            changed |= EditorGUI.EndChangeCheck();
            changed |= serializedObject.ApplyModifiedProperties();

            using (new EditorGUILayout.VerticalScope(sectionBoxStyle))
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStat("Managed", managedCount.ToString());
                DrawStat("Stale", staleCount.ToString());
                DrawStat("Issues", issueCount.ToString());
            }

            EditorGUILayout.Space(8f);
            DrawTabs();
            changed |= DrawSelectedList(config, serializedObject);
            if (showApplyButton || changed)
            {
                EditorGUILayout.Space(10f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Apply", GUILayout.Width(120f), GUILayout.Height(28f)))
                        apply?.Invoke();
                }
            }

            return changed;
        }

        public void Dispose()
        {
            DisposeTemplateImporterEditor();
            addableHandlerTypesByKey.Clear();
        }

        private void DisposeTemplateImporterEditor()
        {
            if (templateImporterEditor != null)
                UnityEngine.Object.DestroyImmediate(templateImporterEditor);

            templateImporterEditor = null;
            templateImporterEditorTarget = null;
        }

        private void DrawHeader(string title, string subtitle)
        {
            var rect = GUILayoutUtility.GetRect(1f, 48f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, new Color(0.16f, 0.19f, 0.22f, 1f));

            var icon = EditorGUIUtility.IconContent("ScriptableObject Icon").image;
            if (icon != null)
                GUI.DrawTexture(new Rect(rect.x + 10f, rect.y + 10f, 28f, 28f), icon, ScaleMode.ScaleToFit);

            GUI.Label(new Rect(rect.x + 48f, rect.y + 7f, rect.width - 60f, 20f), title, headerStyle);
            GUI.Label(new Rect(rect.x + 48f, rect.y + 27f, rect.width - 60f, 18f), subtitle, mutedStyle);
        }

        private void DrawStat(string label, string value)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(96f), GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField(value, headerStyle);
                EditorGUILayout.LabelField(label, mutedStyle);
            }
        }

        private void DrawTabs()
        {
            var selectedIndex = Mathf.Max(0, Array.IndexOf(TabIds, selectedTab));
            var nextTab = TabIds[GUILayout.Toolbar(selectedIndex, TabLabels)];
            if (nextTab == selectedTab)
                return;

            selectedTab = nextTab;
            expandedHandlerKey = string.Empty;
        }

        private bool DrawSelectedList(AssetFlowConfig config, SerializedObject serializedObject)
        {
            var changed = false;
            serializedObject.Update();
            var propertyName = selectedTab == PreImportTab
                ? "preImportProcessors"
                : selectedTab == PostImportTab
                    ? "postImportProcessors"
                    : "validators";
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox($"Missing serialized property: {propertyName}", MessageType.Error);
                return false;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(TabLabels[Array.IndexOf(TabIds, selectedTab)], subHeaderStyle);
                    GUILayout.FlexibleSpace();
                    DrawAddHandlerMenu(config, property);
                }

                if (property.isArray && property.arraySize == 0)
                    EditorGUILayout.HelpBox("No entries configured.", MessageType.Info);

                for (var i = 0; i < property.arraySize; i++)
                    changed |= DrawHandlerListItem(config, property, i);
            }

            changed |= serializedObject.ApplyModifiedProperties();
            return changed;
        }

        private bool DrawHandlerListItem(AssetFlowConfig config, SerializedProperty property, int index)
        {
            var changed = false;
            var element = property.GetArrayElementAtIndex(index);
            var handler = element.objectReferenceValue as AssetFlowHandler;
            using (new EditorGUILayout.VerticalScope(handlerRowStyle))
            {
                var rowRect = GUILayoutUtility.GetRect(1f, HandlerRowHeight, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                {
                    var background = EditorGUIUtility.isProSkin
                        ? new Color(0.22f, 0.22f, 0.22f, 0.55f)
                        : new Color(0.92f, 0.92f, 0.92f, 0.85f);
                    if (rowRect.Contains(Event.current.mousePosition))
                    {
                        background = EditorGUIUtility.isProSkin
                            ? new Color(0.28f, 0.32f, 0.36f, 0.75f)
                            : new Color(0.82f, 0.88f, 0.95f, 0.90f);
                    }

                    EditorGUI.DrawRect(rowRect, background);
                }

                var foldoutRect = new Rect(rowRect.x + 4f, rowRect.y + 5f, 18f, 18f);
                var removeRect = new Rect(rowRect.xMax - HandlerRemoveButtonWidth - 6f, rowRect.y + 6f, HandlerRemoveButtonWidth, 20f);
                var targetRect = new Rect(
                    Mathf.Max(foldoutRect.xMax + 8f, removeRect.x - HandlerTargetBadgeWidth - 6f),
                    rowRect.y + 6f,
                    Mathf.Min(HandlerTargetBadgeWidth, Mathf.Max(0f, removeRect.x - foldoutRect.xMax - 14f)),
                    18f);
                var textRight = targetRect.width > 0f ? targetRect.x - 6f : removeRect.x - 6f;
                var titleRect = new Rect(foldoutRect.xMax + 4f, rowRect.y + 4f, Mathf.Max(0f, textRight - foldoutRect.xMax - 4f), 16f);
                var metaRect = new Rect(titleRect.x, rowRect.y + 21f, titleRect.width, 12f);

                var handlerKey = HandlerExpansionKey(property, index, handler);
                var isExpanded = string.Equals(expandedHandlerKey, handlerKey, StringComparison.Ordinal);
                var nextExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, GUIContent.none);
                if (nextExpanded != isExpanded)
                {
                    expandedHandlerKey = nextExpanded ? handlerKey : string.Empty;
                    element.isExpanded = nextExpanded;
                    isExpanded = nextExpanded;
                }
                else
                {
                    element.isExpanded = isExpanded;
                }

                EditorGUI.LabelField(titleRect, new GUIContent(HandlerDisplayName(handler), HandlerFullName(handler)), subHeaderStyle);
                EditorGUI.LabelField(metaRect, new GUIContent(HandlerShortName(handler), HandlerFullName(handler)), mutedStyle);
                if (targetRect.width > 0f)
                    GUI.Label(targetRect, new GUIContent(HandlerTargetLabel(handler), HandlerTargetTooltip(handler)), badgeStyle);

                if (GUI.Button(removeRect, EditorGUIUtility.IconContent("Toolbar Minus"), EditorStyles.miniButton))
                {
                    RemoveHandler(config, property, index);
                    GUIUtility.ExitGUI();
                }

                if (isExpanded && handler != null)
                    changed |= DrawHandlerInspector(config, handler);
            }

            return changed;
        }

        private static string HandlerExpansionKey(SerializedProperty property, int index, AssetFlowHandler handler)
        {
            if (handler != null)
                return handler.GetInstanceID().ToString();

            return $"{property.propertyPath}:{index}";
        }

        private bool DrawHandlerInspector(AssetFlowConfig config, AssetFlowHandler handler)
        {
            var changed = false;
            using (new EditorGUI.IndentLevelScope())
            using (var handlerObject = new SerializedObject(handler))
            {
                handlerObject.Update();
                var iterator = handlerObject.GetIterator();
                var enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (iterator.propertyPath == "m_Script" || iterator.propertyPath == "versionSalt")
                        continue;

                    if (handler is IAssetFlowImporterTemplateProcessor && iterator.propertyPath == "templateImporter")
                    {
                        changed |= DrawTemplateImporterInspector(config, (IAssetFlowImporterTemplateProcessor)handler);
                        continue;
                    }

                    EditorGUILayout.PropertyField(iterator, includeChildren: true);
                }

                changed |= handlerObject.ApplyModifiedProperties();
            }

            if (changed)
                EditorUtility.SetDirty(config);

            return changed;
        }

        private bool DrawTemplateImporterInspector(AssetFlowConfig config, IAssetFlowImporterTemplateProcessor processor)
        {
            AssetFlowPresetUtility.RemoveLegacyPresetSubAssets(config);
            AssetFlowPresetUtility.EnsureTemplateImporter(config);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Template Importer", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(
                        "Source",
                        processor.TemplateImporter,
                        typeof(AssetImporter),
                        false);
                }

                var importer = processor.TemplateImporter;
                if (importer == null)
                {
                    DisposeTemplateImporterEditor();
                    EditorGUILayout.HelpBox("Template importer is being prepared.", MessageType.Info);
                    return false;
                }

                if (templateImporterEditorTarget != importer)
                {
                    DisposeTemplateImporterEditor();
                    templateImporterEditorTarget = importer;
                    templateImporterEditor = UnityEditor.Editor.CreateEditor(importer);
                }

                if (templateImporterEditor == null)
                    return false;

                EditorGUI.BeginChangeCheck();
                templateImporterEditor.OnInspectorGUI();
                if (!EditorGUI.EndChangeCheck())
                    return false;

                EditorUtility.SetDirty(importer);
                EditorUtility.SetDirty((UnityEngine.Object)processor);
                EditorUtility.SetDirty(config);
                return true;
            }
        }

        private void DrawAddHandlerMenu(AssetFlowConfig config, SerializedProperty property)
        {
            var candidates = FindAddableHandlerTypes(config, property).ToList();
            using (new EditorGUI.DisabledScope(candidates.Count == 0))
            {
                if (!EditorGUILayout.DropdownButton(new GUIContent(candidates.Count == 0 ? "Add: none" : "Add"), FocusType.Passive, EditorStyles.miniButton, GUILayout.Width(86f)))
                    return;
            }

            var menu = new GenericMenu();
            foreach (var type in candidates)
            {
                var capturedType = type;
                menu.AddItem(new GUIContent(GetAddMenuPath(type)), false, () => AddHandler(config, capturedType));
            }

            menu.ShowAsContext();
        }

        private IEnumerable<Type> FindAddableHandlerTypes(AssetFlowConfig config, SerializedProperty property)
        {
            var cacheKey = $"{selectedTab}|{config.TypeKey}";
            if (!addableHandlerTypesByKey.TryGetValue(cacheKey, out var candidates))
            {
                candidates = BuildAddableHandlerTypeCache(config).ToList();
                addableHandlerTypesByKey[cacheKey] = candidates;
            }

            foreach (var type in candidates)
            {
                if (typeof(IAssetFlowImporterTemplateProcessor).IsAssignableFrom(type) && ContainsHandlerAssignableTo(property, typeof(IAssetFlowImporterTemplateProcessor)))
                    continue;

                yield return type;
            }
        }

        private IEnumerable<Type> BuildAddableHandlerTypeCache(AssetFlowConfig config)
        {
            var baseType = selectedTab == PreImportTab
                ? typeof(AssetFlowPreImportProcessor)
                : selectedTab == PostImportTab
                    ? typeof(AssetFlowPostImportProcessor)
                    : typeof(AssetFlowValidator);

            foreach (var type in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition || type.IsNotPublic || type.IsNestedPrivate || !typeof(ScriptableObject).IsAssignableFrom(type))
                    continue;
                if (!IsCompatibleHandlerType(config, type))
                    continue;

                yield return type;
            }
        }

        private static bool IsCompatibleHandlerType(AssetFlowConfig config, Type type)
        {
            if (typeof(AssetFlowPreImportProcessor).IsAssignableFrom(type))
                return TryGetHandlerTargetType(type, typeof(AssetFlowPreImportProcessor<>), out var importerType)
                       && string.Equals(importerType.FullName, config.TypeKey, StringComparison.Ordinal);
            if (typeof(AssetFlowPostImportProcessor).IsAssignableFrom(type))
                return TryGetHandlerTargetType(type, typeof(AssetFlowPostImportProcessor<>), out _);
            if (typeof(AssetFlowValidator).IsAssignableFrom(type))
                return TryGetHandlerTargetType(type, typeof(AssetFlowValidator<>), out _);
            return false;
        }

        private static bool TryGetHandlerTargetType(Type type, Type openGenericType, out Type targetType)
        {
            targetType = null;
            while (type != null && type != typeof(object))
            {
                var current = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                if (current == openGenericType)
                {
                    targetType = type.GetGenericArguments()[0];
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static bool ContainsHandlerAssignableTo(SerializedProperty property, Type type)
        {
            for (var i = 0; i < property.arraySize; i++)
            {
                var value = property.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value != null && type.IsInstanceOfType(value))
                    return true;
            }

            return false;
        }

        private static void AddHandler(AssetFlowConfig config, Type type)
        {
            var handler = (AssetFlowHandler)ScriptableObject.CreateInstance(type);
            handler.name = type.Name;
            var configPath = AssetDatabase.GetAssetPath(config);
            config.AddHandlerAsSubAsset(handler);
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(configPath))
                AssetDatabase.ImportAsset(configPath);
        }

        private static void RemoveHandler(AssetFlowConfig config, SerializedProperty property, int index)
        {
            var handler = property.GetArrayElementAtIndex(index).objectReferenceValue as AssetFlowHandler;
            var configPath = AssetDatabase.GetAssetPath(config);
            if (handler != null)
            {
                config.RemoveHandlerAndSubAsset(handler);
            }
            else
            {
                property.DeleteArrayElementAtIndex(index);
                property.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
            }

            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(configPath))
                AssetDatabase.ImportAsset(configPath);
        }

        private void EnsureStyles()
        {
            headerStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            subHeaderStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            mutedStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.68f, 0.68f, 0.68f) : new Color(0.35f, 0.35f, 0.35f) }
            };
            badgeStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.78f, 0.86f, 0.95f) : new Color(0.20f, 0.32f, 0.45f) }
            };
            sectionBoxStyle ??= new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(8, 8, 6, 6) };
            handlerRowStyle ??= new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(0, 0, 2, 2)
            };
        }

        private static string GetAddMenuPath(Type type)
        {
            return $"{HandlerTargetTypeName(type)}/{ObjectNames.NicifyVariableName(type.Name)}";
        }

        private static string HandlerDisplayName(AssetFlowHandler handler)
        {
            if (handler == null)
                return "Missing Handler";

            if (handler is IAssetFlowImporterTemplateProcessor)
                return ObjectNames.NicifyVariableName(handler.GetType().Name.Replace("PresetProcessor", "TemplateProcessor"));

            return ObjectNames.NicifyVariableName(handler.GetType().Name);
        }

        private static string HandlerFullName(AssetFlowHandler handler)
        {
            return handler == null ? "Reference is missing." : handler.GetType().FullName;
        }

        private static string HandlerShortName(AssetFlowHandler handler)
        {
            if (handler == null)
                return "Reference is missing.";

            var type = handler.GetType();
            return type.Namespace == null ? type.Name : $"{type.Namespace}.{type.Name}";
        }

        private static string HandlerTargetLabel(AssetFlowHandler handler)
        {
            if (handler == null)
                return "Missing";

            return $"{HandlerStageName(handler)} / {FriendlyTypeName(HandlerTargetTypeName(handler.GetType()))}";
        }

        private static string HandlerTargetTooltip(AssetFlowHandler handler)
        {
            if (handler == null)
                return "Missing handler reference.";

            return HandlerTargetTypeName(handler.GetType());
        }

        private static string HandlerStageName(AssetFlowHandler handler)
        {
            if (handler is AssetFlowPreImportProcessor)
                return "Pre";
            if (handler is AssetFlowPostImportProcessor)
                return "Post";
            if (handler is AssetFlowValidator)
                return "Validator";
            return "Handler";
        }

        private static string HandlerTargetTypeName(Type type)
        {
            if (TryGetHandlerTargetType(type, typeof(AssetFlowPreImportProcessor<>), out var importerType))
                return importerType.FullName;
            if (TryGetHandlerTargetType(type, typeof(AssetFlowPostImportProcessor<>), out var assetType))
                return assetType.FullName;
            if (TryGetHandlerTargetType(type, typeof(AssetFlowValidator<>), out var validatorType))
                return validatorType.FullName;

            return "Unknown";
        }

        private static string FriendlyTypeName(string typeKey)
        {
            if (typeKey == typeof(TextureImporter).FullName)
                return "Texture";
            if (typeKey == typeof(ModelImporter).FullName)
                return "Model";
            if (typeKey == typeof(AudioImporter).FullName)
                return "Audio";

            var name = typeKey;
            var dotIndex = name.LastIndexOf('.');
            if (dotIndex >= 0)
                name = name.Substring(dotIndex + 1);
            return name;
        }
    }
}
