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
    [Range(0, 100)]
    [System.Obsolete("This will be deprecated on next version, use WeaponData.attackAnimations instead")]
    public int actionId;
    [System.Obsolete("This will be deprecated on next version, use WeaponData.attackAnimations instead")]
    public float animationDuration;
    [System.Obsolete("This will be deprecated on next version, use WeaponData.attackAnimations instead")]
    public float launchDuration;
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
            addRotationY = -45f;
            addingRotationY = 90f;
        }

        if (spread == 3)
        {
            addRotationY = -90f;
            addingRotationY = 90f;
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
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        bool containId = false;
        foreach (var attackAnimation in attackAnimations)
        {
            if (attackAnimation.actionId == actionId)
            {
                containId = true;
                break;
            }
        }
        if (!containId)
        {
            var newAnimation = new AttackAnimation();
            newAnimation.actionId = actionId;
            newAnimation.animationDuration = animationDuration;
            newAnimation.launchDuration = launchDuration;
            attackAnimations.Add(newAnimation);
        }
        EditorUtility.SetDirty(this);
    }
#endif

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
