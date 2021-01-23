using UnityEngine;
using LiteNetLibManager;
using System.Collections.Generic;

public class IONetworkGameRule : BaseNetworkGameRule
{
    public UIGameplay uiGameplayPrefab;
    public CharacterEntity overrideCharacterPrefab;
    public BotEntity overrideBotPrefab;
    public WeaponData startWeapon;

    public override bool HasOptionBotCount { get { return true; } }
    public override bool HasOptionMatchTime { get { return false; } }
    public override bool HasOptionMatchKill { get { return false; } }
    public override bool HasOptionMatchScore { get { return false; } }
    public override bool ShowZeroScoreWhenDead { get { return true; } }
    public override bool ShowZeroKillCountWhenDead { get { return true; } }
    public override bool ShowZeroAssistCountWhenDead { get { return true; } }
    public override bool ShowZeroDieCountWhenDead { get { return true; } }

    protected override BaseNetworkGameCharacter NewBot()
    {
        var gameInstance = GameInstance.Singleton;
        var botList = gameInstance.bots;
        var bot = botList[Random.Range(0, botList.Length)];
        // Get character prefab
        BotEntity botPrefab = gameInstance.botPrefab;
        if (overrideBotPrefab != null)
            botPrefab = overrideBotPrefab;
        // Set character data
        var botEntity = Instantiate(botPrefab);
        botEntity.playerName = bot.name;
        botEntity.selectHead = bot.GetSelectHead();
        botEntity.selectCharacter = bot.GetSelectCharacter();
        if (startWeapon != null)
            botEntity.selectWeapon = startWeapon.GetHashId();
        else
            botEntity.selectWeapon = bot.GetSelectWeapon();
        return botEntity;
    }

    public virtual void NewPlayer(CharacterEntity character)
    {
        if (startWeapon != null)
            character.selectWeapon = startWeapon.GetHashId();
    }

    public override bool CanCharacterRespawn(BaseNetworkGameCharacter character, params object[] extraParams)
    {
        var gameplayManager = GameplayManager.Singleton;
        var targetCharacter = character as CharacterEntity;
        return gameplayManager.CanRespawn(targetCharacter) && Time.unscaledTime - targetCharacter.deathTime >= gameplayManager.respawnDuration;
    }

    public override bool RespawnCharacter(BaseNetworkGameCharacter character, params object[] extraParams)
    {
        var isWatchedAds = false;
        if (extraParams.Length > 0 && extraParams[0] is bool)
            isWatchedAds = (bool)extraParams[0];

        var targetCharacter = character as CharacterEntity;
        var gameplayManager = GameplayManager.Singleton;
        // For IO Modes, character stats will be reset when dead
        if (!isWatchedAds || targetCharacter.watchAdsCount >= gameplayManager.watchAdsRespawnAvailable)
        {
            targetCharacter.ResetScore();
            targetCharacter.ResetKillCount();
            targetCharacter.ResetAssistCount();
            targetCharacter.Exp = 0;
            targetCharacter.level = 1;
            targetCharacter.statPoint = 0;
            targetCharacter.watchAdsCount = 0;
            targetCharacter.addStats = new CharacterStats();
        }
        else
            ++targetCharacter.watchAdsCount;

        return true;
    }

    public override void InitialClientObjects(LiteNetLibClient client)
    {
        var ui = FindObjectOfType<UIGameplay>();
        if (ui == null && uiGameplayPrefab != null)
            ui = Instantiate(uiGameplayPrefab);
        if (ui != null)
            ui.gameObject.SetActive(true);
    }

    public override void RegisterPrefabs()
    {
        if (GameInstance.Singleton.characterPrefab != null)
            NetworkManager.Assets.RegisterPrefab(GameInstance.Singleton.characterPrefab.Identity);

        if (GameInstance.Singleton.botPrefab != null)
            NetworkManager.Assets.RegisterPrefab(GameInstance.Singleton.botPrefab.Identity);

        if (overrideCharacterPrefab != null)
            NetworkManager.Assets.RegisterPrefab(overrideCharacterPrefab.Identity);

        if (overrideBotPrefab != null)
            NetworkManager.Assets.RegisterPrefab(overrideBotPrefab.Identity);

        foreach (var obj in NetworkManager.Assets.GetSceneObjects())
        {
            var gameplayManager = obj.GetComponentInChildren<GameplayManager>();
            if (gameplayManager)
                gameplayManager.RegisterPrefabs();
        }
    }

    protected override List<BaseNetworkGameCharacter> GetBots()
    {
        return new List<BaseNetworkGameCharacter>(FindObjectsOfType<BotEntity>());
    }
}
