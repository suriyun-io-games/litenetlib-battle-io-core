﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;

public class GameInstance : BaseNetworkGameInstance
{
    public static new GameInstance Singleton { get; private set; }
    public CharacterEntity characterPrefab;
    public BotEntity botPrefab;
    public CharacterData[] characters;
    public HeadData[] heads;
    public WeaponData[] weapons;
    public CustomEquipmentData[] customEquipments;
    public BotData[] bots;
    [Tooltip("Physic layer for characters to avoid it collision")]
    public int characterLayer = 8;
    public bool showJoystickInEditor = true;
    public string watchAdsRespawnPlacement = "respawnPlacement";
    // An available list, list of item that already unlocked
    public static readonly List<HeadData> AvailableHeads = new List<HeadData>();
    public static readonly List<CharacterData> AvailableCharacters = new List<CharacterData>();
    public static readonly List<WeaponData> AvailableWeapons = new List<WeaponData>();
    public static readonly List<CustomEquipmentData> AvailableCustomEquipments = new List<CustomEquipmentData>();
    // All item list
    public static readonly Dictionary<int, HeadData> Heads = new Dictionary<int, HeadData>();
    public static readonly Dictionary<int, CharacterData> Characters = new Dictionary<int, CharacterData>();
    public static readonly Dictionary<int, WeaponData> Weapons = new Dictionary<int, WeaponData>();
    public static readonly Dictionary<int, CustomEquipmentData> CustomEquipments = new Dictionary<int, CustomEquipmentData>();
    public static readonly Dictionary<int, SkillData> Skills = new Dictionary<int, SkillData>();
    public static readonly Dictionary<int, StatusEffectEntity> StatusEffects = new Dictionary<int, StatusEffectEntity>();

    public bool PlayerSaveValidated { get; protected set; } = false;

    protected override void Awake()
    {
        base.Awake();
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;
        DontDestroyOnLoad(gameObject);
        Physics.IgnoreLayerCollision(characterLayer, characterLayer, true);
    }

    protected override void Start()
    {
        base.Start();
        
        Skills.Clear();
        StatusEffects.Clear();
        Heads.Clear();
        foreach (var head in heads)
        {
            if (!head) continue;
            if (head.skills != null)
                AddSkills(head.skills);
            Heads[head.GetHashId()] = head;
        }

        Characters.Clear();
        foreach (var character in characters)
        {
            if (!character) continue;
            if (character.skills != null)
                AddSkills(character.skills);
            Characters[character.GetHashId()] = character;
        }

        Weapons.Clear();
        List<StatusEffectEntity> allStatusEffects = new List<StatusEffectEntity>();
        foreach (var weapon in weapons)
        {
            if (!weapon) continue;
            if (weapon.skills != null)
                AddSkills(weapon.skills);
            weapon.SetupAnimations();
            foreach (var attackAnimation in weapon.attackAnimations)
            {
                if (attackAnimation.damagePrefab && attackAnimation.damagePrefab && attackAnimation.damagePrefab.statusEffectPrefab)
                    allStatusEffects.Add(attackAnimation.damagePrefab.statusEffectPrefab);
            }
            Weapons[weapon.GetHashId()] = weapon;
        }
        AddStatusEffectEntities(allStatusEffects.ToArray());

        CustomEquipments.Clear();
        foreach (var customEquipment in customEquipments)
        {
            if (!customEquipment) continue;
            if (customEquipment.skills != null)
                AddSkills(customEquipment.skills);
            CustomEquipments[customEquipment.GetHashId()] = customEquipment;
        }
    }

    private void LateUpdate()
    {
        UpdateAvailableItems();
        if (!PlayerSaveValidated && MonetizationManager.Save.IsPurchasedItemsLoaded)
        {
            PlayerSaveValidated = true;
            ValidatePlayerSave();
        }
    }

    public void AddSkills(SkillData[] skills)
    {
        if (skills == null) return;
        List<StatusEffectEntity> allStatusEffects = new List<StatusEffectEntity>();
        foreach (var skill in skills)
        {
            if (!skill) continue;
            if (skill.statusEffectPrefab)
                allStatusEffects.Add(skill.statusEffectPrefab);
            if (skill.damagePrefab && skill.damagePrefab.statusEffectPrefab)
                allStatusEffects.Add(skill.damagePrefab.statusEffectPrefab);
            if (skill.attackAnimation.damagePrefab && skill.attackAnimation.damagePrefab && skill.attackAnimation.damagePrefab.statusEffectPrefab)
                allStatusEffects.Add(skill.attackAnimation.damagePrefab.statusEffectPrefab);
            Skills[skill.GetHashId()] = skill;
        }
        AddStatusEffectEntities(allStatusEffects.ToArray());
    }

    public void AddStatusEffectEntities(StatusEffectEntity[] statusEffectEntities)
    {
        if (statusEffectEntities == null) return;
        foreach (var statusEffectEntity in statusEffectEntities)
        {
            if (!statusEffectEntity) continue;
            statusEffectEntity.SetHashId();
            StatusEffects[statusEffectEntity.GetHashId()] = statusEffectEntity;
        }
    }

    public void ValidatePlayerSave()
    {
        var head = PlayerSave.GetHead();
        if (head < 0 || head >= AvailableHeads.Count)
            PlayerSave.SetHead(0);

        var character = PlayerSave.GetCharacter();
        if (character < 0 || character >= AvailableCharacters.Count)
            PlayerSave.SetCharacter(0);

        var weapon = PlayerSave.GetWeapon();
        if (weapon < 0 || weapon >= AvailableWeapons.Count)
            PlayerSave.SetWeapon(0);
    }

    public void UpdateAvailableItems()
    {
        AvailableHeads.Clear();
        foreach (var helmet in heads)
        {
            if (helmet != null && helmet.IsUnlock())
                AvailableHeads.Add(helmet);
        }

        AvailableCharacters.Clear();
        foreach (var character in characters)
        {
            if (character != null && character.IsUnlock())
                AvailableCharacters.Add(character);
        }

        AvailableWeapons.Clear();
        foreach (var weapon in weapons)
        {
            if (weapon != null && weapon.IsUnlock())
                AvailableWeapons.Add(weapon);
        }

        AvailableCustomEquipments.Clear();
        foreach (var customEquipment in customEquipments)
        {
            if (customEquipment != null && customEquipment.IsUnlock())
                AvailableCustomEquipments.Add(customEquipment);
        }
    }

    public static HeadData GetHead(int key)
    {
        if (Heads.Count == 0)
            return null;
        HeadData result;
        Heads.TryGetValue(key, out result);
        return result;
    }

    public static CharacterData GetCharacter(int key)
    {
        if (Characters.Count == 0)
            return null;
        CharacterData result;
        Characters.TryGetValue(key, out result);
        return result;
    }

    public static WeaponData GetWeapon(int key)
    {
        if (Weapons.Count == 0)
            return null;
        WeaponData result;
        Weapons.TryGetValue(key, out result);
        return result;
    }

    public static CustomEquipmentData GetCustomEquipment(int key)
    {
        if (CustomEquipments.Count == 0)
            return null;
        CustomEquipmentData result;
        CustomEquipments.TryGetValue(key, out result);
        return result;
    }

    public static HeadData GetAvailableHead(int index)
    {
        if (AvailableHeads.Count == 0)
            return null;
        if (index <= 0 || index >= AvailableHeads.Count)
            index = 0;
        return AvailableHeads[index];
    }

    public static CharacterData GetAvailableCharacter(int index)
    {
        if (AvailableCharacters.Count == 0)
            return null;
        if (index <= 0 || index >= AvailableCharacters.Count)
            index = 0;
        return AvailableCharacters[index];
    }

    public static WeaponData GetAvailableWeapon(int index)
    {
        if (AvailableWeapons.Count == 0)
            return null;
        if (index <= 0 || index >= AvailableWeapons.Count)
            index = 0;
        return AvailableWeapons[index];
    }

    public static CustomEquipmentData GetAvailableCustomEquipment(int index)
    {
        if (AvailableCustomEquipments.Count == 0)
            return null;
        if (index <= 0 || index >= AvailableCustomEquipments.Count)
            index = 0;
        return AvailableCustomEquipments[index];
    }

    public List<ItemData> GetAllItems()
    {
        List<ItemData> allItems = new List<ItemData>();
        foreach (var character in characters)
        {
            allItems.Add(character);
        }
        foreach (var head in heads)
        {
            allItems.Add(head);
        }
        foreach (var weapon in weapons)
        {
            allItems.Add(weapon);
        }
        foreach (var customEquipment in customEquipments)
        {
            allItems.Add(customEquipment);
        }
        return allItems;
    }
}
