using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public class MonsterEntity : CharacterEntity
{
    public enum Characteristic
    {
        Aggressive,
        NoneAttack,
        NoneAggressive,
    }
    public const float ReachedTargetDistance = 0.1f;
    [Header("Monster configs")]
    public float minimumAttackRange = 5f;
    public float wanderDistanceAroundSpawnPosition = 1f;
    [FormerlySerializedAs("updateWanderDuration")]
    public float updateMovementDuration = 2f;
    public float attackDuration = 2f;
    public float useSkillDuration = 3f;
    public float forgetEnemyDuration = 3f;
    public float respawnDuration = 5f;
    public float detectEnemyDistance = 2f;
    public float followEnemyDistance = 5f;
    public float turnSpeed = 5f;
    public int[] navMeshAreas = new int[] { 0, 1, 2 };
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

    private Queue<Vector3> navPaths = new Queue<Vector3>();
    private Vector3 targetPosition;
    private float lastUpdateMovementTime;
    private float lastAttackTime;
    private float lastUseSkillTime;
    private Vector3 spawnPosition;
    private CharacterEntity enemy;

    public override int Exp
    {
        get { return exp; }
        set { }
    }

    public override CharacterStats SumAddStats
    {
        get
        {
            var stats = monsterStats;
            if (appliedStatusEffects != null)
            {
                foreach (var value in appliedStatusEffects.Values)
                    stats += value.addStats;
            }
            return stats;
        }
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
        UpdateSkills();
    }

    public override void OnStartServer()
    {
        // Override base function to changes functionality, to set character model / weapon data here
        characterModel = monsterCharacterModel;
        weaponData = monsterWeaponData;
        playerName = monsterName;
        level = monsterLevel;
        spawnPosition = CacheTransform.position;
        lastUpdateMovementTime = Time.unscaledTime - updateMovementDuration;
        lastAttackTime = Time.unscaledTime - attackDuration;
        ServerSpawn(false);
        UpdateSkills();
    }

    public override void OnStartOwnerClient()
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
        if (!IsServer)
            return;

        if (GameNetworkManager.Singleton.PlayersCount <= 0)
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
            if (Vector3.Distance(spawnPosition, CacheTransform.position) < followEnemyDistance)
            {
                if (Vector3.Distance(enemy.CacheTransform.position, CacheTransform.position) >= GetAttackRange())
                {
                    GetMovePaths(new Vector3(enemy.CacheTransform.position.x, 0, enemy.CacheTransform.position.z));
                }
                else
                {
                    navPaths.Clear();
                    targetPosition = CacheTransform.position;
                    targetPosition.y = 0;
                }
            }
            else
            {
                navPaths.Clear();
                targetPosition = CacheTransform.position;
                targetPosition.y = 0;
                enemy = null;
                lastUpdateMovementTime = Time.unscaledTime - updateMovementDuration;
            }
        }

        if (Time.unscaledTime - lastUpdateMovementTime >= updateMovementDuration)
        {
            lastUpdateMovementTime = Time.unscaledTime;
            if (enemy == null)
            {
                GetMovePaths(new Vector3(
                    spawnPosition.x + Random.Range(-1f, 1f) * wanderDistanceAroundSpawnPosition,
                    0,
                    spawnPosition.z + Random.Range(-1f, 1f) * wanderDistanceAroundSpawnPosition));
            }
        }

        var lookingPosition = targetPosition;
        if (enemy == null || enemy.IsDead || Time.unscaledTime - lastAttackTime >= forgetEnemyDuration)
        {
            enemy = null;
            // Try find enemy
            switch (characteristic)
            {
                case Characteristic.Aggressive:
                    if (FindEnemy(out enemy))
                    {
                        lastAttackTime = Time.unscaledTime - attackDuration;
                    }
                    break;
            }
        }
        else
        {
            // Set target rotation to enemy position
            lookingPosition = enemy.CacheTransform.position;
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
                        sbyte usingSkillHotkeyId;
                        if (RandomUseSkill(out usingSkillHotkeyId))
                        {
                            this.usingSkillHotkeyId = usingSkillHotkeyId;
                        }
                        else
                            attackingActionId = weaponData.GetRandomAttackAnimation().actionId;
                        lastAttackTime = Time.unscaledTime;
                    }
                    break;
            }
        }

        // Gets a vector that points from the player's position to the target's.
        var isReachedTarget = IsReachedTargetPosition();
        if (!isReachedTarget)
            Move((targetPosition - CacheTransform.position).normalized);

        if (isReachedTarget)
        {
            targetPosition = CacheTransform.position + (CacheTransform.forward * ReachedTargetDistance / 2f);
            CacheRigidbody.velocity = new Vector3(0, CacheRigidbody.velocity.y, 0);
            if (navPaths.Count > 0)
                targetPosition = navPaths.Dequeue();
        }
        // Rotate to target
        var rotateHeading = lookingPosition - CacheTransform.position;
        var targetRotation = Quaternion.LookRotation(rotateHeading);
        CacheTransform.rotation = Quaternion.Lerp(CacheTransform.rotation, Quaternion.Euler(0, targetRotation.eulerAngles.y, 0), Time.deltaTime * turnSpeed);
    }

    void OnDrawGizmos()
    {
        if (path != null && path.corners != null && path.corners.Length > 0)
        {
            for (int i = path.corners.Length - 1; i >= 1; --i)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(path.corners[i], path.corners[i - 1]);
            }
        }
    }

    NavMeshPath path;
    private void GetMovePaths(Vector3 position)
    {
        int areaMask = 0;
        if (navMeshAreas.Length == 0)
        {
            areaMask = NavMesh.AllAreas;
        }
        else
        {
            for (int i = 0; i < navMeshAreas.Length; ++i)
            {
                areaMask = areaMask | 1 << navMeshAreas[i];
            }
        }
        NavMeshPath navPath = new NavMeshPath();
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(position, out navHit, 1000f, areaMask) &&
            NavMesh.CalculatePath(CacheTransform.position, navHit.position, areaMask, navPath))
        {
            path = navPath;
            navPaths = new Queue<Vector3>(navPath.corners);
            // Dequeue first path it's not require for future movement
            navPaths.Dequeue();
            // Set movement
            if (navPaths.Count > 0)
                targetPosition = navPaths.Dequeue();
        }
        else
        {
            targetPosition = position;
        }
    }

    private bool RandomUseSkill(out sbyte hotkeyId)
    {
        hotkeyId = -1;
        if (Time.unscaledTime - lastUseSkillTime < useSkillDuration)
            return false;
        if (Skills == null || Skills.Count == 0)
            return false;
        hotkeyId = Skills.Keys.Skip(Random.Range(0, Skills.Count)).Take(1).First();
        SkillData skill;
        if (Skills.TryGetValue(hotkeyId, out skill) &&
            GetSkillCoolDownCount(hotkeyId) > skill.coolDown)
        {
            lastUseSkillTime = Time.unscaledTime;
            return true;
        }
        hotkeyId = -1;
        return false;
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

            if (character != null && character != this && character.Hp > 0 &&
                Vector3.Distance(spawnPosition, character.CacheTransform.position) < followEnemyDistance)
            {
                enemy = character;
                return true;
            }
        }
        return false;
    }

    public override bool ReceiveDamage(CharacterEntity attacker, int damage, byte type, int dataId, byte actionId)
    {
        if (base.ReceiveDamage(attacker, damage, type, dataId, actionId))
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
        UpdateSkills();
    }

    public override float GetAttackRange()
    {
        float range = base.GetAttackRange();
        if (range < minimumAttackRange)
            return minimumAttackRange;
        return range;
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
