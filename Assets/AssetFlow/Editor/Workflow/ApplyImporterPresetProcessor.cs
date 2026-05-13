using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Workflow
{
    public interface IAssetFlowImporterTemplateProcessor
    {
        AssetImporter TemplateImporter { get; }

        void SetTemplateImporter(AssetImporter value);
    }

    public abstract class ApplyImporterPresetProcessor<TImporter> : AssetFlowPreImportProcessor<TImporter>, IAssetFlowImporterTemplateProcessor
        where TImporter : AssetImporter
    {
        [SerializeField] private AssetImporter templateImporter;
        [SerializeField] private string versionSalt;

        public AssetImporter TemplateImporter => templateImporter;

        public void SetTemplateImporter(AssetImporter value)
        {
            templateImporter = value;
        }

        public override string RuleHashPayload
        {
            get
            {
                var templatePayload = templateImporter == null ? string.Empty : EditorJsonUtility.ToJson(templateImporter);
                return $"{base.RuleHashPayload}|template:{templatePayload}|salt:{versionSalt}";
            }
        }

        public override void Process(TImporter importer, AssetFlowPreImportContext context)
        {
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
