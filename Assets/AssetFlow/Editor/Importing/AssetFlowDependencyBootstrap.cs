using UnityEditor;

namespace AssetFlow.Editor.Importing
{
    [InitializeOnLoad]
    internal static class AssetFlowDependencyBootstrap
    {
        static AssetFlowDependencyBootstrap()
        {
            AssetFlowDependency.RegisterAll();
            EditorApplication.projectChanged += AssetFlowDependency.RegisterAll;
        }
    }
}
