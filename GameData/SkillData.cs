﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;
using LiteNetLib;

public class SkillData : ScriptableObject
{
    public string GetId()
    {
        return name;
    }

    public int GetHashId()
    {
        return GetId().MakeHashId();
    }
    
    [Range(0, 7)]
    public sbyte hotkeyId;
    public Sprite icon;
    public AttackAnimation attackAnimation;
    public DamageEntity damagePrefab;
    [Tooltip("This status will be applied to user when use skill")]
    public StatusEffectEntity statusEffectPrefab;
    [Tooltip("This will increase to weapon damage to calculate skill damage" +
        "Ex. weaponDamage => 10 * this => 1, skill damage = 10 + 1 = 11")]
    public int increaseDamage;
    [Tooltip("This will multiplies to weapon damage then increase to weapon damage to calculate skill damage." +
        "Ex. weaponDamage => 10 * this => 0.1, skill damage = 10 + (10 * 0.1) = 11")]
    public float increaseDamageByRate;
    public int spreadDamages = 0;
    public float coolDown = 3;
    [Header("SFX")]
    public AudioClip[] attackFx;
    private HashSet<long> launchedConnectionIds = new HashSet<long>();

    public void Launch(CharacterEntity attacker, Vector3 targetPosition)
    {
        if (attacker == null || !GameNetworkManager.Singleton.IsServer)
            return;

        attacker.RpcEffect(attacker.ObjectId, CharacterEntity.RPC_EFFECT_SKILL_SPAWN, GetHashId(), 0);

        if (attacker.IsServer && statusEffectPrefab && Random.value <= statusEffectPrefab.applyRate && GameplayManager.Singleton.CanApplyStatusEffect(attacker, null))
            attacker.RpcApplyStatusEffect(statusEffectPrefab.GetHashId(), attacker.Identity.ObjectId);

        if (!damagePrefab)
            return;

        var characterColliders = Physics.OverlapSphere(attacker.CacheTransform.position, damagePrefab.GetAttackRange() + 5f, 1 << GameInstance.Singleton.characterLayer);
        var spread = 1 + spreadDamages;
        var damage = (float)attacker.TotalAttack + increaseDamage + (attacker.TotalAttack * increaseDamageByRate);
        damage += Random.Range(GameplayManager.Singleton.minAttackVaryRate, GameplayManager.Singleton.maxAttackVaryRate) * damage;

        var addRotationX = 0f;
        var addRotationY = 0f;
        var addingRotationY = 360f / spread;

        if (spread <= 16)
        {
            addRotationY = (-(spread - 1) * 15f);
            addingRotationY = 30f;
        }

        for (var i = 0; i < spread; ++i)
        {
            var damageEntity = DamageEntity.InstantiateNewEntity(damagePrefab, false, targetPosition, attacker.ObjectId, addRotationX, addRotationY);
            if (damageEntity)
            {
                damageEntity.weaponDamage = Mathf.CeilToInt(damage);
                damageEntity.hitEffectType = CharacterEntity.RPC_EFFECT_SKILL_HIT;
                damageEntity.relateDataId = GetHashId();
                damageEntity.actionId = 0;
            }

            // Telling nearby clients that the character use skills
            var msg = new OpMsgCharacterUseSkill();
            msg.skillId = GetHashId();
            msg.targetPosition = targetPosition;
            msg.attackerNetId = attacker.ObjectId;
            msg.addRotationX = addRotationX;
            msg.addRotationY = addRotationY;

            launchedConnectionIds.Clear();
            foreach (var characterCollider in characterColliders)
            {
                var character = characterCollider.GetComponent<CharacterEntity>();
                if (character != null && !(character is BotEntity) && !(character is MonsterEntity))
                {
                    if (launchedConnectionIds.Contains(character.ConnectionId)) continue;
                    launchedConnectionIds.Add(character.ConnectionId);
                    GameNetworkManager.Singleton.ServerSendPacket(character.ConnectionId, 0, DeliveryMethod.ReliableOrdered, msg.OpId, msg);
                }
            }
            addRotationY += addingRotationY;
        }
    }
}
