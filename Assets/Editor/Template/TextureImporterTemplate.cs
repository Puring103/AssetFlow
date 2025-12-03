using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

public class TextureImporterTemplate : ImporterTemplate<TextureImporter>
{
    [SerializeField] [InlineEditor] public TextureImporter importer;

    public override TextureImporter Importer
    {
        get => importer;
        set => importer = value;
    }
}