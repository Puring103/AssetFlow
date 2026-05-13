using AssetFlow.Editor.Core;
using AssetFlow.Editor.Importing;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace AssetFlow.Editor.UI
{
    [CustomEditor(typeof(TextureImporter))]
    internal sealed class AssetFlowManagedTextureImporterEditor : AssetFlowManagedImporterEditorBase
    {
    }

    [CustomEditor(typeof(ModelImporter))]
    internal sealed class AssetFlowManagedModelImporterEditor : AssetFlowManagedImporterEditorBase
    {
    }

    [CustomEditor(typeof(AudioImporter))]
    internal sealed class AssetFlowManagedAudioImporterEditor : AssetFlowManagedImporterEditorBase
    {
    }

    internal abstract class AssetFlowManagedImporterEditorBase : AssetImporterEditor
    {
        private bool isManaged;

        public override void OnEnable()
        {
            base.OnEnable();
            RefreshManagedState();
        }

        public override void OnDisable()
        {
            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            if (!isManaged)
            {
                base.OnInspectorGUI();
                return;
            }

            EditorGUILayout.HelpBox(
                "This importer is managed by AssetFlow. Its original importer settings are shown read-only; edit the linked AssetFlow config instead.",
                MessageType.Info);
            DrawReadOnlySerializedProperties();
            using (new EditorGUI.DisabledScope(true))
                ApplyRevertGUI();
        }

        public override bool HasModified()
        {
            return !isManaged && base.HasModified();
        }

        public override void SaveChanges()
        {
            if (!isManaged)
                base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            if (!isManaged)
                base.DiscardChanges();
        }

        private void RefreshManagedState()
        {
            isManaged = false;
            foreach (var currentTarget in targets)
            {
                var importer = currentTarget as AssetImporter;
                if (importer == null || !IsManaged(importer))
                    return;
            }

            isManaged = targets.Length > 0;
        }

        private static bool IsManaged(AssetImporter importer)
        {
            var path = importer.assetPath;
            if (string.IsNullOrEmpty(path)
                || path.IndexOf("/AssetFlow.", System.StringComparison.OrdinalIgnoreCase) >= 0
                || path.StartsWith(AssetFlowPresetUtility.TemporaryAssetFolder + "/", System.StringComparison.OrdinalIgnoreCase))
                return false;

            var resolver = new AssetFlowResolver(AssetFlowConfigScanner.FindConfigSnapshots());
            var result = resolver.Resolve(path, importer.GetType().FullName);
            return result.Status == AssetFlowResolveStatus.Managed;
        }

        private void DrawReadOnlySerializedProperties()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                var iterator = serializedObject.GetIterator();
                var enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (iterator.propertyPath == "m_Script")
                        continue;

                    EditorGUILayout.PropertyField(iterator, includeChildren: true);
                }
            }
        }
    }

}
