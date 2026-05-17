using AssetFlow.Editor.Core;
using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using UnityEditor;

namespace AssetFlow.Editor.UI
{
    [CustomEditor(typeof(AssetFlowConfig), true)]
    public sealed class AssetFlowConfigEditor : UnityEditor.Editor
    {
        private AssetFlowAppliedStateStore appliedStateStore;
        private AssetFlowConfigPanelDrawer panelDrawer;
        private int managedCount;
        private int outOfDateCount;
        private AssetFlowConfigSnapshot cachedSnapshot;
        private AssetFlowAppliedConfigRecord cachedApplied;
        private bool hasCachedSnapshot;
        private double nextStatsRefreshTime;
        private bool restoringSelection;

        private void OnEnable()
        {
            appliedStateStore = new AssetFlowAppliedStateStore();
            panelDrawer = new AssetFlowConfigPanelDrawer();
            RefreshStats(force: true);
        }

        public override void OnInspectorGUI()
        {
            var config = (AssetFlowConfig)target;
            RefreshStats(force: false);
            var snapshot = config.ToSnapshot();
            cachedSnapshot = snapshot;
            hasCachedSnapshot = true;
            var applied = cachedApplied;
            var hasUnappliedChanges = applied == null || applied.ruleHash != snapshot.RuleHash;
            var canApply = hasUnappliedChanges || outOfDateCount > 0;
            var changed = panelDrawer.Draw(
                config,
                serializedObject,
                AssetFlowManagerWindow.FriendlyConfigTitle(snapshot),
                snapshot.ConfigPath,
                managedCount,
                outOfDateCount,
                0,
                canApply,
                () =>
                {
                    Apply(config, snapshot);
                    RefreshStats(force: true);
                });

            if (changed)
            {
                RefreshStats(force: true);
                hasUnappliedChanges = true;
                Repaint();
            }

            if (hasUnappliedChanges)
                EditorGUILayout.HelpBox("This AssetFlow workflow has unapplied changes.", MessageType.Warning);
        }

        private void OnDisable()
        {
            panelDrawer?.Dispose();

            if (target == null || appliedStateStore == null || restoringSelection)
                return;

            var config = (AssetFlowConfig)target;
            var snapshot = GetSnapshot(config);
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
                restoringSelection = true;
                EditorApplication.delayCall += () =>
                {
                    if (config != null)
                    {
                        Selection.activeObject = config;
                        EditorGUIUtility.PingObject(config);
                    }

                    restoringSelection = false;
                };
            }
        }

        private void Apply(AssetFlowConfig config, AssetFlowConfigSnapshot snapshot)
        {
            var count = AssetFlowApplyService.ApplyToManagedAssets(config);
            EditorUtility.DisplayDialog("AssetFlow", $"Applied workflow to {count} managed assets.", "OK");
        }

        private void RefreshStats(bool force)
        {
            if (!force && EditorApplication.timeSinceStartup < nextStatsRefreshTime)
                return;

            var config = target as AssetFlowConfig;
            if (config == null)
                return;

            cachedSnapshot = config.ToSnapshot();
            hasCachedSnapshot = true;
            cachedApplied = appliedStateStore.Find(cachedSnapshot.ConfigGuid);
            var stats = AssetFlowApplyService.GetManagedStats(config);
            managedCount = stats.ManagedCount;
            outOfDateCount = stats.OutOfDateCount;
            nextStatsRefreshTime = EditorApplication.timeSinceStartup + 1.0d;
        }

        private AssetFlowConfigSnapshot GetSnapshot(AssetFlowConfig config)
        {
            if (hasCachedSnapshot)
                return cachedSnapshot;

            cachedSnapshot = config.ToSnapshot();
            hasCachedSnapshot = true;
            return cachedSnapshot;
        }
    }
}
