using AssetFlow.Editor.Workflow;
using UnityEngine;

namespace AssetFlow.Editor.Tests
{
    public sealed class ConfigHandlerAssetTestPostProcessor : AssetFlowPostImportProcessor<Texture2D>
    {
        [SerializeField] private int threshold = 16;

        public int Threshold => threshold;

        public override void Process(Texture2D asset, AssetFlowPostImportContext context)
        {
        }
    }
}
