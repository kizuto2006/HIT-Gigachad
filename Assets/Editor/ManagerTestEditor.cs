using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ManagerTest))]
public sealed class ManagerTestEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Play Mode Actions", EditorStyles.boldLabel);

        ManagerTest managerTest = (ManagerTest)target;
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Toggle Fast Forward"))
                managerTest.ToggleFastForward();
            if (GUILayout.Button("Reset Time"))
                managerTest.ResetTimeScale();
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Spawn Boss"))
                managerTest.SpawnBoss();
            if (GUILayout.Button("Level Up"))
                managerTest.LevelUp();
            if (GUILayout.Button("Spawn Enemy Wave"))
                managerTest.SpawnEnemies();
            if (GUILayout.Button("Heal Player"))
                managerTest.HealPlayer();
            if (GUILayout.Button("Kill All Enemies"))
                managerTest.KillAllEnemies();
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField(
                "Current Time Scale",
                Time.timeScale.ToString("0.##"));
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Các nút chỉ hoạt động trong Play Mode. Có thể dùng F5-F11 để test nhanh.",
                MessageType.Info);
        }
    }
}
