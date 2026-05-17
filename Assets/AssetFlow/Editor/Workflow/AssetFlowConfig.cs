using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AssetFlow.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Workflow
{
    public abstract class AssetFlowConfig : ScriptableObject
    {
        [SerializeField] private bool includeSubfolders;
        [SerializeField] private List<AssetFlowPreImportProcessor> preImportProcessors = new List<AssetFlowPreImportProcessor>();
        [SerializeField] private List<AssetFlowPostImportProcessor> postImportProcessors = new List<AssetFlowPostImportProcessor>();
        [SerializeField] private List<AssetFlowValidator> validators = new List<AssetFlowValidator>();

        public abstract string TypeKey { get; }

        public bool IncludeSubfolders
        {
            get => includeSubfolders;
            set => includeSubfolders = value;
        }

        public IReadOnlyList<AssetFlowPreImportProcessor> PreImportProcessors => preImportProcessors;

        public IReadOnlyList<AssetFlowPostImportProcessor> PostImportProcessors => postImportProcessors;

        public IReadOnlyList<AssetFlowValidator> Validators => validators;

        public AssetFlowConfigSnapshot ToSnapshot()
        {
            var path = AssetDatabase.GetAssetPath(this);
            var guid = AssetDatabase.AssetPathToGUID(path);
            return new AssetFlowConfigSnapshot(guid, path, AssetFlowPath.GetParentFolder(path), TypeKey, includeSubfolders, ComputeRuleHash());
        }

        public string ComputeRuleHash()
        {
            var builder = new StringBuilder();
            builder.Append(GetType().AssemblyQualifiedName).AppendLine();
            builder.Append(TypeKey).AppendLine();
            builder.Append(includeSubfolders).AppendLine();
            AppendHandlerList(builder, preImportProcessors);
            AppendHandlerList(builder, postImportProcessors);
            AppendHandlerList(builder, validators);

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public void EnsureSingleTemplateProcessor()
        {
            var seen = false;
            for (var i = 0; i < preImportProcessors.Count; i++)
            {
                var processor = preImportProcessors[i];
                if (!(processor is IAssetFlowImporterTemplateProcessor))
                    continue;

                if (!seen)
                {
                    seen = true;
                    continue;
                }

                RemoveSubAsset(processor);
                preImportProcessors.RemoveAt(i);
                i--;
            }
        }

        public void AddHandlerAsSubAsset(AssetFlowHandler handler)
        {
            if (handler == null)
                return;

            AddHandlerReference(handler);
            var configPath = AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(configPath) && !AssetDatabase.Contains(handler))
                AssetDatabase.AddObjectToAsset(handler, this);

            EditorUtility.SetDirty(handler);
            EditorUtility.SetDirty(this);
        }

        public void RemoveHandlerAndSubAsset(AssetFlowHandler handler)
        {
            if (handler == null)
                return;

            RemoveHandlerReference(handler);
            if (handler is IAssetFlowImporterTemplateProcessor templateProcessor)
            {
                RemoveSubAsset(templateProcessor.TemplatePreset);
                RemoveSubAsset(templateProcessor.TemplateImporter);
            }

            RemoveSubAsset(handler);
            EditorUtility.SetDirty(this);
        }

        protected void ResetProcessorLists()
        {
            preImportProcessors = new List<AssetFlowPreImportProcessor>();
            postImportProcessors = new List<AssetFlowPostImportProcessor>();
            validators = new List<AssetFlowValidator>();
        }

        protected void AddPreImportProcessor(AssetFlowPreImportProcessor processor)
        {
            if (processor == null)
                return;

            preImportProcessors.Add(processor);
        }

        protected void AddPostImportProcessor(AssetFlowPostImportProcessor processor)
        {
            if (processor == null)
                return;

            postImportProcessors.Add(processor);
        }

        protected void AddValidator(AssetFlowValidator validator)
        {
            if (validator == null)
                return;

            validators.Add(validator);
        }

        private void AddHandlerReference(AssetFlowHandler handler)
        {
            if (handler is AssetFlowPreImportProcessor preImportProcessor)
            {
                preImportProcessors.Add(preImportProcessor);
                return;
            }

            if (handler is AssetFlowPostImportProcessor postImportProcessor)
            {
                postImportProcessors.Add(postImportProcessor);
                return;
            }

            if (handler is AssetFlowValidator validator)
                validators.Add(validator);
        }

        private void RemoveHandlerReference(AssetFlowHandler handler)
        {
            if (handler is AssetFlowPreImportProcessor preImportProcessor)
                preImportProcessors.Remove(preImportProcessor);
            else if (handler is AssetFlowPostImportProcessor postImportProcessor)
                postImportProcessors.Remove(postImportProcessor);
            else if (handler is AssetFlowValidator validator)
                validators.Remove(validator);
        }

        internal void AddPostImportProcessorForTests(AssetFlowPostImportProcessor processor)
        {
            AddPostImportProcessor(processor);
        }

        internal void AddValidatorForTests(AssetFlowValidator validator)
        {
            AddValidator(validator);
        }

        private static void AppendHandlerList<THandler>(StringBuilder builder, IEnumerable<THandler> handlers)
            where THandler : AssetFlowHandler
        {
            foreach (var handler in handlers.Where(handler => handler != null))
                builder.Append(handler.RuleHashPayload).AppendLine();
        }

        private static void RemoveSubAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return;

            if (AssetDatabase.Contains(asset))
                UnityEngine.Object.DestroyImmediate(asset, allowDestroyingAssets: true);
            else
                UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
