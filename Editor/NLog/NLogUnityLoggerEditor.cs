using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(NLogUnityLogger))]
public class NLogUnityLoggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        NLogUnityLogger logger = (NLogUnityLogger)target;
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("로그 설정", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        
        SerializedProperty logFolderNameProp = serializedObject.FindProperty("logFolderName");
        EditorGUILayout.PropertyField(logFolderNameProp, new GUIContent("로그 폴더명"));
        
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("로그 파일은 프로젝트 루트 폴더의 지정된 폴더에 저장됩니다.", MessageType.Info);
    }
} 