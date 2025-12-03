//using UnityEditor;
//using UnityEngine;

///// <summary>
///// FolderSettings 的自定义 Inspector 编辑器
///// </summary>
//[CustomEditor(typeof(FolderSettings))]
//public class FolderSettingsEditor : UnityEditor.Editor
//{
//    private SerializedProperty applyToSubfoldersProperty;
//    private SerializedProperty validatorsProperty;
//    private SerializedProperty processorsProperty;

//    private void OnEnable()
//    {
//        applyToSubfoldersProperty = serializedObject.FindProperty("applyToSubfolders");
//        validatorsProperty = serializedObject.FindProperty("validators");
//        processorsProperty = serializedObject.FindProperty("processors");
//    }

//    public override void OnInspectorGUI()
//    {
//        serializedObject.Update();

//        if (applyToSubfoldersProperty != null)
//        {
//            EditorGUILayout.PropertyField(applyToSubfoldersProperty, new GUIContent("应用到子文件夹"));
//            EditorGUILayout.Space();
//        }
        
//        if (validatorsProperty != null)
//        {
//            EditorGUILayout.PropertyField(validatorsProperty, true);
//            EditorGUILayout.Space();
//        }
        
//        if (processorsProperty != null)
//        {
//            EditorGUILayout.PropertyField(processorsProperty, true);
//        }

//        serializedObject.ApplyModifiedProperties();
//    }
//}

