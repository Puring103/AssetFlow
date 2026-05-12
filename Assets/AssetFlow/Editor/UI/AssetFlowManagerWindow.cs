using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetFlow.Editor.Core;
using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.UI
{
    public sealed class AssetFlowManagerWindow : EditorWindow
    {
        private const float SidebarWidth = 360f;
        private const float RowHeight = 20f;

        private readonly List<AssetFlowManagerConfigView> configViews = new List<AssetFlowManagerConfigView>();
        private readonly List<AssetFlowManagerTreeNode> treeRoots = new List<AssetFlowManagerTreeNode>();
        private readonly HashSet<string> expandedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Vector2 treeScroll;
        private Vector2 inspectorScroll;
        private SerializedObject selectedSerializedObject;
        private AssetFlowConfig selectedConfig;
        private AssetFlowManagerConfigView selectedView;
        private string selectedNodeKey = string.Empty;
        private string selectedTypeFilter = string.Empty;
        private string selectedTab = PreImportTab;

        private static readonly string[] TabLabels = { "Pre Import", "Post Import", "Validators" };
        private static readonly string[] TabIds = { PreImportTab, PostImportTab, ValidatorsTab };

        private const string PreImportTab = "PreImport";
        private const string PostImportTab = "PostImport";
        private const string ValidatorsTab = "Validators";

        [MenuItem("Window/AssetFlow/AssetFlow Manager")]
        public static void Open()
        {
            var window = GetWindow<AssetFlowManagerWindow>();
            window.titleContent = new GUIContent("AssetFlow Manager");
            window.minSize = new Vector2(860f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            DisposeSelectedSerializedObject();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSidebar();
                DrawDivider();
                DrawInspector();
            }
        }

        private void DrawSidebar()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidebarWidth)))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    DrawTypeFilter();
                    if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                        Refresh();
                }

                treeScroll = EditorGUILayout.BeginScrollView(treeScroll);
                if (treeRoots.Count == 0)
                {
                    EditorGUILayout.HelpBox("No AssetFlow configs found.", MessageType.Info);
                }
                else
                {
                    foreach (var node in treeRoots)
                        DrawTreeNode(node, 0);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTypeFilter()
        {
            var labels = new List<string> { "All Types" };
            var values = new List<string> { string.Empty };
            foreach (var typeKey in configViews.Select(view => view.Snapshot.TypeKey).Distinct().OrderBy(FriendlyTypeName))
            {
                labels.Add(FriendlyTypeName(typeKey));
                values.Add(typeKey);
            }

            var currentIndex = Mathf.Max(0, values.IndexOf(selectedTypeFilter));
            var nextIndex = EditorGUILayout.Popup(currentIndex, labels.ToArray(), EditorStyles.toolbarPopup, GUILayout.MinWidth(150f));
            if (nextIndex == currentIndex)
                return;

            selectedTypeFilter = values[nextIndex];
            RebuildTree();
        }

        private static void DrawDivider()
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
        }

        private void DrawTreeNode(AssetFlowManagerTreeNode node, int indent)
        {
            var rect = GUILayoutUtility.GetRect(1f, RowHeight, GUILayout.ExpandWidth(true));
            var selected = string.Equals(selectedNodeKey, node.Key, StringComparison.Ordinal);
            if (selected)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.36f, 0.52f, 0.85f));

            var contentRect = new Rect(rect.x + indent * 16f, rect.y, rect.width - indent * 16f, rect.height);
            var hasChildren = node.Children.Count > 0;
            var expanded = expandedKeys.Contains(node.Key);
            if (hasChildren)
            {
                var foldoutRect = new Rect(contentRect.x, contentRect.y, 16f, contentRect.height);
                var nextExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none);
                if (nextExpanded)
                    expandedKeys.Add(node.Key);
                else
                    expandedKeys.Remove(node.Key);
            }

            var iconRect = new Rect(contentRect.x + 16f, contentRect.y + 2f, 16f, 16f);
            if (node.Icon != null)
                GUI.DrawTexture(iconRect, node.Icon, ScaleMode.ScaleToFit);

            var labelRect = new Rect(iconRect.xMax + 4f, contentRect.y, contentRect.width - 36f, contentRect.height);
            EditorGUI.LabelField(labelRect, new GUIContent(node.Label, node.Path));

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                SelectNode(node);
                Event.current.Use();
            }

            if (!hasChildren || !expandedKeys.Contains(node.Key))
                return;

            foreach (var child in node.Children)
                DrawTreeNode(child, indent + 1);
        }

        private void DrawInspector()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                if (selectedConfig == null || selectedView == null)
                {
                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField("Select an AssetFlow config.", EditorStyles.boldLabel);
                    return;
                }

                inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
                DrawSummary();
                EditorGUILayout.Space(8f);
                DrawTabs();
                DrawSelectedList();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSummary()
        {
            selectedSerializedObject.Update();

            EditorGUILayout.LabelField(RootLabel(selectedView.Snapshot), EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Config", selectedConfig, typeof(AssetFlowConfig), false);
            }

            EditorGUILayout.LabelField("Path", selectedView.Snapshot.ConfigPath);
            EditorGUILayout.LabelField("Type", FriendlyTypeName(selectedView.Snapshot.TypeKey));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Rule Hash", GUILayout.Width(EditorGUIUtility.labelWidth));
                EditorGUILayout.SelectableLabel(selectedView.Snapshot.RuleHash, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Managed: {selectedView.ManagedAssetPaths.Count}", GUILayout.Width(110f));
                EditorGUILayout.LabelField($"Out of date: {selectedView.OutOfDateCount}", GUILayout.Width(120f));
                EditorGUILayout.LabelField($"Validation records: {selectedView.ValidationCount}", GUILayout.Width(170f));
            }

            var includeSubfolders = selectedSerializedObject.FindProperty("includeSubfolders");
            if (includeSubfolders != null)
                EditorGUILayout.PropertyField(includeSubfolders, new GUIContent("Include Subfolders"));

            selectedSerializedObject.ApplyModifiedProperties();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ping Config", GUILayout.Width(110f)))
                {
                    Selection.activeObject = selectedConfig;
                    EditorGUIUtility.PingObject(selectedConfig);
                }

                if (GUILayout.Button("Apply To Managed Assets", GUILayout.Width(180f)))
                {
                    var count = AssetFlowApplyService.ApplyToManagedAssets(selectedConfig);
                    EditorUtility.DisplayDialog("AssetFlow", $"Applied workflow to {count} managed assets.", "OK");
                    Refresh();
                }

                if (GUILayout.Button("Refresh Dependencies", GUILayout.Width(160f)))
                {
                    AssetFlowDependency.RegisterAll();
                    Refresh();
                }
            }
        }

        private void DrawTabs()
        {
            var selectedIndex = Mathf.Max(0, Array.IndexOf(TabIds, selectedTab));
            var nextIndex = GUILayout.Toolbar(selectedIndex, TabLabels);
            selectedTab = TabIds[nextIndex];
        }

        private void DrawSelectedList()
        {
            selectedSerializedObject.Update();

            var propertyName = selectedTab == PreImportTab
                ? "preImportProcessors"
                : selectedTab == PostImportTab
                    ? "postImportProcessors"
                    : "validators";
            var property = selectedSerializedObject.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox($"Missing serialized property: {propertyName}", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(TabLabels[Array.IndexOf(TabIds, selectedTab)], EditorStyles.boldLabel);
            if (property.isArray && property.arraySize == 0)
                EditorGUILayout.HelpBox("No entries configured.", MessageType.Info);

            EditorGUILayout.PropertyField(property, includeChildren: true);
            selectedSerializedObject.ApplyModifiedProperties();
        }

        private void SelectNode(AssetFlowManagerTreeNode node)
        {
            selectedView = node.ConfigView;
            selectedConfig = selectedView.Config;
            selectedNodeKey = node.Key;
            DisposeSelectedSerializedObject();
            selectedSerializedObject = new SerializedObject(selectedConfig);

            if (node.Kind != AssetFlowManagerTreeItemKind.Asset)
                return;

            var asset = AssetDatabase.LoadMainAssetAtPath(node.Path);
            if (asset != null)
                Selection.activeObject = asset;
        }

        private void Refresh()
        {
            DisposeSelectedSerializedObject();
            selectedConfig = null;
            selectedView = null;
            selectedNodeKey = string.Empty;
            configViews.Clear();

            var snapshots = AssetFlowConfigScanner.FindConfigSnapshots()
                .OrderBy(snapshot => snapshot.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(snapshot => FriendlyTypeName(snapshot.TypeKey), StringComparer.OrdinalIgnoreCase)
                .ToList();
            var index = new AssetFlowIndexStore().Load();
            var assetsByConfigGuid = FindManagedAssetPathsByConfig(snapshots);

            foreach (var snapshot in snapshots)
            {
                var config = AssetFlowConfigScanner.LoadConfig(snapshot);
                if (config == null)
                    continue;

                var assetPaths = assetsByConfigGuid.TryGetValue(snapshot.ConfigGuid, out var paths)
                    ? paths
                    : new List<string>();
                var outOfDate = CountOutOfDate(assetPaths, snapshot, index);
                var validationCount = index.ValidationResults.Count(record => record.configGuid == snapshot.ConfigGuid);
                configViews.Add(new AssetFlowManagerConfigView(config, snapshot, assetPaths, outOfDate, validationCount));
            }

            RebuildTree();
            Repaint();
        }

        private void RebuildTree()
        {
            var previousConfigGuid = selectedView?.Snapshot.ConfigGuid ?? string.Empty;
            treeRoots.Clear();
            foreach (var view in configViews.Where(IsVisibleByFilter))
                treeRoots.Add(BuildConfigTree(view));

            var retained = FindNodeByConfigGuid(treeRoots, previousConfigGuid);
            if (retained != null)
                SelectNode(retained);
            else if (treeRoots.Count > 0)
                SelectNode(treeRoots[0]);
        }

        private static AssetFlowManagerTreeNode FindNodeByConfigGuid(IEnumerable<AssetFlowManagerTreeNode> nodes, string configGuid)
        {
            if (string.IsNullOrEmpty(configGuid))
                return null;

            foreach (var node in nodes)
            {
                if (node.Kind == AssetFlowManagerTreeItemKind.Config
                    && string.Equals(node.ConfigView.Snapshot.ConfigGuid, configGuid, StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }

                var child = FindNodeByConfigGuid(node.Children, configGuid);
                if (child != null)
                    return child;
            }

            return null;
        }

        private bool IsVisibleByFilter(AssetFlowManagerConfigView view)
        {
            return string.IsNullOrEmpty(selectedTypeFilter)
                   || string.Equals(view.Snapshot.TypeKey, selectedTypeFilter, StringComparison.Ordinal);
        }

        private AssetFlowManagerTreeNode BuildConfigTree(AssetFlowManagerConfigView view)
        {
            var root = new AssetFlowManagerTreeNode(
                view,
                AssetFlowManagerTreeItemKind.Config,
                RootLabel(view.Snapshot),
                view.Snapshot.ConfigPath,
                EditorGUIUtility.IconContent("ScriptableObject Icon").image);
            expandedKeys.Add(root.Key);

            foreach (var child in BuildAssetTree(view))
                root.Children.Add(child);

            return root;
        }

        private List<AssetFlowManagerTreeNode> BuildAssetTree(AssetFlowManagerConfigView view)
        {
            var rootFolder = AssetFlowPath.Normalize(view.Snapshot.FolderPath);
            var rootNodes = new List<AssetFlowManagerTreeNode>();
            var folderMap = new Dictionary<string, AssetFlowManagerTreeNode>(StringComparer.OrdinalIgnoreCase);

            foreach (var assetPath in view.ManagedAssetPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var relative = MakeRelativeAssetPath(rootFolder, assetPath);
                var parts = relative.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var currentChildren = rootNodes;
                var currentFolderKey = string.Empty;

                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var isFile = i == parts.Length - 1;
                    if (isFile)
                    {
                        currentChildren.Add(new AssetFlowManagerTreeNode(
                            view,
                            AssetFlowManagerTreeItemKind.Asset,
                            part,
                            assetPath,
                            AssetDatabase.GetCachedIcon(assetPath)));
                        continue;
                    }

                    currentFolderKey = string.IsNullOrEmpty(currentFolderKey)
                        ? part
                        : currentFolderKey + "/" + part;

                    if (!folderMap.TryGetValue(currentFolderKey, out var folder))
                    {
                        folder = new AssetFlowManagerTreeNode(
                            view,
                            AssetFlowManagerTreeItemKind.Folder,
                            part,
                            rootFolder + "/" + currentFolderKey,
                            EditorGUIUtility.IconContent("Folder Icon").image);
                        folderMap[currentFolderKey] = folder;
                        currentChildren.Add(folder);
                    }

                    currentChildren = folder.Children;
                }
            }

            return rootNodes;
        }

        private void DisposeSelectedSerializedObject()
        {
            if (selectedSerializedObject == null)
                return;

            selectedSerializedObject.Dispose();
            selectedSerializedObject = null;
        }

        private static Dictionary<string, List<string>> FindManagedAssetPathsByConfig(IReadOnlyList<AssetFlowConfigSnapshot> snapshots)
        {
            var resolver = new AssetFlowResolver(snapshots);
            var assetsByConfigGuid = snapshots.ToDictionary(
                snapshot => snapshot.ConfigGuid,
                _ => new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            foreach (var guid in AssetDatabase.FindAssets(string.Empty))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    continue;

                var importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                    continue;

                var result = resolver.Resolve(path, importer.GetType().FullName);
                if (result.Status == AssetFlowResolveStatus.Managed
                    && assetsByConfigGuid.TryGetValue(result.Config.ConfigGuid, out var paths))
                {
                    paths.Add(path);
                }
            }

            return assetsByConfigGuid;
        }

        private static int CountOutOfDate(IEnumerable<string> assetPaths, AssetFlowConfigSnapshot snapshot, AssetFlowIndex index)
        {
            return assetPaths.Count(path =>
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                return index.IsOutOfDate(guid, snapshot.ConfigGuid, snapshot.RuleHash);
            });
        }

        private static string RootLabel(AssetFlowConfigSnapshot snapshot)
        {
            return $"{Path.GetFileName(snapshot.FolderPath)} ({FriendlyTypeName(snapshot.TypeKey)})";
        }

        private static string FriendlyTypeName(string typeKey)
        {
            if (typeKey == typeof(TextureImporter).FullName)
                return "Texture";
            if (typeKey == typeof(ModelImporter).FullName)
                return "Model";
            if (typeKey == typeof(AudioImporter).FullName)
                return "Audio";

            const string importerSuffix = "Importer";
            var name = typeKey;
            var dotIndex = name.LastIndexOf('.');
            if (dotIndex >= 0)
                name = name.Substring(dotIndex + 1);
            if (name.EndsWith(importerSuffix, StringComparison.Ordinal))
                name = name.Substring(0, name.Length - importerSuffix.Length);
            return name;
        }

        private static string MakeRelativeAssetPath(string rootFolder, string assetPath)
        {
            var normalizedAsset = AssetFlowPath.Normalize(assetPath);
            if (normalizedAsset.StartsWith(rootFolder + "/", StringComparison.OrdinalIgnoreCase))
                return normalizedAsset.Substring(rootFolder.Length + 1);

            return normalizedAsset;
        }

        private sealed class AssetFlowManagerConfigView
        {
            public AssetFlowManagerConfigView(
                AssetFlowConfig config,
                AssetFlowConfigSnapshot snapshot,
                List<string> managedAssetPaths,
                int outOfDateCount,
                int validationCount)
            {
                Config = config;
                Snapshot = snapshot;
                ManagedAssetPaths = managedAssetPaths;
                OutOfDateCount = outOfDateCount;
                ValidationCount = validationCount;
            }

            public AssetFlowConfig Config { get; }

            public AssetFlowConfigSnapshot Snapshot { get; }

            public List<string> ManagedAssetPaths { get; }

            public int OutOfDateCount { get; }

            public int ValidationCount { get; }
        }

        private sealed class AssetFlowManagerTreeNode
        {
            public AssetFlowManagerTreeNode(
                AssetFlowManagerConfigView configView,
                AssetFlowManagerTreeItemKind kind,
                string label,
                string path,
                Texture icon)
            {
                ConfigView = configView;
                Kind = kind;
                Label = label;
                Path = path;
                Icon = icon;
                Key = $"{kind}:{path}:{label}";
            }

            public AssetFlowManagerConfigView ConfigView { get; }

            public AssetFlowManagerTreeItemKind Kind { get; }

            public string Label { get; }

            public string Path { get; }

            public Texture Icon { get; }

            public string Key { get; }

            public List<AssetFlowManagerTreeNode> Children { get; } = new List<AssetFlowManagerTreeNode>();
        }

        private enum AssetFlowManagerTreeItemKind
        {
            Config,
            Folder,
            Asset
        }
    }
}
