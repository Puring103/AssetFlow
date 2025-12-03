using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

public class AudioImporterTemplate : ImporterTemplate<AudioImporter>
{
    [SerializeField] [InlineEditor] public AudioImporter importer;

    public override AudioImporter Importer
    {
        get => importer;
        set => importer = value;
    }
}