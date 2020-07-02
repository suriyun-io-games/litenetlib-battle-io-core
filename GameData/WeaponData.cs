using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LiteNetLibManager;
using LiteNetLib;

public class WeaponData : ItemData
{
    public GameObject rightHandObject;
    public GameObject leftHandObject;
    public GameObject shieldObject;
    public List<AttackAnimation> attackAnimations;
    public DamageEntity damagePrefab;
    [Header("SFX")]
    public AudioClip[] attackFx;
    public int weaponAnimId;
    public readonly Dictionary<short, AttackAnimation> AttackAnimations = new Dictionary<short, AttackAnimation>();

    public void Launch(CharacterEntity attacker, byte actionId)
    {
        if (attacker == null || !GameNetworkManager.Singleton.IsServer)
            return;

        var characterColliders = Physics.OverlapSphere(attacker.CacheTransform.position, damagePrefab.GetAttackRange() + 5f, 1 << GameInstance.Singleton.characterLayer);
        var gameplayManager = GameplayManager.Singleton;
        var spread = attacker.TotalSpreadDamages;
        var damage = (float)attacker.TotalAttack;
        damage += Random.Range(gameplayManager.minAttackVaryRate, gameplayManager.maxAttackVaryRate) * damage;
        if (gameplayManager.divideSpreadedDamageAmount)
            damage /= spread;

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
            // An transform's rotation, position will be set when set `Attacker`
            // So don't worry about them before damage entity going to spawn
            // Velocity also being set when set `Attacker` too.
            var direction = attacker.CacheTransform.forward;

            var damagePrefab = this.damagePrefab;
            if (AttackAnimations.ContainsKey(actionId) &&
                AttackAnimations[actionId].damagePrefab != null)
                damagePrefab = AttackAnimations[actionId].damagePrefab;
            var damageEntity = DamageEntity.InstantiateNewEntity(damagePrefab, AttackAnimations[actionId].isAnimationForLeftHandWeapon, direction, attacker.ObjectId, addRotationX, addRotationY);
            if (damageEntity)
            {
                damageEntity.weaponDamage = Mathf.CeilToInt(damage);
                damageEntity.hitEffectType = CharacterEntity.RPC_EFFECT_DAMAGE_HIT;
                damageEntity.relateDataId = GetHashId();
                damageEntity.actionId = actionId;
            }

            // Telling nearby clients that the character use weapon
            var msg = new OpMsgCharacterAttack();
            msg.weaponId = GetHashId();
            msg.actionId = actionId;
            msg.direction = direction;
            msg.attackerNetId = attacker.ObjectId;
            msg.addRotationX = addRotationX;
            msg.addRotationY = addRotationY;

            foreach (var characterCollider in characterColliders)
            {
                var character = characterCollider.GetComponent<CharacterEntity>();
                if (character != null && !(character is BotEntity) && !(character is MonsterEntity))
                    GameNetworkManager.Singleton.ServerSendPacket(character.ConnectionId, DeliveryMethod.ReliableOrdered, msg.OpId, msg);
            }
            addRotationY += addingRotationY;
        }

        attacker.RpcEffect(attacker.ObjectId, CharacterEntity.RPC_EFFECT_DAMAGE_SPAWN, GetHashId(), actionId);
    }

    public void SetupAnimations()
    {
        foreach (var attackAnimation in attackAnimations)
        {
            AttackAnimations[attackAnimation.actionId] = attackAnimation;
        }
    }

    public AttackAnimation GetRandomAttackAnimation()
    {
        var list = AttackAnimations.Values.ToList();
        var randomedIndex = Random.Range(0, list.Count);
        return list[randomedIndex];
    }
}
