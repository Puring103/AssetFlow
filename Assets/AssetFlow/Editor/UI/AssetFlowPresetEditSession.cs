using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace AssetFlow.Editor.UI
{
    internal sealed class AssetFlowPresetEditSession : System.IDisposable
    {
        private readonly Preset preset;
        private readonly string temporaryAssetPath;
        private readonly AssetImporter importer;
        private readonly UnityEditor.Editor importerEditor;

        private AssetFlowPresetEditSession(Preset preset, string temporaryAssetPath, AssetImporter importer, UnityEditor.Editor importerEditor)
        {
            this.preset = preset;
            this.temporaryAssetPath = temporaryAssetPath;
            this.importer = importer;
            this.importerEditor = importerEditor;
        }

        public static AssetFlowPresetEditSession Create(AssetFlowConfig config, Preset preset)
        {
            if (config == null || preset == null)
                return null;

            var path = AssetFlowPresetUtility.CreateTemporarySourceAssetForPresetEditing(config.TypeKey);
            if (string.IsNullOrEmpty(path))
                return null;

            var importer = AssetImporter.GetAtPath(path);
            if (importer == null || !preset.CanBeAppliedTo(importer))
            {
                AssetFlowPresetUtility.DeleteTemporarySourceAssetForPresetEditing(path);
                return null;
            }

            preset.ApplyTo(importer);
            var editor = UnityEditor.Editor.CreateEditor(importer);
            if (editor == null)
            {
                AssetFlowPresetUtility.DeleteTemporarySourceAssetForPresetEditing(path);
                return null;
            }

            return new AssetFlowPresetEditSession(preset, path, importer, editor);
        }

        public void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            importerEditor.OnInspectorGUI();
            if (!EditorGUI.EndChangeCheck())
                return;

            preset.UpdateProperties(importer);
            EditorUtility.SetDirty(preset);
        }

        public void Dispose()
        {
            if (importerEditor != null)
                Object.DestroyImmediate(importerEditor);

            AssetFlowPresetUtility.DeleteTemporarySourceAssetForPresetEditing(temporaryAssetPath);
        }
    }
}
