using System.Collections.Generic;
using UnityEngine;

public enum EnemyColliderType
{
    CapsuleCollider,
    BoxCollider,
    SphereCollider,
    UseExistingCollider,
    None
}

public sealed class EnemyPrefabCreationSettings
{
    public string enemyName = string.Empty;
    public GameObject sourceVisual;
    public string prefabOutputFolder = "Assets/Prefab";
    public string dataOutputFolder = "Assets/Resources";

    public float hp = 50f;
    public float attack = 5f;
    public float speed = 3f;
    public float armor;
    public EnemySize size = EnemySize.Medium;

    public string enemyTag = "Untagged";
    public string enemyLayer = "Default";
    public EnemyColliderType colliderType = EnemyColliderType.CapsuleCollider;
    public bool autoFitCollider = true;
    public bool isTrigger = true;
    public Vector3 visualLocalPosition = Vector3.zero;
    public Vector3 visualLocalRotation = Vector3.zero;
    public Vector3 visualLocalScale = Vector3.one;
    public bool addAnimatorIfMissing = true;
    public RuntimeAnimatorController animatorController;

    public bool copyConfigurationFromTemplate = true;
    public GameObject templateEnemyPrefab;
}

internal sealed class EnemyPrefabValidationResult
{
    public readonly List<string> errors = new List<string>();
    public readonly List<string> warnings = new List<string>();
    public readonly List<string> information = new List<string>();

    public bool IsValid => errors.Count == 0;
}

internal sealed class EnemyPrefabCreationResult
{
    public string prefabPath;
    public string dataPath;
    public GameObject prefabAsset;
    public EnemyData enemyData;
}
