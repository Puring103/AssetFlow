using AssetFlow.Editor.Workflow;
using NUnit.Framework;
using UnityEngine;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowProcessingContextTests
    {
        [Test]
        public void ReportingIssues_PreservesSeverityAndMessage()
        {
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            try
            {
                var context = new AssetFlowPostImportContext(null, config);

                context.ReportInfo("info");
                context.ReportWarning("warning");
                context.ReportError("error");

                Assert.That(context.AssetPath, Is.EqualTo(string.Empty));
                Assert.That(context.Config, Is.SameAs(config));
                Assert.That(context.Issues, Has.Count.EqualTo(3));
                Assert.That(context.Issues[0].Severity, Is.EqualTo(AssetFlowIssueSeverity.Info));
                Assert.That(context.Issues[0].Message, Is.EqualTo("info"));
                Assert.That(context.Issues[1].Severity, Is.EqualTo(AssetFlowIssueSeverity.Warning));
                Assert.That(context.Issues[1].Message, Is.EqualTo("warning"));
                Assert.That(context.Issues[2].Severity, Is.EqualTo(AssetFlowIssueSeverity.Error));
                Assert.That(context.Issues[2].Message, Is.EqualTo("error"));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
