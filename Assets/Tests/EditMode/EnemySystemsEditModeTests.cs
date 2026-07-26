using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EnemySystemsEditModeTests
{
    [Test]
    public void GroupSize_IncreasesOnceEveryThirtySeconds()
    {
        GameObject gameObject = new GameObject("EnemySpawn_Test");
        gameObject.SetActive(false);
        Component spawn = gameObject.AddComponent(GetGameType("EnemySpawn"));
        SetPublicField(spawn, "startingMinGroupSize", 1);
        SetPublicField(spawn, "startingMaxGroupSize", 2);
        SetPublicField(spawn, "groupSizeStepSeconds", 30f);
        SetPublicField(spawn, "groupSizeIncreasePerStep", 1);
        SetPublicField(spawn, "maximumGroupSize", 10);

        try
        {
            AssertGroupSize(spawn, 0f, 1, 2);
            AssertGroupSize(spawn, 29.99f, 1, 2);
            AssertGroupSize(spawn, 30f, 2, 3);
            AssertGroupSize(spawn, 60f, 3, 4);
            AssertGroupSize(spawn, 600f, 10, 10);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void EnemySpawn_DefaultGroundMaskTargetsGroundLayer()
    {
        GameObject gameObject = new GameObject("EnemySpawn_GroundMask_Test");
        gameObject.SetActive(false);
        Component spawn = gameObject.AddComponent(GetGameType("EnemySpawn"));

        try
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.That(groundLayer, Is.EqualTo(7));
            LayerMask mask = GetPrivateField<LayerMask>(spawn, "groundMask");
            Assert.That(mask.value, Is.EqualTo(1 << groundLayer));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void EnemyData_FollowsDesignedRoles()
    {
        UnityEngine.Object mummy = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Resources/Enemies/EnemyData_Mummy.asset");
        UnityEngine.Object skeleton = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Resources/Enemies/EnemyData_Skeleton.asset");
        UnityEngine.Object sandHunter = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Resources/Enemies/EnemyData_SandHunter.asset");

        Assert.That(mummy, Is.Not.Null);
        Assert.That(skeleton, Is.Not.Null);
        Assert.That(sandHunter, Is.Not.Null);
        Assert.That(GetPublicFloat(skeleton, "speed"), Is.GreaterThan(GetPublicFloat(mummy, "speed")));
        Assert.That(GetPublicFloat(mummy, "speed"), Is.GreaterThan(GetPublicFloat(sandHunter, "speed")));
        Assert.That(GetPublicFloat(skeleton, "hp"), Is.LessThan(GetPublicFloat(mummy, "hp")));
        Assert.That(GetPublicFloat(mummy, "hp"), Is.LessThan(GetPublicFloat(sandHunter, "hp")));
        Assert.That(GetPublicFloat(skeleton, "atk"), Is.LessThanOrEqualTo(GetPublicFloat(mummy, "atk")));
        Assert.That(GetPublicFloat(mummy, "atk"), Is.LessThan(GetPublicFloat(sandHunter, "atk")));
    }

    [Test]
    public void EnemyHealth_KeepsAssignedSpawnerOwner()
    {
        GameObject spawnerObject = new GameObject("Spawner_Owner_Test");
        GameObject enemyObject = new GameObject("Enemy_Owner_Test");
        spawnerObject.SetActive(false);
        enemyObject.SetActive(false);
        Component spawn = spawnerObject.AddComponent(GetGameType("EnemySpawn"));
        Component health = enemyObject.AddComponent(GetGameType("EnemyHealth"));

        try
        {
            MethodInfo setSpawner = health.GetType().GetMethod("SetSpawner", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(setSpawner, Is.Not.Null);
            setSpawner.Invoke(health, new object[] { spawn });
            Assert.That(GetPrivateField<Component>(health, "ownerSpawner"), Is.SameAs(spawn));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemyObject);
            UnityEngine.Object.DestroyImmediate(spawnerObject);
        }
    }

    [Test]
    public void GameManager_DetachesFromParentBeforePersistence()
    {
        GameObject parent = new GameObject("Manager_Parent_Test");
        GameObject child = new GameObject("GameManager_Child_Test");
        child.transform.SetParent(parent.transform);
        Component manager = child.AddComponent(GetGameType("GameManager"));

        try
        {
            InvokePrivate(manager, "DetachFromParentForPersistence");
            Assert.That(child.transform.parent, Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(child);
            UnityEngine.Object.DestroyImmediate(parent);
        }
    }

    private static Type GetGameType(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, "Missing runtime type: " + typeName);
        return type;
    }

    private static void AssertGroupSize(Component spawn, float elapsed, int expectedMin, int expectedMax)
    {
        SetPrivateField(spawn, "elapsedTime", elapsed);
        InvokePrivate(spawn, "UpdateCurrentGroupSize");
        Assert.That(GetPrivateField<int>(spawn, "currentMinGroupSize"), Is.EqualTo(expectedMin));
        Assert.That(GetPrivateField<int>(spawn, "currentMaxGroupSize"), Is.EqualTo(expectedMax));
    }

    private static float GetPublicFloat(UnityEngine.Object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
        return (float)field.GetValue(target);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
        field.SetValue(target, value);
    }

    private static void SetPublicField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Missing method: " + methodName);
        method.Invoke(target, null);
    }
}
