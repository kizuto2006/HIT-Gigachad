using UnityEditor;
using UnityEngine;

public static class RecreateMummy
{
    [MenuItem("Tools/Recreate Mummy Auto")]
    public static void Run()
    {
        var settings = new EnemyPrefabCreationSettings
        {
            enemyName = "Mummy",
            sourceVisual = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/Enemy/Mummy/EnemyAnimation/Walking.fbx"),
            templateEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Mummy.prefab"),
            prefabOutputFolder = "Assets/Prefab",
            dataOutputFolder = "Assets/Resources/Enemies",
            hp = 12f,
            attack = 5f,
            speed = 3f,
            armor = 0f,
            size = EnemySize.Medium,
            copyConfigurationFromTemplate = true,
            autoFitCollider = true,
            isTrigger = true
        };

        if (settings.sourceVisual == null)
        {
            Debug.LogError("Could not find source visual at Assets/Model/Enemy/Mummy/EnemyAnimation/Walking.fbx");
            return;
        }

        EnemyPrefabCreatorUtility.ApplyTemplateDefaults(settings);

        try
        {
            var result = EnemyPrefabCreatorUtility.CreateEnemy(
                settings, 
                EnemyPrefabCreatorUtility.GetPrefabPath(settings), 
                EnemyPrefabCreatorUtility.GetDataPath(settings), 
                true
            );
            Debug.Log("Successfully remade Mummy: " + result.prefabPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error recreating mummy: " + e.Message);
        }
    }
}
