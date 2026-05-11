using AssetFlow.Editor.Core;
using AssetFlow.Editor.Importing;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.UI
{
    [InitializeOnLoad]
    public static class AssetFlowInspectorOverlay
    {
        static AssetFlowInspectorOverlay()
        {
            global::UnityEditor.Editor.finishedDefaultHeaderGUI += DrawHeader;
        }

        private static void DrawHeader(global::UnityEditor.Editor editor)
        {
            if (editor.targets.Length != 1 || editor.target == null)
                return;

            var path = AssetDatabase.GetAssetPath(editor.target);
            if (string.IsNullOrEmpty(path))
                return;

            var importer = AssetImporter.GetAtPath(path);
            if (importer == null)
                return;

            var resolver = new AssetFlowResolver(AssetFlowConfigScanner.FindConfigSnapshots());
            var result = resolver.Resolve(path, importer.GetType().FullName);
            if (result.Status == AssetFlowResolveStatus.Unmanaged)
                return;

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (result.Status == AssetFlowResolveStatus.Conflict)
                {
                    EditorGUILayout.LabelField("AssetFlow Conflict", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Multiple configs of the same TypeKey exist in this folder.");
                    return;
                }

                var index = new AssetFlowIndexStore().Load();
                var assetGuid = AssetDatabase.AssetPathToGUID(path);
                var outOfDate = index.IsOutOfDate(assetGuid, result.Config.ConfigGuid, result.Config.RuleHash);

                EditorGUILayout.LabelField(outOfDate ? "AssetFlow Managed (Out of date)" : "AssetFlow Managed", EditorStyles.boldLabel);
                EditorGUILayout.ObjectField("Config", AssetDatabase.LoadAssetAtPath<Object>(result.Config.ConfigPath), typeof(Object), false);
                EditorGUILayout.LabelField("RuleHash", result.Config.RuleHash);
                DrawPausedHandlers(assetGuid);
                DrawValidationResults(index, assetGuid, result.Config.ConfigGuid);
            }
        }

        private static void DrawPausedHandlers(string assetGuid)
        {
            var paused = AssetFlowAssetPostprocessor.SharedLoopGuard.GetPausedKeysForAsset(assetGuid);
            if (paused.Count == 0)
                return;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Paused handlers", EditorStyles.boldLabel);
            foreach (var key in paused)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{key.Stage}: {key.HandlerTypeFullName}");
                    if (GUILayout.Button("Retry", GUILayout.Width(60)))
                    {
                        AssetFlowAssetPostprocessor.SharedLoopGuard.Retry(key);
                        AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(assetGuid), ImportAssetOptions.ForceUpdate);
                    }
                }
            }
        }

        private static void DrawValidationResults(AssetFlowIndex index, string assetGuid, string configGuid)
        {
            var results = index.ValidationResults
                .Where(record => record.assetGuid == assetGuid && record.configGuid == configGuid)
                .Take(5)
                .ToList();
            if (results.Count == 0)
                return;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            foreach (var record in results)
                EditorGUILayout.HelpBox(record.message, ToMessageType(record.severity));
        }

        private static MessageType ToMessageType(string severity)
        {
            if (severity == AssetFlow.Editor.Workflow.AssetFlowIssueSeverity.Error.ToString())
                return MessageType.Error;
            if (severity == AssetFlow.Editor.Workflow.AssetFlowIssueSeverity.Warning.ToString())
                return MessageType.Warning;
            return MessageType.Info;
        }
    }
}
