using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class GameplayManager : NetworkBehaviour
{
    public const float REAL_MOVE_SPEED_RATE = 0.1f;
    public const int RANKING_AMOUNT = 5;
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
    public int botCount = 10;
    public int watchAdsRespawnAvailable = 2;
    public float updateScoreDuration = 1f;
    public float respawnDuration = 5f;
    public float invincibleDuration = 1.5f;
    public SpawnArea[] characterSpawnAreas;
    public SpawnArea[] powerUpSpawnAreas;
    public PowerUpSpawnData[] powerUps;
    public readonly List<CharacterEntity> characters = new List<CharacterEntity>();
    public readonly Dictionary<string, PowerUpEntity> powerUpEntities = new Dictionary<string, PowerUpEntity>();
    public readonly Dictionary<string, CharacterAttributes> attributes = new Dictionary<string, CharacterAttributes>();
    private UserRanking[] userRankings = new UserRanking[RANKING_AMOUNT];
    // Private
    private float lastUpdateScoreTime;

    private void Awake()
    {
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;
        lastUpdateScoreTime = Time.unscaledTime;

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
        var gameInstance = GameInstance.Singleton;
        var botList = gameInstance.bots;
        var characterKeys = GameInstance.Characters.Keys;
        var weaponKeys = GameInstance.Weapons.Keys;
        for (var i = 0; i < botCount; ++i)
        {
            var bot = botList[Random.Range(0, botList.Length)];
            var botEntity = Instantiate(gameInstance.botPrefab);
            botEntity.playerName = bot.name;
            botEntity.selectHead = bot.GetSelectHead();
            botEntity.selectCharacter = bot.GetSelectCharacter();
            botEntity.selectWeapon = bot.GetSelectWeapon();
            NetworkServer.Spawn(botEntity.gameObject);
            Singleton.characters.Add(botEntity);
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

    private void Update()
    {
        if (Time.unscaledTime - lastUpdateScoreTime >= updateScoreDuration)
        {
            if (isServer)
                UpdateScores();
            lastUpdateScoreTime = Time.unscaledTime;
        }
    }

    private void UpdateScores()
    {
        characters.Sort();
        userRankings = new UserRanking[RANKING_AMOUNT];
        for (var i = 0; i < RANKING_AMOUNT; ++i)
        {
            if (i >= characters.Count)
                break;
            var character = characters[i];
            var ranking = new UserRanking();
            ranking.netId = character.netId;
            ranking.playerName = character.playerName;
            ranking.score = character.Score;
            ranking.killCount = character.killCount;
            userRankings[i] = ranking;
        }
        RpcUpdateRankings(userRankings);
    }

    public Vector3 GetCharacterSpawnPosition()
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
    
    public void UpdateRank(NetworkInstanceId netId)
    {
        var target = NetworkServer.FindLocalObject(netId);
        if (target == null)
            return;
        var character = target.GetComponent<CharacterEntity>();
        if (character == null)
            return;
        var ranking = new UserRanking();
        ranking.netId = character.netId;
        ranking.playerName = character.playerName;
        ranking.score = character.Score;
        ranking.killCount = character.killCount;
        if (character.connectionToClient != null)
        {
            characters.Sort();
            TargetUpdateLocalRank(character.connectionToClient, characters.IndexOf(character) + 1, ranking);
        }
    }

    [ClientRpc]
    private void RpcUpdateRankings(UserRanking[] userRankings)
    {
        if (uiGameplay != null)
            uiGameplay.UpdateRankings(userRankings);
    }

    [TargetRpc]
    private void TargetUpdateLocalRank(NetworkConnection conn, int rank, UserRanking ranking)
    {
        if (uiGameplay != null)
            uiGameplay.UpdateLocalRank(rank, ranking);
    }
}
