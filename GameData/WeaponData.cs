using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

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
    public readonly Dictionary<int, AttackAnimation> AttackAnimations = new Dictionary<int, AttackAnimation>();

    public void Launch(CharacterEntity attacker, int actionId)
    {
        if (attacker == null || !NetworkServer.active)
            return;

        var characterColliders = Physics.OverlapSphere(attacker.CacheTransform.position, damagePrefab.GetAttackRange() + 5f, 1 << GameInstance.Singleton.characterLayer);
        var gameplayManager = GameplayManager.Singleton;
        var spread = attacker.TotalSpreadDamages;
        var damage = (float)attacker.TotalAttack;
        damage += Random.Range(gameplayManager.minAttackVaryRate, gameplayManager.maxAttackVaryRate) * damage;

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
            Transform launchTransform;
            attacker.GetDamageLaunchTransform(AttackAnimations[actionId].isAnimationForLeftHandWeapon, out launchTransform);
            // An transform's rotation, position will be set when set `Attacker`
            // So don't worry about them before damage entity going to spawn
            // Velocity also being set when set `Attacker` too.
            var position = launchTransform.position;
            var direction = attacker.CacheTransform.forward;

            var damagePrefab = this.damagePrefab;
            if (AttackAnimations[actionId].damagePrefab != null)
                damagePrefab = AttackAnimations[actionId].damagePrefab;
            var damageEntity = DamageEntity.InstantiateNewEntity(damagePrefab, AttackAnimations[actionId].isAnimationForLeftHandWeapon, position, direction, attacker.netId, addRotationX, addRotationY);
            damageEntity.weaponDamage = Mathf.CeilToInt(damage);
            damageEntity.hitEffectType = CharacterEntity.RPC_EFFECT_DAMAGE_HIT;
            damageEntity.relateDataId = GetHashId();

            // Telling nearby clients that the character use weapon
            var msg = new OpMsgCharacterAttack();
            msg.weaponId = GetHashId();
            msg.position = position;
            msg.direction = direction;
            msg.attackerNetId = attacker.netId;
            msg.addRotationX = addRotationX;
            msg.addRotationY = addRotationY;

            foreach (var characterCollider in characterColliders)
            {
                var character = characterCollider.GetComponent<CharacterEntity>();
                if (character != null && !(character is BotEntity) && !(character is MonsterEntity))
                    NetworkServer.SendToClient(character.connectionToClient.connectionId, msg.OpId, msg);
            }
            addRotationY += addingRotationY;
        }

        attacker.RpcEffect(attacker.netId, CharacterEntity.RPC_EFFECT_DAMAGE_SPAWN, GetHashId());
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
