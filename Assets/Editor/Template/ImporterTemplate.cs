using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

public class ImporterTemplate<T> : SerializedScriptableObject
    where T : AssetImporter
{
    [SerializeField, Tooltip("是否作用于子文件夹")] public bool includeSubfolders = false;

    public virtual T Importer { get; set; }

    [ReadOnly, ShowInInspector]
    public List<Object> AffectedAssetPaths
        => ImporterTemplateUtility.GetAffectedAssets(this);

    [Button]
    public void ReImport()
    {
        foreach (var asset in AffectedAssetPaths)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            var importer = AssetImporter.GetAtPath(assetPath) as T;
            if (importer != null && Importer != null)
            {
                EditorUtility.CopySerialized(Importer, importer);
                importer.SaveAndReimport();
            }
        }
    }
}