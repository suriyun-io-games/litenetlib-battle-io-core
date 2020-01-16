using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterEntity : CharacterEntity
{
    public enum Characteristic
    {
        Aggressive,
        NoneAttack,
        NoneAggressive,
    }
    public const float ReachedTargetDistance = 0.1f;
    [Header("Monster config set here")]
    public float wanderDistanceAroundSpawnPosition = 1f;
    public float updateWanderDuration = 2f;
    public float attackDuration = 2f;
    public float forgetEnemyDuration = 3f;
    public float respawnDuration = 5f;
    public float detectEnemyDistance = 2f;
    public float followEnemyDistance = 5f;
    public float turnSpeed = 5f;
    public Characteristic characteristic;
    public string monsterName;
    public int monsterLevel;
    public int monsterRewardExp;
    public int monsterKillScore;
    public CharacterModel monsterCharacterModel;
    public WeaponData monsterWeaponData;
    public CharacterStats monsterStats;
    [Tooltip("Monster with same type ID won't attack eacn other when it's more than 0")]
    [Range(0, 100)]
    public int monsterTypeId;

    private Vector3 targetPosition;
    private float lastUpdateWanderTime;
    private float lastAttackTime;
    private Vector3 spawnPosition;
    private CharacterEntity enemy;

    public override int Exp
    {
        get { return exp; }
        set { }
    }

    public override CharacterStats SumAddStats
    {
        get { return monsterStats; }
    }

    public override int TotalHp
    {
        get { return SumAddStats.addHp; }
    }

    public override int TotalAttack
    {
        get { return SumAddStats.addAttack; }
    }

    public override int TotalDefend
    {
        get { return SumAddStats.addDefend; }
    }

    public override int TotalMoveSpeed
    {
        get { return SumAddStats.addMoveSpeed; }
    }

    public override float TotalExpRate
    {
        get { return 0; }
    }

    public override float TotalScoreRate
    {
        get { return 0; }
    }

    public override int TotalSpreadDamages
    {
        get { return SumAddStats.addSpreadDamages; }
    }

    public override int RewardExp
    {
        get { return monsterRewardExp; }
    }

    public override int KillScore
    {
        get { return monsterKillScore; }
    }

    public override void OnStartClient()
    {
        // Override base function to changes functionality, to set character model / weapon data here
        characterModel = monsterCharacterModel;
        weaponData = monsterWeaponData;
    }

    public override void OnStartServer()
    {
        // Override base function to changes functionality, to set character model / weapon data here
        characterModel = monsterCharacterModel;
        weaponData = monsterWeaponData;
        playerName = monsterName;
        level = monsterLevel;
        spawnPosition = CacheTransform.position;
        lastUpdateWanderTime = Time.unscaledTime - updateWanderDuration;
        lastAttackTime = Time.unscaledTime - attackDuration;
        ServerSpawn(false);
    }

    public override void OnStartLocalPlayer()
    {
        // Override base function to changes functionality, to do nothing
    }

    protected override void OnHeadChanged(int value)
    {
        // Override base function to changes functionality, to do nothing
    }

    protected override void OnCharacterChanged(int value)
    {
        // Override base function to changes functionality, to do nothing
    }

    protected override void OnWeaponChanged(int value)
    {
        // Override base function to changes functionality, to do nothing
    }

    protected override void UpdateMovements()
    {
        if (!isServer)
            return;

        if (GameNetworkManager.Singleton.numPlayers <= 0)
        {
            attackingActionId = -1;
            return;
        }


        if (Hp <= 0)
        {
            ServerRespawn(false);
            CacheRigidbody.velocity = new Vector3(0, CacheRigidbody.velocity.y, 0);
            return;
        }

        if (enemy != null)
        {
            if (Vector3.Distance(spawnPosition, CacheTransform.position) >= followEnemyDistance)
            {
                targetPosition = spawnPosition;
                targetPosition.y = 0;
            }
            else
            {
                targetPosition = enemy.CacheTransform.position;
                targetPosition.y = 0;
            }
        }
        else if (Time.unscaledTime - lastUpdateWanderTime >= updateWanderDuration)
        {
            lastUpdateWanderTime = Time.unscaledTime;
            targetPosition = new Vector3(
                spawnPosition.x + Random.Range(-1f, 1f) * wanderDistanceAroundSpawnPosition,
                0,
                spawnPosition.z + Random.Range(-1f, 1f) * wanderDistanceAroundSpawnPosition);
        }

        var rotatePosition = targetPosition;
        if (enemy == null || enemy.IsDead || Time.unscaledTime - lastAttackTime >= forgetEnemyDuration)
        {
            // Try find enemy
            if (FindEnemy(out enemy))
            {
                lastAttackTime = Time.unscaledTime;
            }
        }
        else
        {
            // Set target rotation to enemy position
            rotatePosition = enemy.CacheTransform.position;
        }

        attackingActionId = -1;
        if (enemy != null)
        {
            switch (characteristic)
            {
                case Characteristic.Aggressive:
                case Characteristic.NoneAggressive:
                    if (Time.unscaledTime - lastAttackTime >= attackDuration &&
                        Vector3.Distance(enemy.CacheTransform.position, CacheTransform.position) < GetAttackRange())
                    {
                        // Attack when nearby enemy
                        attackingActionId = weaponData.GetRandomAttackAnimation().actionId;
                        lastAttackTime = Time.unscaledTime;
                    }
                    break;
            }
        }

        // Gets a vector that points from the player's position to the target's.
        if (!IsReachedTargetPosition())
            Move((targetPosition - CacheTransform.position).normalized);

        if (IsReachedTargetPosition())
        {
            targetPosition = CacheTransform.position + (CacheTransform.forward * ReachedTargetDistance / 2f);
            CacheRigidbody.velocity = new Vector3(0, CacheRigidbody.velocity.y, 0);
        }
        // Rotate to target
        var rotateHeading = rotatePosition - CacheTransform.position;
        var targetRotation = Quaternion.LookRotation(rotateHeading);
        CacheTransform.rotation = Quaternion.Lerp(CacheTransform.rotation, Quaternion.Euler(0, targetRotation.eulerAngles.y, 0), Time.deltaTime * turnSpeed);
    }

    private bool IsReachedTargetPosition()
    {
        if (enemy != null)
            return Vector3.Distance(targetPosition, CacheTransform.position) < Mathf.Min(enemy.CacheCollider.bounds.size.x, enemy.CacheCollider.bounds.size.z);
        return Vector3.Distance(targetPosition, CacheTransform.position) < ReachedTargetDistance;
    }

    private bool FindEnemy(out CharacterEntity enemy)
    {
        enemy = null;
        var colliders = Physics.OverlapSphere(CacheTransform.position, detectEnemyDistance);
        foreach (var collider in colliders)
        {
            var character = collider.GetComponent<CharacterEntity>();
            if (character is MonsterEntity &&
                (character as MonsterEntity).monsterTypeId > 0 &&
                (character as MonsterEntity).monsterTypeId == monsterTypeId)
                continue;

            if (character != null && character != this && character.Hp > 0)
            {
                enemy = character;
                return true;
            }
        }
        return false;
    }

    public override bool ReceiveDamage(CharacterEntity attacker, int damage, byte type, int dataId)
    {
        if (base.ReceiveDamage(attacker, damage, type, dataId))
        {
            switch (characteristic)
            {
                case Characteristic.Aggressive:
                case Characteristic.NoneAggressive:
                    if (enemy == null)
                        enemy = attacker;
                    else if (Random.value > 0.5f)
                        enemy = attacker;
                    break;
            }
            return true;
        }
        return false;
    }

    public override void OnSpawn()
    {
        base.OnSpawn();
        weaponData = monsterWeaponData;
        level = monsterLevel;
    }

    public override bool CanRespawn(params object[] extraParams)
    {
        return Time.unscaledTime - deathTime >= respawnDuration;
    }

    public override Vector3 GetSpawnPosition()
    {
        return spawnPosition;
    }
}
