using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Workflow
{
    public abstract class AssetFlowHandler : ScriptableObject
    {
        public virtual int Version => 1;

        public virtual string RuleHashPayload
        {
            get
            {
                var serialized = EditorJsonUtility.ToJson(this);
                return $"{GetType().AssemblyQualifiedName}|{Version}|{serialized}";
            }
        }
    }

    public abstract class AssetFlowPreImportProcessor : AssetFlowHandler
    {
        public abstract Type ImporterType { get; }

        public abstract void Process(AssetImporter importer, AssetFlowPreImportContext context);
    }

    public abstract class AssetFlowPreImportProcessor<TImporter> : AssetFlowPreImportProcessor
        where TImporter : AssetImporter
    {
        public sealed override Type ImporterType => typeof(TImporter);

        public sealed override void Process(AssetImporter importer, AssetFlowPreImportContext context)
        {
            if (importer is TImporter typedImporter)
                Process(typedImporter, context);
        }

        public abstract void Process(TImporter importer, AssetFlowPreImportContext context);
    }

    public abstract class AssetFlowPostImportProcessor : AssetFlowHandler
    {
        public abstract Type AssetType { get; }

        public abstract void Process(UnityEngine.Object asset, AssetFlowPostImportContext context);
    }

    public abstract class AssetFlowPostImportProcessor<TAsset> : AssetFlowPostImportProcessor
        where TAsset : UnityEngine.Object
    {
        public sealed override Type AssetType => typeof(TAsset);

        public sealed override void Process(UnityEngine.Object asset, AssetFlowPostImportContext context)
        {
            if (asset is TAsset typedAsset)
                Process(typedAsset, context);
        }

        public abstract void Process(TAsset asset, AssetFlowPostImportContext context);
    }

    public abstract class AssetFlowValidator : AssetFlowHandler
    {
        public abstract Type AssetType { get; }

        public abstract IEnumerable<AssetFlowIssue> Validate(UnityEngine.Object asset, AssetFlowValidationContext context);
    }

    public abstract class AssetFlowValidator<TAsset> : AssetFlowValidator
        where TAsset : UnityEngine.Object
    {
        public sealed override Type AssetType => typeof(TAsset);

        public sealed override IEnumerable<AssetFlowIssue> Validate(UnityEngine.Object asset, AssetFlowValidationContext context)
        {
            if (asset is TAsset typedAsset)
                return Validate(typedAsset, context);

            return Array.Empty<AssetFlowIssue>();
        }

        public abstract IEnumerable<AssetFlowIssue> Validate(TAsset asset, AssetFlowValidationContext context);
    }
}
