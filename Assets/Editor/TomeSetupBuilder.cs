using UnityEditor;
using UnityEngine;

public static class TomeSetupBuilder
{
    private const string TomeFolder = "Assets/Resources/Tomes";
    private const string PlayerPrefabPath = "Assets/Prefab/Player.prefab";
    private const string PlayerStatsPath = "Assets/Resources/PlayerStats_Gigachad.asset";

    [MenuItem("Tools/Gigachad/Setup Tomes")]
    public static void Build()
    {
        EnsureFolder("Assets/Resources", "Tomes");

        CreateOrUpdateTome(
            "DamageTome",
            "Damage Tome",
            "Increases all weapon damage.",
            TomeStatType.Damage,
            "Assets/Icons/Tomes/DamageTome.png",
            0.1f);

        CreateOrUpdateTome(
            "SizeTome",
            "Size Tome",
            "Increases the size and attack area of all weapons.",
            TomeStatType.WeaponSize,
            "Assets/Icons/Tomes/SizeTome.png",
            0.1f);

        CreateOrUpdateTome(
            "SpeedTome",
            "Speed Tome",
            "Increases the player's movement speed.",
            TomeStatType.MoveSpeed,
            "Assets/Icons/Tomes/SpeedTome.png",
            0.1f);

        AddInventoryToPlayerPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TomeSetupBuilder] Created three tome assets and configured Player.prefab.");
    }

    private static void CreateOrUpdateTome(
        string fileName,
        string displayName,
        string description,
        TomeStatType statType,
        string iconPath,
        float bonusPerLevel)
    {
        string assetPath = $"{TomeFolder}/{fileName}.asset";
        TomeData tome = AssetDatabase.LoadAssetAtPath<TomeData>(assetPath);
        if (tome == null)
        {
            tome = ScriptableObject.CreateInstance<TomeData>();
            AssetDatabase.CreateAsset(tome, assetPath);
        }

        tome.tomeName = displayName;
        tome.description = description;
        tome.statType = statType;
        tome.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        tome.maxLevel = 5;
        tome.bonusPerLevel = bonusPerLevel;
        EditorUtility.SetDirty(tome);
    }

    private static void AddInventoryToPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            PlayerTomeInventory inventory = root.GetComponent<PlayerTomeInventory>();
            if (inventory == null)
                inventory = root.AddComponent<PlayerTomeInventory>();

            PlayerBaseStats stats = AssetDatabase.LoadAssetAtPath<PlayerBaseStats>(PlayerStatsPath);
            SerializedObject serializedInventory = new SerializedObject(inventory);
            serializedInventory.FindProperty("playerStats").objectReferenceValue = stats;
            serializedInventory.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
