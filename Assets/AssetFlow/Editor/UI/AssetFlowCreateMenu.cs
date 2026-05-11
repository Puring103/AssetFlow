using AssetFlow.Editor.Importing;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.UI
{
    public static class AssetFlowCreateMenu
    {
        [MenuItem("Assets/Create/AssetFlow/Texture", priority = 80)]
        public static void CreateTexture()
        {
            SelectCreated(AssetFlowConfigFactory.CreateTextureConfig(GetSelectedFolder()));
        }

        [MenuItem("Assets/Create/AssetFlow/Model", priority = 81)]
        public static void CreateModel()
        {
            SelectCreated(AssetFlowConfigFactory.CreateModelConfig(GetSelectedFolder()));
        }

        [MenuItem("Assets/Create/AssetFlow/Audio", priority = 82)]
        public static void CreateAudio()
        {
            SelectCreated(AssetFlowConfigFactory.CreateAudioConfig(GetSelectedFolder()));
        }

        private static string GetSelectedFolder()
        {
            var selected = Selection.activeObject;
            var path = selected == null ? "Assets" : AssetDatabase.GetAssetPath(selected);
            if (AssetDatabase.IsValidFolder(path))
                return path;

            var slash = path.LastIndexOf('/');
            return slash < 0 ? "Assets" : path.Substring(0, slash);
        }

        private static void SelectCreated(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
