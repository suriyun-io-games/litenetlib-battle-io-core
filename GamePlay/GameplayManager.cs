using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class GameplayManager : NetworkBehaviour
{
    public const float REAL_MOVE_SPEED_RATE = 0.1f;
    public static GameplayManager Singleton { get; private set; }
    [Header("Character stats")]
    public int maxLevel = 1000;
    public IntAttribute exp = new IntAttribute() { minValue = 20, maxValue = 1023050, growth = 2.5f };
    public IntAttribute rewardExp = new IntAttribute() { minValue = 8, maxValue = 409220, growth = 2.5f };
    public IntAttribute killScore = new IntAttribute() { minValue = 10, maxValue = 511525, growth = 1f };
    public int minHp = 100;
    public int minAttack = 30;
    public int minDefend = 20;
    public int minMoveSpeed = 30;
    public int maxSpreadDamages = 6;
    public int addingStatPoint = 1;
    public float minAttackVaryRate = -0.07f;
    public float maxAttackVaryRate = 0.07f;
    public CharacterAttributes[] availableAttributes;
    [Header("UI")]
    public UIGameplay uiGameplay;
    [Header("Game rules")]
    public int watchAdsRespawnAvailable = 2;
    public float respawnDuration = 5f;
    public float invincibleDuration = 1.5f;
    public SpawnArea[] characterSpawnAreas;
    public SpawnArea[] powerUpSpawnAreas;
    public PowerUpSpawnData[] powerUps;
    public readonly Dictionary<string, PowerUpEntity> powerUpEntities = new Dictionary<string, PowerUpEntity>();
    public readonly Dictionary<string, CharacterAttributes> attributes = new Dictionary<string, CharacterAttributes>();

    private void Awake()
    {
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;

        powerUpEntities.Clear();
        foreach (var powerUp in powerUps)
        {
            var powerUpPrefab = powerUp.powerUpPrefab;
            if (powerUpPrefab != null && !ClientScene.prefabs.ContainsValue(powerUpPrefab.gameObject))
                ClientScene.RegisterPrefab(powerUpPrefab.gameObject);
            if (powerUpPrefab != null && !powerUpEntities.ContainsKey(powerUpPrefab.name))
                powerUpEntities.Add(powerUpPrefab.name, powerUpPrefab);
        }
        attributes.Clear();
        foreach (var availableAttribute in availableAttributes)
        {
            attributes[availableAttribute.name] = availableAttribute;
        }
    }

    public override void OnStartServer()
    {
        foreach (var powerUp in powerUps)
        {
            if (powerUp.powerUpPrefab == null)
                continue;
            for (var i = 0; i < powerUp.amount; ++i)
                SpawnPowerUp(powerUp.powerUpPrefab.name);
        }
    }

    public void SpawnPowerUp(string prefabName)
    {
        if (!isServer || string.IsNullOrEmpty(prefabName))
            return;
        PowerUpEntity powerUpPrefab = null;
        if (powerUpEntities.TryGetValue(prefabName, out powerUpPrefab)) {
            var powerUpEntity = Instantiate(powerUpPrefab, GetPowerUpSpawnPosition(), Quaternion.identity);
            powerUpEntity.prefabName = prefabName;
            NetworkServer.Spawn(powerUpEntity.gameObject);
        }
    }

    public Vector3 GetCharacterSpawnPosition(CharacterEntity character)
    {
        if (characterSpawnAreas == null || characterSpawnAreas.Length == 0)
            return Vector3.zero;
        return characterSpawnAreas[Random.Range(0, characterSpawnAreas.Length)].GetSpawnPosition();
    }

    public Vector3 GetPowerUpSpawnPosition()
    {
        if (powerUpSpawnAreas == null || powerUpSpawnAreas.Length == 0)
            return Vector3.zero;
        return powerUpSpawnAreas[Random.Range(0, powerUpSpawnAreas.Length)].GetSpawnPosition();
    }

    public int GetExp(int currentLevel)
    {
        return exp.Calculate(currentLevel, maxLevel);
    }

    public int GetRewardExp(int currentLevel)
    {
        return rewardExp.Calculate(currentLevel, maxLevel);
    }

    public int GetKillScore(int currentLevel)
    {
        return killScore.Calculate(currentLevel, maxLevel);
    }
}
