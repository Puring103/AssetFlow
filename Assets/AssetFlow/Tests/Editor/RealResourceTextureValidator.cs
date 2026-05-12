using System.Collections.Generic;
using AssetFlow.Editor.Workflow;
using UnityEngine;

namespace AssetFlow.Editor.Tests
{
    public sealed class RealResourceTextureValidator : AssetFlowValidator<Texture2D>
    {
        public override IEnumerable<AssetFlowIssue> Validate(
            Texture2D asset,
            AssetFlowValidationContext context)
        {
            yield return new AssetFlowIssue(AssetFlowIssueSeverity.Warning, "Real texture validated: " + context.AssetPath);
        }
    }
}
