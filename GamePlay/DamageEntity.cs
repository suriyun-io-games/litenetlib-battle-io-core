using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(Rigidbody))]
public class DamageEntity : MonoBehaviour
{
    public EffectEntity spawnEffectPrefab;
    public EffectEntity explodeEffectPrefab;
    public EffectEntity hitEffectPrefab;
    public AudioClip[] hitFx;
    public float radius;
    public float lifeTime;
    public float spawnForwardOffset;
    public float speed;
    public bool relateToAttacker;
    private bool isDead;
    private bool isLeftHandWeapon;
    private NetworkInstanceId attackerNetId;
    private float addRotationX;
    private float addRotationY;
    [HideInInspector]
    public int weaponDamage;
    [HideInInspector]
    public byte hitEffectType;
    [HideInInspector]
    public int relateDataId;

    private CharacterEntity attacker;
    public CharacterEntity Attacker
    {
        get
        {
            if (attacker == null)
            {
                var go = NetworkServer.active ? NetworkServer.FindLocalObject(attackerNetId) : ClientScene.FindLocalObject(attackerNetId);
                if (go != null)
                    attacker = go.GetComponent<CharacterEntity>();
            }
            return attacker;
        }
    }
    public Transform CacheTransform { get; private set; }
    public Rigidbody CacheRigidbody { get; private set; }
    public Collider CacheCollider { get; private set; }

    private void Awake()
    {
        gameObject.layer = Physics.IgnoreRaycastLayer;
        CacheTransform = transform;
        CacheRigidbody = GetComponent<Rigidbody>();
        CacheCollider = GetComponent<Collider>();
        CacheCollider.isTrigger = true;
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// Init Attacker, this function must be call at server to init attacker
    /// </summary>
    public void InitAttackData(bool isLeftHandWeapon, NetworkInstanceId attackerNetId, float addRotationX, float addRotationY)
    {
        this.isLeftHandWeapon = isLeftHandWeapon;
        this.attackerNetId = attackerNetId;
        this.addRotationX = addRotationX;
        this.addRotationY = addRotationY;
        InitTransform();
    }

    private void InitTransform()
    {
        if (Attacker == null)
            return;

        if (relateToAttacker)
        {
            Transform damageLaunchTransform;
            Attacker.GetDamageLaunchTransform(isLeftHandWeapon, out damageLaunchTransform);
            CacheTransform.SetParent(damageLaunchTransform);
            var baseAngles = attacker.CacheTransform.eulerAngles;
            CacheTransform.rotation = Quaternion.Euler(baseAngles.x + addRotationX, baseAngles.y + addRotationY, baseAngles.z);
        }
    }

    private void FixedUpdate()
    {
        UpdateMovement();
    }

    private void UpdateMovement()
    {
        if (Attacker != null)
        {
            if (relateToAttacker)
            {
                if (CacheTransform.parent == null)
                {
                    Transform damageLaunchTransform;
                    Attacker.GetDamageLaunchTransform(isLeftHandWeapon, out damageLaunchTransform);
                    CacheTransform.SetParent(damageLaunchTransform);
                }
                var baseAngles = attacker.CacheTransform.eulerAngles;
                CacheTransform.rotation = Quaternion.Euler(baseAngles.x + addRotationX, baseAngles.y + addRotationY, baseAngles.z);
                CacheRigidbody.velocity = Attacker.CacheRigidbody.velocity + GetForwardVelocity();
            }
            else
                CacheRigidbody.velocity = GetForwardVelocity();
        }
        else
            CacheRigidbody.velocity = GetForwardVelocity();
    }

    private void OnDestroy()
    {
        if (!isDead)
            EffectEntity.PlayEffect(explodeEffectPrefab, CacheTransform);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == Physics.IgnoreRaycastLayer)
            return;

        var otherCharacter = other.GetComponent<CharacterEntity>();
        // Damage will not hit attacker, so avoid it
        if (otherCharacter != null && otherCharacter.netId.Value == attackerNetId.Value)
            return;

        var hitSomeAliveCharacter = false;
        if (otherCharacter != null && otherCharacter.Hp > 0)
        {
            hitSomeAliveCharacter = true;
            ApplyDamage(otherCharacter);
        }

        Collider[] colliders = Physics.OverlapSphere(CacheTransform.position, radius, 1 << GameInstance.Singleton.characterLayer);
        for (int i = 0; i < colliders.Length; i++)
        {
            var target = colliders[i].GetComponent<CharacterEntity>();
            // If not character or character is attacker, skip it.
            if (target == null || target == otherCharacter || target.netId.Value == attackerNetId.Value || target.Hp <= 0)
                continue;

            hitSomeAliveCharacter = true;
            ApplyDamage(target);
        }
        // If hit character (So it will not wall) but not hit alive character, don't destroy, let's find another target.
        if (otherCharacter != null && !hitSomeAliveCharacter)
            return;

        if (!isDead && hitSomeAliveCharacter)
        {
            // Play hit effect
            if (hitFx != null && hitFx.Length > 0 && AudioManager.Singleton != null)
                AudioSource.PlayClipAtPoint(hitFx[Random.Range(0, hitFx.Length - 1)], CacheTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);
        }

        Destroy(gameObject);
        isDead = true;
    }

    private void ApplyDamage(CharacterEntity target)
    {
        // Damage receiving calculation on server only
        if (NetworkServer.active)
        {
            var gameplayManager = GameplayManager.Singleton;
            float damage = Attacker.TotalAttack;
            damage += (Random.Range(gameplayManager.minAttackVaryRate, gameplayManager.maxAttackVaryRate) * damage);
            target.ReceiveDamage(Attacker, Mathf.CeilToInt(damage), hitEffectType, relateDataId);
        }
    }

    public float GetAttackRange()
    {
        // s = v * t
        if (!CacheCollider)
            CacheCollider = GetComponent<Collider>();
        return (speed * lifeTime * GameplayManager.REAL_MOVE_SPEED_RATE) + Mathf.Min(CacheCollider.bounds.extents.x, CacheCollider.bounds.extents.z);
    }

    public Vector3 GetForwardVelocity()
    {
        return CacheTransform.forward * speed * GameplayManager.REAL_MOVE_SPEED_RATE;
    }

    public static DamageEntity InstantiateNewEntity(OpMsgCharacterAttack msg)
    {
        WeaponData weaponData = null;
        if (GameInstance.Weapons.TryGetValue(msg.weaponId, out weaponData))
        {
            return InstantiateNewEntity(weaponData.damagePrefab, msg.isLeftHandWeapon, msg.position, msg.direction, msg.attackerNetId, msg.addRotationX, msg.addRotationY);
        }
        return null;
    }

    public static DamageEntity InstantiateNewEntity(OpMsgCharacterUseSkill msg)
    {
        SkillData skillData = null;
        if (GameInstance.Skills.TryGetValue(msg.skillId, out skillData))
        {
            return InstantiateNewEntity(skillData.damagePrefab, false, msg.position, msg.direction, msg.attackerNetId, msg.addRotationX, msg.addRotationY);
        }
        return null;
    }

    public static DamageEntity InstantiateNewEntity(
        DamageEntity prefab,
        bool isLeftHandWeapon,
        Vector3 position,
        Vector3 direction,
        NetworkInstanceId attackerNetId,
        float addRotationX,
        float addRotationY)
    {
        if (prefab == null)
            return null;

        CharacterEntity attacker = null;
        var go = NetworkServer.active ? NetworkServer.FindLocalObject(attackerNetId) : ClientScene.FindLocalObject(attackerNetId);
        if (go != null)
            attacker = go.GetComponent<CharacterEntity>();

        if (attacker != null)
        {
            Transform launchTransform;
            attacker.GetDamageLaunchTransform(isLeftHandWeapon, out launchTransform);
            position = launchTransform.position + attacker.CacheTransform.forward * prefab.spawnForwardOffset;
        }
        var rotation = Quaternion.LookRotation(direction, Vector3.up);
        rotation = Quaternion.Euler(rotation.eulerAngles + new Vector3(addRotationX, addRotationY));
        var result = Instantiate(prefab, position, rotation);
        result.InitAttackData(isLeftHandWeapon, attackerNetId, addRotationX, addRotationY);
        return result;
    }
}
