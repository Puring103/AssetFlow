using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

public abstract class ImporterTemplate<T> : SerializedScriptableObject
    where T : AssetImporter
{
    [SerializeField, Tooltip("是否作用于子文件夹")] public bool includeSubfolders = false;

    public abstract T Importer { get; set; }

    [ReadOnly,ShowInInspector]
    public List<Object> AffectedAssetPaths
        => ImporterTemplateUtility.GetAffectedAssets(this);
}