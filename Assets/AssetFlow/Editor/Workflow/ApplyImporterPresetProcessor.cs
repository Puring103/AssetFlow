using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace AssetFlow.Editor.Workflow
{
    public interface IAssetFlowPresetProcessor
    {
        Preset Preset { get; }
    }

    public abstract class ApplyImporterPresetProcessor<TImporter> : AssetFlowPreImportProcessor<TImporter>, IAssetFlowPresetProcessor
        where TImporter : AssetImporter
    {
        [SerializeField] private Preset preset;
        [SerializeField] private string versionSalt;

        public Preset Preset => preset;

        public void SetPreset(Preset value)
        {
            preset = value;
        }

        public override string RuleHashPayload
        {
            get
            {
                var presetPayload = preset == null ? string.Empty : EditorJsonUtility.ToJson(preset);
                return $"{base.RuleHashPayload}|preset:{presetPayload}|salt:{versionSalt}";
            }
        }

        public override void Process(TImporter importer, AssetFlowPreImportContext context)
        {
            if (preset == null)
                return;

            if (!preset.CanBeAppliedTo(importer))
            {
                context.ReportError("Preset cannot be applied to importer.");
                return;
            }

            preset.ApplyTo(importer);
        }

        public void SetVersionSaltForTests(string value)
        {
            versionSalt = value;
        }
    }

}
