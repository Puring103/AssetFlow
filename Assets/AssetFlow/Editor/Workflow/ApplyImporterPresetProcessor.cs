using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace AssetFlow.Editor.Workflow
{
    public interface IAssetFlowImporterTemplateProcessor
    {
        Preset TemplatePreset { get; }

        AssetImporter LegacyTemplateImporter { get; }

        void SetTemplatePreset(Preset value);

        void ClearLegacyTemplateImporter();
    }

    public abstract class ApplyImporterPresetProcessor<TImporter> : AssetFlowPreImportProcessor<TImporter>, IAssetFlowImporterTemplateProcessor
        where TImporter : AssetImporter
    {
        [SerializeField] private Preset preset;
        [SerializeField] private AssetImporter templateImporter;
        [SerializeField] private string versionSalt;

        public Preset TemplatePreset => preset;

        public AssetImporter LegacyTemplateImporter => templateImporter;

        public void SetTemplatePreset(Preset value)
        {
            preset = value;
            templateImporter = null;
        }

        public void ClearLegacyTemplateImporter()
        {
            templateImporter = null;
        }

        public override string RuleHashPayload
        {
            get
            {
                var templatePayload = preset != null
                    ? EditorJsonUtility.ToJson(preset)
                    : templateImporter == null
                        ? string.Empty
                        : EditorJsonUtility.ToJson(templateImporter);
                return $"{base.RuleHashPayload}|template:{templatePayload}|salt:{versionSalt}";
            }
        }

        public override void Process(TImporter importer, AssetFlowPreImportContext context)
        {
            if (preset != null)
            {
                if (!preset.CanBeAppliedTo(importer))
                {
                    context.ReportError("Template preset is incompatible with target importer.");
                    return;
                }

                preset.ApplyTo(importer);
                return;
            }

            if (templateImporter == null)
                return;

            if (!(templateImporter is TImporter typedTemplateImporter))
            {
                context.ReportError("Template importer is incompatible with target importer.");
                return;
            }

            if (ReferenceEquals(typedTemplateImporter, importer))
                return;

            EditorUtility.CopySerialized(typedTemplateImporter, importer);
        }

        public void SetVersionSaltForTests(string value)
        {
            versionSalt = value;
        }
    }

}
