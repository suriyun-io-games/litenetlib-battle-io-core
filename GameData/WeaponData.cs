using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WeaponData : ItemData
{
    public GameObject rightHandObject;
    public GameObject leftHandObject;
    public GameObject shieldObject;
    public List<AttackAnimation> attackAnimations;
    public DamageEntity damagePrefab;
    public readonly Dictionary<int, AttackAnimation> AttackAnimations = new Dictionary<int, AttackAnimation>();

    public void Launch(CharacterEntity attacker, int spread)
    {
        if (attacker == null)
            return;

        var addRotationY = 0f;
        var addingRotationY = 360f / spread;
        
        if (spread == 2)
        {
            addRotationY = -15f;
            addingRotationY = 30f;
        }

        if (spread == 3)
        {
            addRotationY = -30f;
            addingRotationY = 30f;
        }

        for (var i = 0; i < spread; ++i)
        {
            var damageLaunchTransform = attacker.damageLaunchTransform;
            var damageEntity = Instantiate(damagePrefab,
                    damageLaunchTransform.position,
                    damageLaunchTransform.rotation);
            // An transform's rotation, position will be set when set `Attacker`
            // So don't worry about them before damage entity going to spawn
            // Velocity also being set when set `Attacker` too.
            damageEntity.Attacker = attacker;
            damageEntity.addRotationY = addRotationY;
            NetworkServer.Spawn(damageEntity.gameObject);
            addRotationY += addingRotationY;
        }

        attacker.RpcEffect(attacker.netId, CharacterEntity.RPC_EFFECT_DAMAGE_SPAWN);
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
        var randomedIndex = Random.Range(0, list.Count - 1);
        return list[randomedIndex];
    }
}
