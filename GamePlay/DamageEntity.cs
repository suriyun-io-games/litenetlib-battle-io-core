using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;

[RequireComponent(typeof(Rigidbody))]
public class DamageEntity : MonoBehaviour
{
    public EffectEntity spawnEffectPrefab;
    public EffectEntity explodeEffectPrefab;
    public EffectEntity hitEffectPrefab;
    [Tooltip("This status will be applied to character whom hitted by this damage entity")]
    public StatusEffectEntity statusEffectPrefab;
    public AudioClip[] hitFx;
    public float radius;
    public float explosionForceRadius;
    public float explosionForce;
    public float lifeTime;
    public float spawnForwardOffset;
    public float speed;
    public bool relateToAttacker;
    private bool isDead;
    private bool isLeftHandWeapon;
    private uint attackerNetId;
    private float addRotationX;
    private float addRotationY;
    private float? colliderExtents;
    public int weaponDamage { get; set; }
    public byte hitEffectType { get; set; }
    public int relateDataId { get; set; }
    public byte actionId { get; set; }

    private CharacterEntity attacker;
    public CharacterEntity Attacker
    {
        get
        {
            if (attacker == null)
            {
                LiteNetLibIdentity go;
                if (GameNetworkManager.Singleton.Assets.TryGetSpawnedObject(attackerNetId, out go))
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
        gameObject.layer = GenericUtils.IgnoreRaycastLayer;
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
    public void InitAttackData(bool isLeftHandWeapon, uint attackerNetId, float addRotationX, float addRotationY)
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
        {
            Explode(null);
            EffectEntity.PlayEffect(explodeEffectPrefab, CacheTransform);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == GenericUtils.IgnoreRaycastLayer)
            return;

        var otherCharacter = other.GetComponent<CharacterEntity>();
        // Damage will not hit attacker, so avoid it
        if (otherCharacter != null && otherCharacter.ObjectId == attackerNetId)
            return;

        var hitSomeAliveCharacter = false;
        if (otherCharacter != null && otherCharacter.Hp > 0)
        {
            hitSomeAliveCharacter = true;
            ApplyDamage(otherCharacter);
        }

        if (Explode(otherCharacter))
        {
            hitSomeAliveCharacter = true;
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

    private bool Explode(CharacterEntity otherCharacter)
    {
        var hitSomeAliveCharacter = false;
        Collider[] colliders = Physics.OverlapSphere(CacheTransform.position, radius, 1 << GameInstance.Singleton.characterLayer);
        CharacterEntity hitCharacter;
        for (int i = 0; i < colliders.Length; i++)
        {
            hitCharacter = colliders[i].GetComponent<CharacterEntity>();
            // If not character or character is attacker, skip it.
            if (hitCharacter == null || hitCharacter == otherCharacter || hitCharacter.ObjectId == attackerNetId || hitCharacter.Hp <= 0)
                continue;
            ApplyDamage(hitCharacter);
            hitSomeAliveCharacter = true;
        }
        return hitSomeAliveCharacter;
    }

    private void ApplyDamage(CharacterEntity target)
    {
        // Damage receiving calculation on server only
        if (GameNetworkManager.Singleton.IsServer)
        {
            target.ReceiveDamage(Attacker, weaponDamage, hitEffectType, relateDataId, actionId);
            if (statusEffectPrefab && GameplayManager.Singleton.CanApplyStatusEffect(target, Attacker))
                target.RpcApplyStatusEffect(statusEffectPrefab.GetHashId());
        }
        target.CacheRigidbody.AddExplosionForce(explosionForce, CacheTransform.position, explosionForceRadius);
    }

    private float GetColliderExtents()
    {
        if (colliderExtents.HasValue)
            return colliderExtents.Value;
        var tempObject = Instantiate(gameObject);
        var tempCollider = tempObject.GetComponent<Collider>();
        colliderExtents = Mathf.Min(tempCollider.bounds.extents.x, tempCollider.bounds.extents.z);
        Destroy(tempObject);
        return colliderExtents.Value;
    }

    public float GetAttackRange()
    {
        // s = v * t
        return (speed * lifeTime * GameplayManager.REAL_MOVE_SPEED_RATE) + GetColliderExtents();
    }

    public Vector3 GetForwardVelocity()
    {
        return CacheTransform.forward * speed * GameplayManager.REAL_MOVE_SPEED_RATE;
    }

    public static DamageEntity InstantiateNewEntity(OpMsgCharacterAttack msg)
    {
        WeaponData weaponData;
        if (GameInstance.Weapons.TryGetValue(msg.weaponId, out weaponData))
        {

            var damagePrefab = weaponData.damagePrefab;
            if (weaponData.AttackAnimations.ContainsKey(msg.actionId) &&
                weaponData.AttackAnimations[msg.actionId].damagePrefab != null)
                damagePrefab = weaponData.AttackAnimations[msg.actionId].damagePrefab;
            var damageEntity = InstantiateNewEntity(damagePrefab, weaponData.AttackAnimations[msg.actionId].isAnimationForLeftHandWeapon, msg.targetPosition, msg.attackerNetId, msg.addRotationX, msg.addRotationY);
            if (damageEntity)
                return damageEntity;
            else
                Debug.LogWarning("Can't find weapon damage entity prefab: " + msg.weaponId);
        }
        else
        {
            Debug.LogWarning("Can't find weapon data: " + msg.weaponId);
        }
        return null;
    }

    public static DamageEntity InstantiateNewEntity(OpMsgCharacterUseSkill msg)
    {
        SkillData skillData;
        if (GameInstance.Skills.TryGetValue(msg.skillId, out skillData))
        {
            var damagePrefab = skillData.damagePrefab;
            if (damagePrefab)
                return InstantiateNewEntity(damagePrefab, false, msg.targetPosition, msg.attackerNetId, msg.addRotationX, msg.addRotationY);
            else
                Debug.LogWarning("Can't find skill damage entity prefab: " + msg.skillId);
        }
        else
        {
            Debug.LogWarning("Can't find skill data: " + msg.skillId);
        }
        return null;
    }

    public static DamageEntity InstantiateNewEntity(
        DamageEntity prefab,
        bool isLeftHandWeapon,
        Vector3 targetPosition,
        uint attackerNetId,
        float addRotationX,
        float addRotationY)
    {
        if (prefab == null)
            return null;

        CharacterEntity attacker = null;
        LiteNetLibIdentity go;
        if (GameNetworkManager.Singleton.Assets.TryGetSpawnedObject(attackerNetId, out go))
            attacker = go.GetComponent<CharacterEntity>();

        if (attacker != null)
        {
            Transform launchTransform;
            attacker.GetDamageLaunchTransform(isLeftHandWeapon, out launchTransform);
            Vector3 position = launchTransform.position + attacker.CacheTransform.forward * prefab.spawnForwardOffset;
            Vector3 dir = targetPosition - position;
            Quaternion rotation = Quaternion.LookRotation(dir, Vector3.up);
            rotation = Quaternion.Euler(rotation.eulerAngles + new Vector3(addRotationX, addRotationY));
            DamageEntity result = Instantiate(prefab, position, rotation);
            result.InitAttackData(isLeftHandWeapon, attackerNetId, addRotationX, addRotationY);
            return result;
        }
        return null;
    }
}
