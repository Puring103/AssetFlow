using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace AssetFlow.Editor.Workflow
{
    public interface IAssetFlowImporterTemplateProcessor
    {
        AssetImporter TemplateImporter { get; }

        AssetImporter TemplateImporterReference { get; }

        Preset LegacyPreset { get; }

        Preset TemplatePreset { get; }

        string TemplateImporterTypeKey { get; }

        void SetTemplateImporter(AssetImporter value);

        void SetTemplatePreset(Preset value);

        void SetTemplateImporterTypeKey(string value);

        void ClearLegacyPreset();
    }

    public abstract class ApplyImporterTemplateProcessor<TImporter> : AssetFlowPreImportProcessor<TImporter>, IAssetFlowImporterTemplateProcessor
        where TImporter : AssetImporter
    {
        [SerializeField] private Preset preset;
        [SerializeField] private Preset templatePreset;
        [SerializeField] private AssetImporter templateImporter;
        [SerializeField] private string templateImporterTypeKey;
        [SerializeField] private string versionSalt;

        public AssetImporter TemplateImporter
        {
            get => templateImporter;
        }

        public Preset LegacyPreset => preset;

        public Preset TemplatePreset => templatePreset;

        public AssetImporter TemplateImporterReference => templateImporter;

        public string TemplateImporterTypeKey => templateImporterTypeKey;

        public void SetTemplateImporter(AssetImporter value)
        {
            templateImporter = value;
        }

        public void SetTemplatePreset(Preset value)
        {
            templatePreset = value;
        }

        public void SetTemplateImporterTypeKey(string value)
        {
            templateImporterTypeKey = value ?? string.Empty;
        }

        public void ClearLegacyPreset()
        {
            preset = null;
        }

        public override string RuleHashPayload
        {
            get
            {
                var templatePayload = templatePreset != null
                    ? EditorJsonUtility.ToJson(templatePreset)
                    : templateImporter == null
                        ? string.Empty
                        : EditorJsonUtility.ToJson(templateImporter);
                return $"{base.RuleHashPayload}|template:{templatePayload}|salt:{versionSalt}";
            }
        }

        public override void Process(TImporter importer, AssetFlowPreImportContext context)
        {
            if (templateImporter == null)
            {
                if (templatePreset != null)
                {
                    if (templatePreset.CanBeAppliedTo(importer))
                        templatePreset.ApplyTo(importer);
                    else
                        context.ReportError("Template importer is incompatible with target importer.");
                }

                return;
            }

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

        internal void SetLegacyPresetForTests(Preset value)
        {
            preset = value;
        }
    }

}
