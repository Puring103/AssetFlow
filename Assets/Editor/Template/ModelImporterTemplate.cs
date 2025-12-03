using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

public class ModelImporterTemplate : ImporterTemplate<ModelImporter>
{
    [SerializeField] [InlineEditor] public ModelImporter importer;

    public override ModelImporter Importer
    {
        get => importer;
        set => importer = value;
    }
}

