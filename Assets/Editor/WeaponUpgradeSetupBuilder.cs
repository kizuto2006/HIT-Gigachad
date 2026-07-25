using UnityEditor;
using UnityEngine;

public static class WeaponUpgradeSetupBuilder
{
    private const string PlayerPrefabPath = "Assets/Prefab/Player.prefab";
    private const string AuraPath = "Assets/Resources/Weapons/Aura.asset";
    private const string SwordPath = "Assets/Resources/Weapons/Sword.asset";
    private const string DamageTomePath = "Assets/Resources/Tomes/DamageTome.asset";
    private const string SizeTomePath = "Assets/Resources/Tomes/SizeTome.asset";
    private const string SpeedTomePath = "Assets/Resources/Tomes/SpeedTome.asset";
    private const string UpgradeUIPath = "Assets/UI/Prefabs/WeaponUpgradePanel.prefab";

    [MenuItem("Tools/Gigachad/Setup Weapon Upgrade System")]
    public static void Build()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            UpgradeManager manager = root.GetComponent<UpgradeManager>();
            if (manager == null)
                manager = root.AddComponent<UpgradeManager>();

            XPSystem xpSystem = root.GetComponentInChildren<XPSystem>(true);
            WeaponController weaponController = root.GetComponentInChildren<WeaponController>(true);
            PlayerTomeInventory tomeInventory = root.GetComponentInChildren<PlayerTomeInventory>(true);
            WeaponData aura = AssetDatabase.LoadAssetAtPath<WeaponData>(AuraPath);
            WeaponData sword = AssetDatabase.LoadAssetAtPath<WeaponData>(SwordPath);
            TomeData damageTome = AssetDatabase.LoadAssetAtPath<TomeData>(DamageTomePath);
            TomeData sizeTome = AssetDatabase.LoadAssetAtPath<TomeData>(SizeTomePath);
            TomeData speedTome = AssetDatabase.LoadAssetAtPath<TomeData>(SpeedTomePath);
            GameObject upgradeUIPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UpgradeUIPath);

            SerializedObject serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("xpSystem").objectReferenceValue = xpSystem;
            serializedManager.FindProperty("weaponController").objectReferenceValue = weaponController;
            serializedManager.FindProperty("tomeInventory").objectReferenceValue = tomeInventory;
            serializedManager.FindProperty("upgradeUIPrefab").objectReferenceValue = upgradeUIPrefab;
            serializedManager.FindProperty("optionsPerLevel").intValue = 3;
            serializedManager.FindProperty("pauseGameWhileChoosing").boolValue = true;

            SerializedProperty weapons = serializedManager.FindProperty("allWeapons");
            weapons.arraySize = 2;
            weapons.GetArrayElementAtIndex(0).objectReferenceValue = aura;
            weapons.GetArrayElementAtIndex(1).objectReferenceValue = sword;

            SerializedProperty tomes = serializedManager.FindProperty("allTomes");
            tomes.arraySize = 3;
            tomes.GetArrayElementAtIndex(0).objectReferenceValue = damageTome;
            tomes.GetArrayElementAtIndex(1).objectReferenceValue = sizeTome;
            tomes.GetArrayElementAtIndex(2).objectReferenceValue = speedTome;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            // Aura is the only starting weapon. Every other weapon enters through level-up choices.
            SerializedObject serializedController = new SerializedObject(weaponController);
            serializedController.FindProperty("weaponSlot1").objectReferenceValue = aura;
            serializedController.FindProperty("weaponSlot2").objectReferenceValue = null;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedTomes = new SerializedObject(tomeInventory);
            serializedTomes.FindProperty("ownedTomes").arraySize = 0;
            serializedTomes.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[WeaponUpgradeSetupBuilder] UpgradeManager configured with Aura, Sword and all Tomes on Player.prefab.");
    }
}
