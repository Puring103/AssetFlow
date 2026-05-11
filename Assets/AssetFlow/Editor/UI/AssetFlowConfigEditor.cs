using AssetFlow.Editor.Core;
using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.UI
{
    [CustomEditor(typeof(AssetFlowConfig), true)]
    public sealed class AssetFlowConfigEditor : UnityEditor.Editor
    {
        private AssetFlowAppliedStateStore appliedStateStore;

        private void OnEnable()
        {
            appliedStateStore = new AssetFlowAppliedStateStore();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var config = (AssetFlowConfig)target;
            var snapshot = config.ToSnapshot();
            var applied = appliedStateStore.Find(snapshot.ConfigGuid);
            var hasUnappliedChanges = applied == null || applied.ruleHash != snapshot.RuleHash;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("AssetFlow", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("TypeKey", config.TypeKey);
            EditorGUILayout.LabelField("RuleHash", snapshot.RuleHash);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply To Managed Assets"))
                    Apply(config, snapshot);

                if (GUILayout.Button("Capture From Selected Asset"))
                    CaptureFromSelection(config);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Edit Preset"))
                    EditPreset(config);

                if (GUILayout.Button("Clear Preset"))
                    ClearPreset(config);

                if (GUILayout.Button("Refresh Dependencies"))
                    AssetFlowDependency.RegisterAll();
            }

            var outOfDate = AssetFlowApplyService.CountOutOfDateManagedAssets(config);
            EditorGUILayout.HelpBox($"Out-of-date managed assets: {outOfDate}", MessageType.Info);

            if (hasUnappliedChanges)
                EditorGUILayout.HelpBox("This AssetFlow workflow has unapplied changes.", MessageType.Warning);
        }

        private void OnDisable()
        {
            if (target == null || appliedStateStore == null)
                return;

            var config = (AssetFlowConfig)target;
            var snapshot = config.ToSnapshot();
            var applied = appliedStateStore.Find(snapshot.ConfigGuid);
            if (applied != null && applied.ruleHash == snapshot.RuleHash)
                return;

            var choice = EditorUtility.DisplayDialogComplex(
                "AssetFlow unapplied changes",
                "This AssetFlow workflow has unapplied changes.",
                "Apply",
                "Discard",
                "Cancel");

            if (choice == 0)
            {
                Apply(config, snapshot);
            }
            else if (choice == 1 && applied != null && !string.IsNullOrEmpty(applied.snapshotJson))
            {
                EditorJsonUtility.FromJsonOverwrite(applied.snapshotJson, config);
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            else if (choice == 2)
            {
                Selection.activeObject = config;
            }
        }

        private void Apply(AssetFlowConfig config, AssetFlowConfigSnapshot snapshot)
        {
            var count = AssetFlowApplyService.ApplyToManagedAssets(config);
            var json = EditorJsonUtility.ToJson(config);
            appliedStateStore.SaveAppliedSnapshot(snapshot.ConfigGuid, config.ComputeRuleHash(), json);
            EditorUtility.DisplayDialog("AssetFlow", $"Applied workflow to {count} managed assets.", "OK");
        }

        private static void CaptureFromSelection(AssetFlowConfig config)
        {
            var selected = Selection.activeObject;
            var path = selected == null ? string.Empty : AssetDatabase.GetAssetPath(selected);
            if (AssetFlowPresetUtility.CaptureFromAsset(config, path))
                EditorUtility.DisplayDialog("AssetFlow", "Captured importer preset from selected asset.", "OK");
            else
                EditorUtility.DisplayDialog("AssetFlow", "Select an asset with the same importer type first.", "OK");
        }

        private static void EditPreset(AssetFlowConfig config)
        {
            if (!AssetFlowPresetUtility.PingPreset(config))
                EditorUtility.DisplayDialog("AssetFlow", "This workflow has no captured preset.", "OK");
        }

        private static void ClearPreset(AssetFlowConfig config)
        {
            if (!EditorUtility.DisplayDialog("AssetFlow", "Clear the captured importer preset?", "Clear", "Cancel"))
                return;

            if (AssetFlowPresetUtility.ClearPreset(config))
                EditorUtility.DisplayDialog("AssetFlow", "Preset cleared.", "OK");
            else
                EditorUtility.DisplayDialog("AssetFlow", "This workflow has no preset to clear.", "OK");
        }
    }
}
