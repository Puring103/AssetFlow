using System.Collections.Generic;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using TriInspector;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

// [CreateAssetMenu(fileName = "FolderSettings", menuName = "AssetPipeline/FolderSettings")]
public class FolderSettings : SerializedScriptableObject
{
    public const string FileName = "FolderSettings.asset";

    [Tooltip("应用到子文件夹")]
    public bool applyToSubfolders = false;
    
    [SerializeReference]
    public List<Validator> validators = new();
    
    [SerializeReference]
    public List<AssetProcessor> processors = new();

    [TriInspector.InlineEditor]
    public Preset Preset;

    public bool Validate(Object asset, out List<string> errorMessages)
    {
        errorMessages = new List<string>();
        if (validators == null) return true;

        foreach (var validator in validators)
        {
            if (validator != null && !validator.Validate(asset, out var errorMessage))
            {
                errorMessages.Add(errorMessage);
            }
        }

        return errorMessages.Count == 0;
    }

    public void Process(Object asset)
    {
        if (processors == null) return;

        foreach (var processor in processors)
        {
            processor.Process(asset);
        }
    }
}

[System.Serializable]
public abstract class Validator
{
    public abstract bool Validate(Object asset, out string errorMessages);
}

// public class NameValidator : Validator
// {
//     public string pattern;

//     public override bool Validate(Object asset, out string errorMessage)
//     {
//         errorMessage = null;

//         if (!Regex.IsMatch(asset.name, pattern))
//         {
//             errorMessage = $"Asset name '{asset.name}' does not match pattern '{pattern}'";
//             return false;
//         }

//         return true;
//     }
// }

[System.Serializable]
public abstract class AssetProcessor
{
    public abstract void Process(Object asset);
}