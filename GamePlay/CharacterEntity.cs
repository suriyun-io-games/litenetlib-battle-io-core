using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class CharacterEntity : NetworkBehaviour, IComparable<CharacterEntity>
{
    public const byte RPC_EFFECT_DAMAGE_SPAWN = 0;
    public const byte RPC_EFFECT_DAMAGE_HIT = 1;
    public const byte RPC_EFFECT_TRAP_HIT = 2;
    public static CharacterEntity Local { get; private set; }
    public Transform damageLaunchTransform;
    public Transform effectTransform;
    public Transform characterModelTransform;
    public GameObject[] localPlayerObjects;
    [Header("UI")]
    public Transform hpBarContainer;
    public Image hpFillImage;
    public Text hpText;
    public Text nameText;
    public Text levelText;
    [Header("Effect")]
    public GameObject invincibleEffect;
    [Header("Online data")]
    [SyncVar]
    public string playerName;

    [SyncVar]
    public int score;
    public int Score
    {
        get { return score; }
        set
        {
            if (!isServer)
                return;

            score = value;
            GameplayManager.Singleton.UpdateRank(netId);
        }
    }

    [SyncVar]
    public int hp;
    public int Hp
    {
        get { return hp; }
        set
        {
            if (!isServer)
                return;

            if (value <= 0)
            {
                value = 0;
                if (!isDead)
                {
                    if (connectionToClient != null)
                        TargetDead(connectionToClient);
                    deathTime = Time.unscaledTime;
                    isDead = true;
                }
            }
            if (value > TotalHp)
                value = TotalHp;
            hp = value;
        }
    }

    [SyncVar]
    public int exp;
    public int Exp
    {
        get { return exp; }
        set
        {
            if (!isServer)
                return;

            var gameplayManager = GameplayManager.Singleton;
            while (true)
            {
                if (level == gameplayManager.maxLevel)
                    break;

                var currentExp = gameplayManager.GetExp(level);
                if (value < currentExp)
                    break;
                var remainExp = value - currentExp;
                value = remainExp;
                ++level;
                statPoint += gameplayManager.addingStatPoint;
            }
            exp = value;
        }
    }

    [SyncVar]
    public int level = 1;

    [SyncVar]
    public int statPoint;

    [SyncVar]
    public int killCount;

    [SyncVar]
    public int watchAdsCount;

    [SyncVar(hook = "OnCharacterChanged")]
    public string selectCharacter = "";

    [SyncVar(hook = "OnHeadChanged")]
    public string selectHead = "";

    [SyncVar(hook = "OnWeaponChanged")]
    public string selectWeapon = "";

    [SyncVar]
    public bool isInvincible;

    [SyncVar, Tooltip("If this value >= 0 it's means character is attacking, so set it to -1 to stop attacks")]
    public int attackingActionId;

    [SyncVar]
    public CharacterStats addStats;

    protected Camera targetCamera;
    protected CharacterModel characterModel;
    protected CharacterData characterData;
    protected HeadData headData;
    protected WeaponData weaponData;

    public bool isReady { get; private set; }
    public bool isDead { get; private set; }
    public bool isPlayingAttackAnim { get; private set; }
    public float deathTime { get; private set; }
    public float invincibleTime { get; private set; }

    private Transform tempTransform;
    public Transform TempTransform
    {
        get
        {
            if (tempTransform == null)
                tempTransform = GetComponent<Transform>();
            return tempTransform;
        }
    }
    private Rigidbody tempRigidbody;
    public Rigidbody TempRigidbody
    {
        get
        {
            if (tempRigidbody == null)
                tempRigidbody = GetComponent<Rigidbody>();
            return tempRigidbody;
        }
    }

    public int TotalHp
    {
        get
        {
            var total = GameplayManager.Singleton.minHp + addStats.addHp;
            if (headData != null)
                total += headData.stats.addHp;
            if (characterData != null)
                total += characterData.stats.addHp;
            if (weaponData != null)
                total += weaponData.stats.addHp;
            return total;
        }
    }

    public int TotalAttack
    {
        get
        {
            var total = GameplayManager.Singleton.minAttack + addStats.addAttack;
            if (headData != null)
                total += headData.stats.addAttack;
            if (characterData != null)
                total += characterData.stats.addAttack;
            if (weaponData != null)
                total += weaponData.stats.addAttack;
            return total;
        }
    }

    public int TotalDefend
    {
        get
        {
            var total = GameplayManager.Singleton.minDefend + addStats.addDefend;
            if (headData != null)
                total += headData.stats.addDefend;
            if (characterData != null)
                total += characterData.stats.addDefend;
            if (weaponData != null)
                total += weaponData.stats.addDefend;
            return total;
        }
    }

    public int TotalMoveSpeed
    {
        get
        {
            var total = GameplayManager.Singleton.minMoveSpeed + addStats.addMoveSpeed;
            if (headData != null)
                total += headData.stats.addMoveSpeed;
            if (characterData != null)
                total += characterData.stats.addMoveSpeed;
            if (weaponData != null)
                total += weaponData.stats.addMoveSpeed;
            return total;
        }
    }

    public float TotalExpRate
    {
        get
        {
            var total = 1 + addStats.addExpRate;
            if (headData != null)
                total += headData.stats.addExpRate;
            if (characterData != null)
                total += characterData.stats.addExpRate;
            if (weaponData != null)
                total += weaponData.stats.addExpRate;
            return total;
        }
    }

    public float TotalScoreRate
    {
        get
        {
            var total = 1 + addStats.addScoreRate;
            if (headData != null)
                total += headData.stats.addScoreRate;
            if (characterData != null)
                total += characterData.stats.addScoreRate;
            if (weaponData != null)
                total += weaponData.stats.addScoreRate;
            return total;
        }
    }

    public float TotalHpRecoveryRate
    {
        get
        {
            var total = 1 + addStats.addHpRecoveryRate;
            if (headData != null)
                total += headData.stats.addHpRecoveryRate;
            if (characterData != null)
                total += characterData.stats.addHpRecoveryRate;
            if (weaponData != null)
                total += weaponData.stats.addHpRecoveryRate;
            return total;
        }
    }

    public float TotalDamageRateLeechHp
    {
        get
        {
            var total = addStats.addDamageRateLeechHp;
            if (headData != null)
                total += headData.stats.addDamageRateLeechHp;
            if (characterData != null)
                total += characterData.stats.addDamageRateLeechHp;
            if (weaponData != null)
                total += weaponData.stats.addDamageRateLeechHp;
            return total;
        }
    }

    public int TotalSpreadDamages
    {
        get
        {
            var total = 1 + addStats.addSpreadDamages;
            if (headData != null)
                total += headData.stats.addSpreadDamages;
            if (characterData != null)
                total += characterData.stats.addSpreadDamages;
            if (weaponData != null)
                total += weaponData.stats.addSpreadDamages;
            var maxValue = GameplayManager.Singleton.maxSpreadDamages;
            if (total > maxValue)
                return maxValue;
            return total;
        }
    }

    private void Awake()
    {
        gameObject.layer = GameInstance.Singleton.characterLayer;
        if (damageLaunchTransform == null)
            damageLaunchTransform = TempTransform;
        if (effectTransform == null)
            effectTransform = TempTransform;
        if (characterModelTransform == null)
            characterModelTransform = TempTransform;
        foreach (var localPlayerObject in localPlayerObjects)
        {
            localPlayerObject.SetActive(false);
        }
    }

    public override void OnStartClient()
    {
        if (!isServer)
        {
            OnHeadChanged(selectHead);
            OnCharacterChanged(selectCharacter);
            OnWeaponChanged(selectWeapon);
        }
    }

    public override void OnStartServer()
    {
        OnHeadChanged(selectHead);
        OnCharacterChanged(selectCharacter);
        OnWeaponChanged(selectWeapon);
        attackingActionId = -1;
    }

    public override void OnStartLocalPlayer()
    {
        if (Local != null)
            return;

        Local = this;
        var followCam = FindObjectOfType<FollowCamera>();
        followCam.target = TempTransform;
        targetCamera = followCam.GetComponent<Camera>();
        GameplayManager.Singleton.uiGameplay.FadeOut();

        foreach (var localPlayerObject in localPlayerObjects)
        {
            localPlayerObject.SetActive(true);
        }

        CmdReady();
    }

    private void Update()
    {
        if (isServer && isInvincible && Time.unscaledTime - invincibleTime >= GameplayManager.Singleton.invincibleDuration)
            isInvincible = false;
        if (invincibleEffect != null)
            invincibleEffect.SetActive(isInvincible);
        if (nameText != null)
            nameText.text = playerName;
        if (hpBarContainer != null)
            hpBarContainer.gameObject.SetActive(hp > 0);
        if (hpFillImage != null)
            hpFillImage.fillAmount = (float)hp / (float)TotalHp;
        if (hpText != null)
            hpText.text = hp + "/" + TotalHp;
        if (levelText != null)
            levelText.text = level.ToString("N0");
        if (isServer && Hp <= 0)
            attackingActionId = -1;
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        UpdateMovements();
    }

    protected virtual void UpdateAnimation()
    {
        if (characterModel == null)
            return;

        var animator = characterModel.TempAnimator;
        if (animator == null)
            return;

        if (Hp <= 0)
        {
            animator.SetBool("IsDead", true);
            animator.SetFloat("JumpSpeed", 0);
            animator.SetFloat("MoveSpeed", 0);
            animator.SetBool("IsGround", true);
        }
        else
        {
            var velocity = TempRigidbody.velocity;
            var xzMagnitude = new Vector3(velocity.x, 0, velocity.z).magnitude;
            var ySpeed = velocity.y;
            animator.SetBool("IsDead", false);
            animator.SetFloat("JumpSpeed", ySpeed);
            animator.SetFloat("MoveSpeed", xzMagnitude);
            animator.SetBool("IsGround", Mathf.Abs(ySpeed) < 0.5f);
        }

        if (attackingActionId >= 0 && !isPlayingAttackAnim)
            StartCoroutine(AttackRoutine(attackingActionId));
    }

    protected virtual void UpdateMovements()
    {
        if (!isLocalPlayer)
            return;

        if (Hp <= 0)
        {
            TempRigidbody.velocity = new Vector3(0, TempRigidbody.velocity.y, 0);
            return;
        }

        var direction = new Vector3(InputManager.GetAxis("Horizontal", false), 0, InputManager.GetAxis("Vertical", false));
        if (direction.magnitude != 0)
        {
            if (direction.magnitude > 1)
                direction = direction.normalized;
            Vector3 movementDir = direction * TotalMoveSpeed * GameplayManager.REAL_MOVE_SPEED_RATE;
            TempRigidbody.velocity = new Vector3(movementDir.x, TempRigidbody.velocity.y, movementDir.z);
        }
        else
            TempRigidbody.velocity = new Vector3(0, TempRigidbody.velocity.y, 0);

        if (Application.isMobilePlatform)
        {
            direction = new Vector2(InputManager.GetAxis("Mouse X", false), InputManager.GetAxis("Mouse Y", false));
            Rotate(direction);
            if (direction.magnitude != 0)
                Attack();
            else
                StopAttack();
        }
        else
        {
            direction = (Input.mousePosition - targetCamera.WorldToScreenPoint(transform.position)).normalized;
            Rotate(direction);
            if (Input.GetMouseButton(0))
                Attack();
            else
                StopAttack();
        }
    }

    protected void Rotate(Vector2 direction)
    {
        if (direction.magnitude != 0)
        {
            int newRotation = (int)(Quaternion.LookRotation(new Vector3(direction.x, 0, direction.y)).eulerAngles.y + targetCamera.transform.eulerAngles.y);
            Quaternion targetRotation = Quaternion.Euler(0, newRotation, 0);
            TempTransform.rotation = Quaternion.Lerp(TempTransform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }

    protected void Attack()
    {
        if (attackingActionId < 0 && isLocalPlayer)
            CmdAttack();
    }

    protected void StopAttack()
    {
        if (attackingActionId >= 0 && isLocalPlayer)
            CmdStopAttack();
    }

    IEnumerator AttackRoutine(int actionId)
    {
        if (!isPlayingAttackAnim && Hp > 0)
        {
            isPlayingAttackAnim = true;
            if (weaponData != null && characterModel != null)
            {
                var animator = characterModel.TempAnimator;
                if (animator != null && weaponData.AttackAnimations.ContainsKey(actionId))
                {
                    // Play attack animation
                    var attackAnimation = weaponData.AttackAnimations[actionId];
                    animator.SetBool("DoAction", true);
                    animator.SetInteger("ActionID", attackAnimation.actionId);
                    var speed = attackAnimation.speed;
                    var animationDuration = attackAnimation.animationDuration;
                    var launchDuration = attackAnimation.launchDuration;
                    if (launchDuration > animationDuration)
                        launchDuration = animationDuration;
                    yield return new WaitForSeconds(launchDuration / speed);
                    // Launch damage entity on server only
                    if (isServer)
                        weaponData.Launch(this, TotalSpreadDamages);
                    yield return new WaitForSeconds((animationDuration - launchDuration) / speed);
                    // Attack animation ended
                    animator.SetBool("DoAction", false);
                }
            }
            // If player still attacking, random new attacking action id
            if (isServer && attackingActionId >= 0 && weaponData != null)
                attackingActionId = weaponData.GetRandomAttackAnimation().actionId;
            yield return new WaitForEndOfFrame();
            isPlayingAttackAnim = false;
        }
    }

    [Server]
    public void ReceiveDamage(CharacterEntity attacker, int damage)
    {
        if (Hp <= 0 || isInvincible)
            return;

        RpcEffect(attacker.netId, RPC_EFFECT_DAMAGE_HIT);
        int reduceHp = damage - TotalDefend;
        if (reduceHp < 0)
            reduceHp = 0;

        Hp -= reduceHp;
        if (attacker != null)
        {
            if (attacker.Hp > 0)
            {
                var leechHpAmount = Mathf.CeilToInt(attacker.TotalDamageRateLeechHp * reduceHp);
                attacker.Hp += leechHpAmount;
            }
            if (Hp == 0)
                attacker.KilledTarget(this);
        }
    }

    [Server]
    public void KilledTarget(CharacterEntity target)
    {
        var gameplayManager = GameplayManager.Singleton;
        var targetLevel = target.level;
        Exp += Mathf.CeilToInt(gameplayManager.GetRewardExp(targetLevel) * TotalExpRate);
        Score += Mathf.CeilToInt(gameplayManager.GetKillScore(targetLevel) * TotalScoreRate);
        ++killCount;
    }

    [Server]
    public void Heal(int amount)
    {
        if (Hp <= 0)
            return;

        Hp += amount;
    }

    public float GetAttackRange()
    {
        if (weaponData == null || weaponData.damagePrefab == null)
            return 0;
        return weaponData.damagePrefab.GetAttackRange();
    }

    protected virtual void OnCharacterChanged(string value)
    {
        selectCharacter = value;
        if (characterModel != null)
            Destroy(characterModel.gameObject);
        characterData = GameInstance.GetCharacter(value);
        if (characterData == null || characterData.modelObject == null)
            return;
        characterModel = Instantiate(characterData.modelObject, characterModelTransform);
        characterModel.transform.localPosition = Vector3.zero;
        characterModel.transform.localEulerAngles = Vector3.zero;
        characterModel.transform.localScale = Vector3.one;
        if (headData != null)
            characterModel.SetHeadModel(headData.modelObject);
        if (weaponData != null)
            characterModel.SetWeaponModel(weaponData.rightHandObject, weaponData.leftHandObject, weaponData.shieldObject);
        characterModel.gameObject.SetActive(true);
    }

    protected virtual void OnHeadChanged(string value)
    {
        selectHead = value;
        headData = GameInstance.GetHead(value);
        if (characterModel != null && headData != null)
            characterModel.SetHeadModel(headData.modelObject);
    }

    protected virtual void OnWeaponChanged(string value)
    {
        selectWeapon = value;
        weaponData = GameInstance.GetWeapon(value);
        if (characterModel != null && weaponData != null)
            characterModel.SetWeaponModel(weaponData.rightHandObject, weaponData.leftHandObject, weaponData.shieldObject);
    }

    public void ChangeWeapon(WeaponData weaponData)
    {
        if (weaponData == null)
            return;
        selectWeapon = weaponData.GetId();
    }
    
    protected virtual void OnSpawn() { }

    [Server]
    public void ServerInvincible()
    {
        invincibleTime = Time.unscaledTime;
        isInvincible = true;
    }

    [Server]
    public void ServerSpawn(bool isWatchedAds)
    {
        var gameplayManager = GameplayManager.Singleton;
        if (!isWatchedAds || watchAdsCount >= gameplayManager.watchAdsRespawnAvailable)
            Reset();
        else
        {
            ++watchAdsCount;
            isDead = false;
            Hp = TotalHp;
        }
        ServerInvincible();
        OnSpawn();
        var position = gameplayManager.GetCharacterSpawnPosition();
        TempTransform.position = position;
        if (connectionToClient != null)
            TargetSpawn(connectionToClient, position);
    }

    [Server]
    public void ServerRespawn(bool isWatchedAds)
    {
        var gameplayManager = GameplayManager.Singleton;
        if (Time.unscaledTime - deathTime >= gameplayManager.respawnDuration)
            ServerSpawn(isWatchedAds);
    }

    [Server]
    public void Reset()
    {
        Score = 0;
        Exp = 0;
        level = 1;
        statPoint = 0;
        killCount = 0;
        watchAdsCount = 0;
        addStats = new CharacterStats();
        isDead = false;
        isPlayingAttackAnim = false;
        Hp = TotalHp;
    }

    [Command]
    public void CmdReady()
    {
        if (!isReady)
        {
            ServerSpawn(false);
            GameplayManager.Singleton.UpdateRank(netId);
            isReady = true;
        }
    }

    [Command]
    public void CmdRespawn(bool isWatchedAds)
    {
        ServerRespawn(isWatchedAds);
    }

    [Command]
    public void CmdAttack()
    {
        if (weaponData != null)
            attackingActionId = weaponData.GetRandomAttackAnimation().actionId;
        else
            attackingActionId = -1;
    }

    [Command]
    public void CmdStopAttack()
    {
        attackingActionId = -1;
    }

    [Command]
    public void CmdAddAttribute(string name)
    {
        if (statPoint > 0)
        {
            var gameplay = GameplayManager.Singleton;
            CharacterAttributes attribute;
            if (gameplay.attributes.TryGetValue(name, out attribute))
            {
                addStats += attribute.stats;
                var changingWeapon = attribute.changingWeapon;
                if (changingWeapon != null)
                    ChangeWeapon(changingWeapon);
                --statPoint;
            }
        }
    }

    [ClientRpc]
    public void RpcEffect(NetworkInstanceId triggerId, byte effectType)
    {
        GameObject triggerObject = null;
        if (isServer)
            triggerObject = NetworkServer.FindLocalObject(triggerId);
        else
            triggerObject = ClientScene.FindLocalObject(triggerId);

        if (triggerObject != null)
        {
            if (effectType == RPC_EFFECT_DAMAGE_SPAWN || effectType == RPC_EFFECT_DAMAGE_HIT)
            {
                var attacker = triggerObject.GetComponent<CharacterEntity>();
                if (attacker != null &&
                    attacker.weaponData != null &&
                    attacker.weaponData.damagePrefab != null)
                {
                    switch (effectType)
                    {
                        case RPC_EFFECT_DAMAGE_SPAWN:
                            EffectEntity.PlayEffect(attacker.weaponData.damagePrefab.spawnEffectPrefab, effectTransform);
                            break;
                        case RPC_EFFECT_DAMAGE_HIT:
                            EffectEntity.PlayEffect(attacker.weaponData.damagePrefab.hitEffectPrefab, effectTransform);
                            break;
                    }
                }
            }
            else if (effectType == RPC_EFFECT_TRAP_HIT)
            {
                var trap = triggerObject.GetComponent<TrapEntity>();
                if (trap != null)
                    EffectEntity.PlayEffect(trap.hitEffectPrefab, effectTransform);
            }
        }
    }

    [TargetRpc]
    private void TargetDead(NetworkConnection conn)
    {
        deathTime = Time.unscaledTime;
    }

    [TargetRpc]
    private void TargetSpawn(NetworkConnection conn, Vector3 position)
    {
        transform.position = position;
    }

    public int CompareTo(CharacterEntity other)
    {
        return ((-1 * Score.CompareTo(other.Score)) * 10) + netId.Value.CompareTo(other.netId.Value);
    }
}
