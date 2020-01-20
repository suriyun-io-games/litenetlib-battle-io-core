﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class BotEntity : CharacterEntity
{
    public enum Characteristic
    {
        Aggressive,
        NoneAttack
    }
    public const float ReachedTargetDistance = 0.1f;
    [Header("Bot config set here")]
    public float updateMovementDuration = 2f;
    public float attackDuration = 0f;
    public float useSkillDuration = 3f;
    public float forgetEnemyDuration = 3f;
    public float randomDashDurationMin = 3f;
    public float randomDashDurationMax = 5f;
    public float randomMoveDistance = 5f;
    public float detectEnemyDistance = 2f;
    public float turnSpeed = 5f;
    public bool useNavMesh;
    public Characteristic characteristic;
    public CharacterStats startAddStats;
    [HideInInspector, System.NonSerialized]
    public bool isFixRandomMoveAroundPoint;
    [HideInInspector, System.NonSerialized]
    public Vector3 fixRandomMoveAroundPoint;
    [HideInInspector, System.NonSerialized]
    public float fixRandomMoveAroundDistance;
    private float lastUpdateMovementTime;
    private float lastAttackTime;
    private float lastUseSkillTime;
    private float randomDashDuration;
    private CharacterEntity enemy;
    private Vector3 dashDirection;
    private Queue<Vector3> navPaths;
    private Vector3 targetPosition;
    private Vector3 lookingPosition;

    public override void OnStartServer()
    {
        base.OnStartServer();

        ServerSpawn(false);
        lastUpdateMovementTime = Time.unscaledTime - updateMovementDuration;
        lastAttackTime = Time.unscaledTime - attackDuration;
        randomDashDuration = dashDuration + Random.Range(randomDashDurationMin, randomDashDurationMax);
    }

    public override void OnStartLocalPlayer()
    {
        // Do nothing
    }

    protected override void UpdateMovements()
    {
        if (!isServer)
            return;

        if (GameNetworkManager.Singleton.numPlayers <= 0)
        {
            attackingActionId = -1;
            CacheRigidbody.velocity = new Vector3(0, CacheRigidbody.velocity.y, 0);
            return;
        }

        if (Hp <= 0)
        {
            ServerRespawn(false);
            CacheRigidbody.velocity = new Vector3(0, CacheRigidbody.velocity.y, 0);
            return;
        }

        // Bots will update target movement when reached move target / hitting the walls / it's time
        var isReachedTarget = IsReachedTargetPosition();
        if (isReachedTarget || Time.unscaledTime - lastUpdateMovementTime >= updateMovementDuration)
        {
            lastUpdateMovementTime = Time.unscaledTime;
            if (enemy != null)
            {
                GetMovePaths(new Vector3(
                    enemy.CacheTransform.position.x + Random.Range(-1f, 1f) * detectEnemyDistance,
                    0,
                    enemy.CacheTransform.position.z + Random.Range(-1f, 1f) * detectEnemyDistance));
            }
            else if (isFixRandomMoveAroundPoint)
            {
                GetMovePaths(new Vector3(
                    fixRandomMoveAroundPoint.x + Random.Range(-1f, 1f) * fixRandomMoveAroundDistance,
                    0,
                    fixRandomMoveAroundPoint.z + Random.Range(-1f, 1f) * fixRandomMoveAroundDistance));
            }
            else
            {
                GetMovePaths(new Vector3(
                    CacheTransform.position.x + Random.Range(-1f, 1f) * randomMoveDistance,
                    0,
                    CacheTransform.position.z + Random.Range(-1f, 1f) * randomMoveDistance));
            }
        }

        lookingPosition = targetPosition;
        if (enemy == null || enemy.IsDead || Time.unscaledTime - lastAttackTime >= forgetEnemyDuration)
        {
            enemy = null;
            // Try find enemy. If found move to target in next frame
            switch (characteristic)
            {
                case Characteristic.Aggressive:
                    if (FindEnemy(out enemy))
                    {
                        lastAttackTime = Time.unscaledTime;
                        lastUpdateMovementTime = Time.unscaledTime - updateMovementDuration;
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
                    if (Time.unscaledTime - lastAttackTime >= attackDuration &&
                    Vector3.Distance(enemy.CacheTransform.position, CacheTransform.position) < GetAttackRange())
                    {
                        // Attack when nearby enemy
                        sbyte usingSkillHotkeyId;
                        if (RandomUseSkill(out usingSkillHotkeyId))
                            this.usingSkillHotkeyId = usingSkillHotkeyId;
                        else
                            attackingActionId = weaponData.GetRandomAttackAnimation().actionId;
                        lastAttackTime = Time.unscaledTime;
                    }
                    break;
            }
        }

        // Dashing
        if (Time.unscaledTime - dashingTime >= randomDashDuration && !isDashing)
        {
            randomDashDuration = dashDuration + Random.Range(randomDashDurationMin, randomDashDurationMax);
            dashDirection = CacheTransform.forward;
            dashDirection.y = 0;
            dashDirection.Normalize();
            isDashing = true;
            dashingTime = Time.unscaledTime;
            CmdDash();
        }

        // Gets a vector that points from the player's position to the target's.
        isReachedTarget = IsReachedTargetPosition();
        if (!isReachedTarget)
            Move(isDashing ? dashDirection : (targetPosition - CacheTransform.position).normalized);

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
        UpdateStatPoint();
    }

    private void GetMovePaths(Vector3 position)
    {
        if (useNavMesh)
        {
            NavMeshPath navPath = new NavMeshPath();
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(position, out navHit, 5f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(CacheTransform.position, navHit.position, NavMesh.AllAreas, navPath))
            {
                navPaths = new Queue<Vector3>(navPath.corners);
                // Dequeue first path it's not require for future movement
                navPaths.Dequeue();
            }
        }
        else
        {
            // If not use nav mesh, just move to position by direction
            navPaths = new Queue<Vector3>();
            navPaths.Enqueue(position);
        }
        // Set first target position immediately
        if (navPaths.Count > 0)
            targetPosition = navPaths.Dequeue();
    }

    private void UpdateStatPoint()
    {
        if (statPoint <= 0)
            return;
        var dict = new Dictionary<CharacterAttributes, int>();
        var list = GameplayManager.Singleton.attributes.Values.ToList();
        foreach (var entry in list)
        {
            dict.Add(entry, entry.randomWeight);
        }
        CmdAddAttribute(WeightedRandomizer.From(dict).TakeOne().name);
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
        var gameplayManager = GameplayManager.Singleton;
        var colliders = Physics.OverlapSphere(CacheTransform.position, detectEnemyDistance);
        foreach (var collider in colliders)
        {
            var character = collider.GetComponent<CharacterEntity>();
            if (character != null && character != this && character.Hp > 0 && gameplayManager.CanReceiveDamage(character, this))
            {
                enemy = character;
                return true;
            }
        }
        return false;
    }

    protected override void OnCollisionStay(Collision collision)
    {
        base.OnCollisionStay(collision);
        if (useNavMesh)
            return;

        if (collision.collider.CompareTag("Wall"))
        {
            // Find another position to move in next frame
            lastUpdateMovementTime = Time.unscaledTime - updateMovementDuration;
        }
    }

    public override bool ReceiveDamage(CharacterEntity attacker, int damage, byte type, int dataId)
    {
        if (base.ReceiveDamage(attacker, damage, type, dataId))
        {
            switch (characteristic)
            {
                case Characteristic.Aggressive:
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
        addStats += startAddStats;
        Hp = TotalHp;
    }
}
