using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using static LiteNetLibManager.LiteNetLibSyncList;

[RequireComponent(typeof(LiteNetLibTransform))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CharacterMovement))]
public class CharacterEntity : BaseNetworkGameCharacter
{
    public const float DISCONNECT_WHEN_NOT_RESPAWN_DURATION = 60;
    public const byte RPC_EFFECT_DAMAGE_SPAWN = 0;
    public const byte RPC_EFFECT_DAMAGE_HIT = 1;
    public const byte RPC_EFFECT_TRAP_HIT = 2;
    public const byte RPC_EFFECT_SKILL_SPAWN = 3;
    public const byte RPC_EFFECT_SKILL_HIT = 4;

    public enum ViewMode
    {
        TopDown,
        ThirdPerson,
    }

    [System.Serializable]
    public class ViewModeSettings
    {
        public Vector3 targetOffsets = Vector3.zero;
        public float zoomDistance = 3f;
        public float minZoomDistance = 3f;
        public float maxZoomDistance = 3f;
        public float xRotation = 45f;
        public float minXRotation = 45f;
        public float maxXRotation = 45f;
        public float yRotation = 0f;
        public float fov = 60f;
        public float nearClipPlane = 0.3f;
        public float farClipPlane = 1000f;
    }

    public ViewMode viewMode;
    public ViewModeSettings topDownViewModeSettings;
    public ViewModeSettings thirdPersionViewModeSettings;
    public bool doNotLockCursor;
    public Transform damageLaunchTransform;
    public Transform effectTransform;
    public Transform characterModelTransform;
    public GameObject[] localPlayerObjects;
    public float dashDuration = 1.5f;
    public float dashMoveSpeedMultiplier = 1.5f;
    public float blockMoveSpeedMultiplier = 0.75f;
    public float returnToMoveDirectionDelay = 1f;
    public float endActionDelay = 0.75f;
    [Header("UI")]
    public Transform hpBarContainer;
    public Image hpFillImage;
    public Text hpText;
    public Text nameText;
    public Text levelText;
    public GameObject attackSignalObject;
    [Header("Effect")]
    public GameObject invincibleEffect;
    [Header("Online data")]
    [SyncField]
    public int hp;
    public int Hp
    {
        get { return hp; }
        set
        {
            if (!IsServer)
                return;

            if (value <= 0)
            {
                value = 0;
                if (!isDead)
                {
                    TargetDead(ConnectionId);
                    deathTime = Time.unscaledTime;
                    ++dieCount;
                    isDead = true;
                }
            }
            if (value > TotalHp)
                value = TotalHp;
            hp = value;
        }
    }

    [SyncField]
    public int exp;
    public virtual int Exp
    {
        get { return exp; }
        set
        {
            if (!IsServer)
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

    [SyncField]
    public int level = 1;

    [SyncField]
    public int statPoint;

    [SyncField]
    public int watchAdsCount;

    [SyncField(hook = nameof(OnCharacterChanged))]
    public int selectCharacter = 0;

    [SyncField(hook = nameof(OnHeadChanged))]
    public int selectHead = 0;

    [SyncField(hook = nameof(OnWeaponChanged))]
    public int selectWeapon = 0;

    public SyncListInt selectCustomEquipments = new SyncListInt();

    [SyncField]
    public bool isInvincible;

    [SyncField]
    public bool isBlocking;

    [SyncField, Tooltip("If this value >= 0 it's means character is attacking, so set it to -1 to stop attacks")]
    public short attackingActionId = -1;

    [SyncField, Tooltip("If this value >= 0 it's means character is using skill, so set it to -1 to stop skills")]
    public sbyte usingSkillHotkeyId = -1;

    [SyncField(hook = nameof(OnAttributeAmountsChanged), alwaysSync = true)]
    public AttributeAmounts attributeAmounts = new AttributeAmounts(0);

    [SyncField]
    public string extra;

    [SyncField(syncMode = LiteNetLibSyncField.SyncMode.ClientMulticast)]
    public Vector3 aimPosition;

    [HideInInspector]
    public int rank = 0;

    public override bool IsDead
    {
        get { return hp <= 0; }
    }

    public override bool IsBot
    {
        get { return false; }
    }

    public System.Action onDead;
    protected ViewMode dirtyViewMode;
    protected Camera targetCamera;
    protected Vector3 cameraForward;
    protected Vector3 cameraRight;
    protected FollowCameraControls followCameraControls;
    protected CharacterModel characterModel;
    protected CharacterData characterData;
    protected HeadData headData;
    protected WeaponData weaponData;
    protected Dictionary<int, CustomEquipmentData> customEquipmentDict = new Dictionary<int, CustomEquipmentData>();
    protected Dictionary<int, StatusEffectEntity> appliedStatusEffects = new Dictionary<int, StatusEffectEntity>();
    protected bool isMobileInput;
    protected Vector3 inputMove;
    protected Vector3 inputDirection;
    protected bool inputAttack;
    protected bool inputJump;
    protected bool isDashing;
    protected Vector3 dashInputMove;
    protected float dashingTime;
    protected Dictionary<sbyte, SkillData> skills = new Dictionary<sbyte, SkillData>();
    protected float[] lastSkillUseTimes = new float[8];
    protected bool inputCancelUsingSkill;
    protected sbyte holdingUseSkillHotkeyId = -1;
    protected sbyte releasedUseSkillHotkeyId = -1;
    protected Vector3? previousPosition;
    protected Vector3 currentVelocity;
    protected float lastActionTime;
    protected Coroutine endActionDelayCoroutine;

    public bool isReady { get; private set; }
    public bool isDead { get; private set; }
    public bool isGrounded { get { return CacheCharacterMovement.IsGrounded; } }
    public bool isPlayingAttackAnim { get; private set; }
    public bool isPlayingUseSkillAnim { get; private set; }
    public float deathTime { get; private set; }
    public float invincibleTime { get; private set; }
    public int defaultSelectWeapon { get; private set; }
    public bool currentActionIsForLeftHand { get; protected set; }

    public Dictionary<sbyte, SkillData> Skills
    {
        get { return skills; }
    }

    private bool isHidding;
    public bool IsHidding
    {
        get { return isHidding; }
        set
        {
            isHidding = value;
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
                renderer.enabled = !isHidding;
            var canvases = GetComponentsInChildren<Canvas>();
            foreach (var canvas in canvases)
                canvas.enabled = !isHidding;
            var projectors = GetComponentsInChildren<Projector>();
            foreach (var projector in projectors)
                projector.enabled = !isHidding;
        }
    }

    public Transform CacheTransform { get; private set; }
    public Rigidbody CacheRigidbody { get; private set; }
    public CharacterMovement CacheCharacterMovement { get; private set; }
    public LiteNetLibTransform CacheNetTransform { get; private set; }

    protected bool refreshingSumAddStats = true;
    protected CharacterStats sumAddStats = new CharacterStats();
    public virtual CharacterStats SumAddStats
    {
        get
        {
            if (refreshingSumAddStats)
            {
                var addStats = new CharacterStats();
                if (headData != null)
                    addStats += headData.stats;
                if (characterData != null)
                    addStats += characterData.stats;
                if (weaponData != null)
                    addStats += weaponData.stats;
                if (customEquipmentDict != null)
                {
                    foreach (var value in customEquipmentDict.Values)
                    {
                        addStats += value.stats;
                    }
                }
                if (attributeAmounts.Dict != null)
                {
                    foreach (var kv in attributeAmounts.Dict)
                    {
                        CharacterAttributes attribute;
                        if (GameplayManager.Singleton.Attributes.TryGetValue(kv.Key, out attribute))
                            addStats += attribute.stats * kv.Value;
                    }
                }
                if (appliedStatusEffects != null)
                {
                    foreach (var value in appliedStatusEffects.Values)
                        addStats += value.addStats;
                }
                sumAddStats = addStats;
                refreshingSumAddStats = false;
            }
            return sumAddStats;
        }
    }

    public virtual int TotalHp
    {
        get
        {
            var total = GameplayManager.Singleton.baseHp + SumAddStats.addHp;
            return total;
        }
    }

    public virtual int TotalAttack
    {
        get
        {
            var total = GameplayManager.Singleton.baseAttack + SumAddStats.addAttack;
            return total;
        }
    }

    public virtual int TotalDefend
    {
        get
        {
            var total = GameplayManager.Singleton.baseDefend + SumAddStats.addDefend;
            return total;
        }
    }

    public virtual int TotalMoveSpeed
    {
        get
        {
            var total = GameplayManager.Singleton.baseMoveSpeed + SumAddStats.addMoveSpeed;
            return total;
        }
    }

    public virtual float TotalExpRate
    {
        get
        {
            var total = 1 + SumAddStats.addExpRate;
            return total;
        }
    }

    public virtual float TotalScoreRate
    {
        get
        {
            var total = 1 + SumAddStats.addScoreRate;
            return total;
        }
    }

    public virtual float TotalHpRecoveryRate
    {
        get
        {
            var total = 1 + SumAddStats.addHpRecoveryRate;
            return total;
        }
    }

    public virtual float TotalBlockReduceDamageRate
    {
        get
        {
            var total = GameplayManager.Singleton.baseBlockReduceDamageRate + SumAddStats.addBlockReduceDamageRate;

            var maxValue = GameplayManager.Singleton.maxBlockReduceDamageRate;
            if (total < maxValue)
                return total;
            else
                return maxValue;
        }
    }

    public virtual float TotalDamageRateLeechHp
    {
        get
        {
            var total = SumAddStats.addDamageRateLeechHp;
            return total;
        }
    }

    public virtual int TotalSpreadDamages
    {
        get
        {
            var total = 1 + SumAddStats.addSpreadDamages;

            var maxValue = GameplayManager.Singleton.maxSpreadDamages;
            if (total < maxValue)
                return total;
            else
                return maxValue;
        }
    }

    public virtual float TotalIncreaseDamageRate
    {
        get
        {
            var total = SumAddStats.increaseDamageRate;
            if (total < -1f)
                total = -0.9f;
            return total;
        }
    }

    public virtual float TotalReduceReceiveDamageRate
    {
        get
        {
            var total = SumAddStats.reduceReceiveDamageRate;
            return total;
        }
    }

    public virtual int RewardExp
    {
        get { return GameplayManager.Singleton.GetRewardExp(level); }
    }

    public virtual int KillScore
    {
        get { return GameplayManager.Singleton.GetKillScore(level); }
    }

    private void Awake()
    {
        selectCustomEquipments.onOperation = OnCustomEquipmentsChanged;
        gameObject.layer = GameInstance.Singleton.characterLayer;
        CacheTransform = transform;
        CacheRigidbody = gameObject.GetOrAddComponent<Rigidbody>();
        CacheRigidbody.useGravity = false;
        CacheCharacterMovement = gameObject.GetOrAddComponent<CharacterMovement>();
        CacheNetTransform = gameObject.GetOrAddComponent<LiteNetLibTransform>();
        CacheNetTransform.ownerClientCanSendTransform = true;
        if (damageLaunchTransform == null)
            damageLaunchTransform = CacheTransform;
        if (effectTransform == null)
            effectTransform = CacheTransform;
        if (characterModelTransform == null)
            characterModelTransform = CacheTransform;
        foreach (var localPlayerObject in localPlayerObjects)
        {
            localPlayerObject.SetActive(false);
        }
        deathTime = Time.unscaledTime;
    }

    public override void OnStartServer()
    {
        attackingActionId = -1;
        usingSkillHotkeyId = -1;
    }

    public override void OnStartOwnerClient()
    {
        base.OnStartOwnerClient();

        followCameraControls = FindObjectOfType<FollowCameraControls>();
        followCameraControls.target = CacheTransform;
        targetCamera = followCameraControls.CacheCamera;
        var uiGameplay = FindObjectOfType<UIGameplay>();
        if (uiGameplay != null)
            uiGameplay.FadeOut();

        foreach (var localPlayerObject in localPlayerObjects)
        {
            localPlayerObject.SetActive(true);
        }

        CmdReady();
    }

    protected override void Update()
    {
        base.Update();
        if (NetworkManager != null && NetworkManager.IsMatchEnded)
            return;

        var targetSimulateSpeed = GetMoveSpeed() * (isDashing ? dashMoveSpeedMultiplier : 1f);
        CacheNetTransform.interpolateMode = LiteNetLibTransform.InterpolateMode.FixedSpeed;
        CacheNetTransform.fixedInterpolateSpeed = targetSimulateSpeed;

        if (Hp <= 0)
        {
            if (!IsServer && IsOwnerClient && Time.unscaledTime - deathTime >= DISCONNECT_WHEN_NOT_RESPAWN_DURATION)
                GameNetworkManager.Singleton.StopHost();

            if (IsServer)
            {
                attackingActionId = -1;
                usingSkillHotkeyId = -1;
                isBlocking = false;
            }
        }

        if (IsServer && isInvincible && Time.unscaledTime - invincibleTime >= GameplayManager.Singleton.invincibleDuration)
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
        UpdateViewMode();
        UpdateAimPosition();
        UpdateAnimation();
        UpdateInput();
        // Update dash state
        if (isDashing && Time.unscaledTime - dashingTime > dashDuration)
            isDashing = false;
        // Update attack signal
        if (attackSignalObject != null)
            attackSignalObject.SetActive(isPlayingAttackAnim);
    }

    private void FixedUpdate()
    {
        if (!previousPosition.HasValue)
            previousPosition = CacheTransform.position;
        var currentMove = CacheTransform.position - previousPosition.Value;
        currentVelocity = currentMove / Time.deltaTime;
        previousPosition = CacheTransform.position;

        if (NetworkManager != null && NetworkManager.IsMatchEnded)
            return;

        UpdateMovements();
    }

    protected virtual void UpdateInput()
    {
        if (!IsOwnerClient)
            return;

        bool canControl = true;
        var fields = FindObjectsOfType<InputField>();
        foreach (var field in fields)
        {
            if (field.isFocused)
            {
                canControl = false;
                break;
            }
        }

        isMobileInput = Application.isMobilePlatform;
#if UNITY_EDITOR
        isMobileInput = GameInstance.Singleton.showJoystickInEditor;
#endif
        InputManager.useMobileInputOnNonMobile = isMobileInput;

        bool canAttack = isMobileInput || !EventSystem.current.IsPointerOverGameObject();
        // Reset input states
        inputMove = Vector3.zero;
        inputDirection = Vector3.zero;
        inputAttack = false;

        if (inputCancelUsingSkill = InputManager.GetButton("CancelUsingSkill"))
        {
            holdingUseSkillHotkeyId = -1;
            releasedUseSkillHotkeyId = -1;
        }

        if (canControl)
        {
            cameraForward = followCameraControls.CacheCameraTransform.forward;
            cameraForward.y = 0;
            cameraForward = cameraForward.normalized;
            cameraRight = followCameraControls.CacheCameraTransform.right;
            cameraRight.y = 0;
            cameraRight = cameraRight.normalized;
            inputMove = Vector3.zero;
            if (!IsDead)
            {
                inputMove += cameraForward * InputManager.GetAxis("Vertical", false);
                inputMove += cameraRight * InputManager.GetAxis("Horizontal", false);
            }

            // Bloacking
            isBlocking = !IsDead && !isDashing && attackingActionId < 0 && usingSkillHotkeyId < 0 && isGrounded && InputManager.GetButton("Block");

            // Jump
            if (!IsDead && !inputJump)
                inputJump = InputManager.GetButtonDown("Jump") && isGrounded && !isDashing;

            if (!isBlocking && !isDashing)
            {
                UpdateInputDirection_TopDown(canAttack);
                UpdateInputDirection_ThirdPerson(canAttack);
                if (!IsDead)
                    isDashing = InputManager.GetButtonDown("Dash") && isGrounded;
                if (isDashing)
                {
                    if (isMobileInput)
                        dashInputMove = inputMove.normalized;
                    else
                        dashInputMove = new Vector3(CacheTransform.forward.x, 0f, CacheTransform.forward.z).normalized;
                    inputAttack = false;
                    dashingTime = Time.unscaledTime;
                    CmdDash();
                }
            }
        }
    }

    protected virtual void UpdateInputDirection_TopDown(bool canAttack)
    {
        if (viewMode != ViewMode.TopDown)
            return;
        doNotLockCursor = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        followCameraControls.updateRotation = false;
        followCameraControls.updateZoom = true;
        if (isMobileInput)
        {
            inputDirection = Vector3.zero;
            inputDirection += InputManager.GetAxis("Mouse Y", false) * cameraForward;
            inputDirection += InputManager.GetAxis("Mouse X", false) * cameraRight;
            if (canAttack)
            {
                inputAttack = inputDirection.magnitude != 0;
                if (!inputAttack)
                {
                    // Find out that player pressed on skill hotkey or not
                    for (sbyte i = 0; i < 8; ++i)
                    {
                        inputDirection = Vector3.zero;
                        inputDirection += InputManager.GetAxis("Skill Y " + i, false) * cameraForward;
                        inputDirection += InputManager.GetAxis("Skill X " + i, false) * cameraRight;
                        if (inputDirection.sqrMagnitude != 0 && holdingUseSkillHotkeyId < 0)
                        {
                            // Start drag
                            holdingUseSkillHotkeyId = i;
                            releasedUseSkillHotkeyId = -1;
                            break;
                        }
                        if (inputDirection.sqrMagnitude != 0 && holdingUseSkillHotkeyId == i)
                        {
                            // Holding
                            break;
                        }
                        if (inputDirection.sqrMagnitude == 0 && holdingUseSkillHotkeyId == i)
                        {
                            // End drag
                            holdingUseSkillHotkeyId = -1;
                            releasedUseSkillHotkeyId = i;
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            inputDirection = (InputManager.MousePosition() - targetCamera.WorldToScreenPoint(CacheTransform.position)).normalized;
            inputDirection = new Vector3(inputDirection.x, 0, inputDirection.y);
            if (canAttack)
            {
                inputAttack = InputManager.GetButton("Fire1");
                if (!inputAttack)
                {
                    // Find out that player pressed on skill hotkey or not
                    for (sbyte i = 0; i < 8; ++i)
                    {
                        if (InputManager.GetButton("Skill " + i) && holdingUseSkillHotkeyId < 0)
                        {
                            // Break if use skill
                            holdingUseSkillHotkeyId = -1;
                            releasedUseSkillHotkeyId = i;
                            break;
                        }
                    }
                }
            }
        }
    }

    protected virtual void UpdateInputDirection_ThirdPerson(bool canAttack)
    {
        if (viewMode != ViewMode.ThirdPerson)
            return;
        if (isMobileInput || doNotLockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (isMobileInput)
        {
            followCameraControls.updateRotation = InputManager.GetButton("CameraRotate");
            followCameraControls.updateZoom = true;
            inputDirection = Vector3.zero;
            inputDirection += InputManager.GetAxis("Mouse Y", false) * cameraForward;
            inputDirection += InputManager.GetAxis("Mouse X", false) * cameraRight;
            if (canAttack)
            {
                inputAttack = InputManager.GetButton("Fire1");
                if (!inputAttack)
                {
                    // Find out that player pressed on skill hotkey or not
                    for (sbyte i = 0; i < 8; ++i)
                    {
                        inputDirection = Vector3.zero;
                        inputDirection += InputManager.GetAxis("Skill Y " + i, false) * cameraForward;
                        inputDirection += InputManager.GetAxis("Skill X " + i, false) * cameraRight;
                        if (inputDirection.sqrMagnitude != 0 && holdingUseSkillHotkeyId < 0)
                        {
                            // Start drag
                            holdingUseSkillHotkeyId = i;
                            releasedUseSkillHotkeyId = -1;
                            break;
                        }
                        if (inputDirection.sqrMagnitude != 0 && holdingUseSkillHotkeyId == i)
                        {
                            // Holding
                            break;
                        }
                        if (inputDirection.sqrMagnitude == 0 && holdingUseSkillHotkeyId == i)
                        {
                            // End drag
                            holdingUseSkillHotkeyId = -1;
                            releasedUseSkillHotkeyId = i;
                            break;
                        }
                    }
                }
            }
        }
        else
        {
            followCameraControls.updateRotation = true;
            followCameraControls.updateZoom = true;
            if (canAttack)
            {
                inputAttack = InputManager.GetButton("Fire1");
                if (!inputAttack)
                {
                    // Find out that player pressed on skill hotkey or not
                    for (sbyte i = 0; i < 8; ++i)
                    {
                        if (InputManager.GetButton("Skill " + i) && holdingUseSkillHotkeyId < 0)
                        {
                            // Break if use skill
                            holdingUseSkillHotkeyId = -1;
                            releasedUseSkillHotkeyId = i;
                            break;
                        }
                    }
                }
            }
        }
        if (inputAttack || releasedUseSkillHotkeyId >= 0)
            lastActionTime = Time.unscaledTime;
    }

    protected virtual void UpdateViewMode(bool force = false)
    {
        if (!IsOwnerClient)
            return;

        if (force || dirtyViewMode != viewMode)
        {
            dirtyViewMode = viewMode;
            ViewModeSettings settings = viewMode == ViewMode.ThirdPerson ? thirdPersionViewModeSettings : topDownViewModeSettings;
            followCameraControls.limitXRotation = true;
            followCameraControls.limitYRotation = false;
            followCameraControls.limitZoomDistance = true;
            followCameraControls.targetOffset = settings.targetOffsets;
            followCameraControls.zoomDistance = settings.zoomDistance;
            followCameraControls.minZoomDistance = settings.minZoomDistance;
            followCameraControls.maxZoomDistance = settings.maxZoomDistance;
            followCameraControls.xRotation = settings.xRotation;
            followCameraControls.minXRotation = settings.minXRotation;
            followCameraControls.maxXRotation = settings.maxXRotation;
            followCameraControls.yRotation = settings.yRotation;
            targetCamera.fieldOfView = settings.fov;
            targetCamera.nearClipPlane = settings.nearClipPlane;
            targetCamera.farClipPlane = settings.farClipPlane;
        }
    }

    protected virtual void UpdateAimPosition()
    {
        if (!(IsOwnerClient || (IsServer && ConnectionId <= 0)) || !weaponData)
            return;

        float attackDist = weaponData.damagePrefab.GetAttackRange();
        switch (viewMode)
        {
            case ViewMode.TopDown:
                // Update aim position
                currentActionIsForLeftHand = CurrentActionIsForLeftHand();
                Transform launchTransform;
                GetDamageLaunchTransform(currentActionIsForLeftHand, out launchTransform);
                aimPosition = launchTransform.position + (CacheTransform.forward * attackDist);
                break;
            case ViewMode.ThirdPerson:
                float distanceToCharacter = Vector3.Distance(CacheTransform.position, followCameraControls.CacheCameraTransform.position);
                float distanceToTarget = attackDist;
                Vector3 lookAtCharacterPosition = targetCamera.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, distanceToCharacter));
                Vector3 lookAtTargetPosition = targetCamera.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, distanceToTarget));
                aimPosition = lookAtTargetPosition;
                RaycastHit[] hits =Physics.RaycastAll(lookAtCharacterPosition, (lookAtTargetPosition- lookAtCharacterPosition).normalized, attackDist);
                for (int i = 0; i < hits.Length; ++i)
                {
                    if (hits[i].transform.root != transform.root)
                        aimPosition = hits[i].point;
                }
                break;
        }
    }

    protected virtual void UpdateAnimation()
    {
        if (characterModel == null)
            return;

        var animator = characterModel.CacheAnimator;
        if (animator == null)
            return;

        if (Hp <= 0)
        {
            animator.SetBool("IsDead", true);
            animator.SetFloat("JumpSpeed", 0);
            animator.SetFloat("MoveSpeed", 0);
            animator.SetBool("IsGround", true);
            animator.SetBool("IsDash", false);
            animator.SetBool("IsBlock", false);
        }
        else
        {
            var velocity = currentVelocity;
            var xzMagnitude = new Vector3(velocity.x, 0, velocity.z).magnitude;
            var ySpeed = velocity.y;
            animator.SetBool("IsDead", false);
            animator.SetFloat("JumpSpeed", ySpeed);
            animator.SetFloat("MoveSpeed", xzMagnitude);
            animator.SetBool("IsGround", Mathf.Abs(ySpeed) < 0.5f);
            animator.SetBool("IsDash", isDashing);
            animator.SetBool("IsBlock", isBlocking);
        }

        if (weaponData != null)
            animator.SetInteger("WeaponAnimId", weaponData.weaponAnimId);

        animator.SetBool("IsIdle", !animator.GetBool("IsDead") && !animator.GetBool("DoAction") && animator.GetBool("IsGround"));

        if (attackingActionId >= 0 && usingSkillHotkeyId < 0 && !isPlayingAttackAnim)
            StartCoroutine(AttackRoutine());

        if (usingSkillHotkeyId >= 0 && !isPlayingUseSkillAnim)
            StartCoroutine(UseSkillRoutine());
    }

    protected virtual float GetMoveSpeed()
    {
        return TotalMoveSpeed * GameplayManager.REAL_MOVE_SPEED_RATE;
    }

    protected virtual bool CurrentActionIsForLeftHand()
    {
        if (usingSkillHotkeyId >= 0)
        {
            SkillData skillData;
            if (skills.TryGetValue(usingSkillHotkeyId, out skillData))
                return skillData.attackAnimation.isAnimationForLeftHandWeapon;
        }
        else if (attackingActionId >= 0)
        {
            AttackAnimation attackAnimation;
            if (weaponData.AttackAnimations.TryGetValue(attackingActionId, out attackAnimation))
                return attackAnimation.isAnimationForLeftHandWeapon;
        }
        return false;
    }

    protected virtual void Move(Vector3 direction)
    {
        if (direction.sqrMagnitude > 1)
            direction = direction.normalized;
        direction.y = 0;

        var targetSpeed = GetMoveSpeed() * (isBlocking ? blockMoveSpeedMultiplier : (isDashing ? dashMoveSpeedMultiplier : 1f));
        CacheCharacterMovement.UpdateMovement(Time.deltaTime, targetSpeed, direction, inputJump);
    }

    protected virtual void UpdateMovements()
    {
        if (!IsOwnerClient)
            return;

        var moveDirection = inputMove;
        var dashDirection = dashInputMove;

        Move(isDashing ? dashDirection : moveDirection);
        // Turn character to move direction
        if (inputDirection.magnitude <= 0 && inputMove.magnitude > 0 || viewMode == ViewMode.ThirdPerson)
            inputDirection = inputMove;
        if (characterModel && characterModel.CacheAnimator && (characterModel.CacheAnimator.GetBool("DoAction") || Time.unscaledTime - lastActionTime <= returnToMoveDirectionDelay) && viewMode == ViewMode.ThirdPerson)
            inputDirection = cameraForward;
        if (!IsDead)
            Rotate(isDashing ? dashInputMove : inputDirection);

        if (!IsDead && !isBlocking)
        {
            if (inputAttack && GameplayManager.Singleton.CanAttack(this))
                Attack();
            else
                StopAttack();

            if (!inputCancelUsingSkill && releasedUseSkillHotkeyId >= 0 && GameplayManager.Singleton.CanAttack(this))
            {
                UseSkill(releasedUseSkillHotkeyId);
                holdingUseSkillHotkeyId = -1;
                releasedUseSkillHotkeyId = -1;
            }
        }

        inputJump = false;
    }

    protected void Rotate(Vector3 direction)
    {
        if (direction.sqrMagnitude != 0)
            CacheTransform.rotation = Quaternion.LookRotation(direction);
    }

    public void GetDamageLaunchTransform(bool isLeftHandWeapon, out Transform launchTransform)
    {
        if (characterModel == null || !characterModel.TryGetDamageLaunchTransform(isLeftHandWeapon, out launchTransform))
            launchTransform = damageLaunchTransform;
    }

    public void Attack()
    {
        if (isPlayingAttackAnim || isBlocking)
            return;

        if (attackingActionId < 0 && IsOwnerClient)
            CmdAttack();
    }

    public void StopAttack()
    {
        if (attackingActionId >= 0 && IsOwnerClient)
            CmdStopAttack();
    }

    public void UseSkill(sbyte hotkeyId)
    {
        SkillData skill;
        if (attackingActionId < 0 &&
            usingSkillHotkeyId < 0 &&
            IsOwnerClient && skills.TryGetValue(hotkeyId, out skill) &&
            GetSkillCoolDownCount(hotkeyId) > skill.coolDown)
        {
            lastSkillUseTimes[hotkeyId] = Time.unscaledTime;
            CmdUseSkill(hotkeyId);
        }
    }

    public float GetSkillCoolDownCount(sbyte hotkeyId)
    {
        return Time.unscaledTime - lastSkillUseTimes[hotkeyId];
    }

    IEnumerator AttackRoutine()
    {
        if (!isPlayingAttackAnim &&
            Hp > 0 &&
            characterModel != null &&
            characterModel.CacheAnimator != null)
        {
            isPlayingAttackAnim = true;
            AttackAnimation attackAnimation;
            if (weaponData != null && attackingActionId >= 0 && attackingActionId < 255 &&
                weaponData.AttackAnimations.TryGetValue(attackingActionId, out attackAnimation))
            {
                byte actionId = (byte)attackingActionId;
                yield return StartCoroutine(PlayAttackAnimationRoutine(attackAnimation, weaponData.attackFx, () =>
                {
                    // Launch damage entity on server only
                    if (IsServer)
                        weaponData.Launch(this, aimPosition, actionId);
                }));
                // If player still attacking, random new attacking action id
                if (IsServer && attackingActionId >= 0 && weaponData != null)
                    attackingActionId = weaponData.GetRandomAttackAnimation().actionId;
            }
            isPlayingAttackAnim = false;
        }
    }

    IEnumerator UseSkillRoutine()
    {
        if (!isPlayingUseSkillAnim &&
            Hp > 0 &&
            characterModel != null &&
            characterModel.CacheAnimator != null)
        {
            isPlayingUseSkillAnim = true;
            SkillData skillData;
            if (skills.TryGetValue(usingSkillHotkeyId, out skillData))
            {
                yield return StartCoroutine(PlayAttackAnimationRoutine(skillData.attackAnimation, skillData.attackFx, () =>
                {
                    // Launch damage entity on server only
                    if (IsServer)
                        skillData.Launch(this, aimPosition);
                }));
            }
            usingSkillHotkeyId = -1;
            isPlayingUseSkillAnim = false;
        }
    }

    IEnumerator PlayAttackAnimationRoutine(AttackAnimation attackAnimation, AudioClip[] attackFx, System.Action onAttack)
    {
        if (attackAnimation != null)
        {
            if (endActionDelayCoroutine != null)
                StopCoroutine(endActionDelayCoroutine);
            // Play attack animation
            characterModel.CacheAnimator.SetBool("DoAction", true);
            characterModel.CacheAnimator.SetInteger("ActionID", attackAnimation.actionId);
            characterModel.CacheAnimator.Play(0, 1, 0);

            // Wait to launch damage entity
            var speed = attackAnimation.speed;
            var animationDuration = attackAnimation.animationDuration;
            var launchDuration = attackAnimation.launchDuration;
            if (launchDuration > animationDuration)
                launchDuration = animationDuration;
            yield return new WaitForSeconds(launchDuration / speed);

            onAttack.Invoke();

            // Random play shoot sounds
            if (attackFx != null && attackFx.Length > 0 && AudioManager.Singleton != null)
                AudioSource.PlayClipAtPoint(attackFx[Random.Range(0, weaponData.attackFx.Length - 1)], CacheTransform.position, AudioManager.Singleton.sfxVolumeSetting.Level);

            // Wait till animation end
            yield return new WaitForSeconds((animationDuration - launchDuration) / speed);

            // Attack animation ended
            endActionDelayCoroutine = StartCoroutine(DelayEndAction(endActionDelay));
        }
    }

    IEnumerator DelayEndAction(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        characterModel.CacheAnimator.SetBool("DoAction", false);
    }

    public virtual bool ReceiveDamage(CharacterEntity attacker, int damage, byte type, int dataId, byte actionId)
    {
        if (!IsServer)
            return false;

        if (Hp <= 0 || isInvincible)
            return false;

        if (!GameplayManager.Singleton.CanReceiveDamage(this, attacker))
            return false;

        RpcEffect(attacker.ObjectId, type, dataId, actionId);

        // Calculate damage and reduceHp
        int reduceHp = (int)(damage + ((float)damage * TotalIncreaseDamageRate) - ((float)damage * TotalReduceReceiveDamageRate)) - TotalDefend;

        // Blocking
        if (isBlocking)
            reduceHp -= Mathf.CeilToInt(damage * TotalBlockReduceDamageRate);

        // Avoid increasing hp by damage
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
            {
                var statusEffects = new List<StatusEffectEntity>(appliedStatusEffects.Values);
                foreach (var statusEffect in statusEffects)
                {
                    if (statusEffect)
                        Destroy(statusEffect.gameObject);
                }
                statusEffects.Clear();
                if (onDead != null)
                    onDead.Invoke();
                attacker.KilledTarget(this);
                ++dieCount;
            }
        }
        return true;
    }

    public void KilledTarget(CharacterEntity target)
    {
        if (!IsServer)
            return;

        var gameplayManager = GameplayManager.Singleton;
        var targetLevel = target.level;
        var maxLevel = gameplayManager.maxLevel;
        Exp += Mathf.CeilToInt(target.RewardExp * TotalExpRate);
        score += Mathf.CeilToInt(target.KillScore * TotalScoreRate);
        foreach (var rewardCurrency in gameplayManager.rewardCurrencies)
        {
            var currencyId = rewardCurrency.currencyId;
            var amount = rewardCurrency.amount.Calculate(targetLevel, maxLevel);
            TargetRewardCurrency(ConnectionId, currencyId, amount);
        }
        ++killCount;
        GameNetworkManager.Singleton.SendKillNotify(playerName, target.playerName, weaponData == null ? string.Empty : weaponData.GetId());
    }

    public void Heal(int amount)
    {
        if (!IsServer)
            return;

        if (Hp <= 0)
            return;

        Hp += amount;
    }

    public virtual float GetAttackRange()
    {
        if (weaponData == null || weaponData.damagePrefab == null)
            return 0;
        return weaponData.damagePrefab.GetAttackRange();
    }

    protected void UpdateSkills()
    {
        skills.Clear();
        if (characterData != null)
        {
            foreach (var skill in characterData.skills)
            {
                skills[skill.hotkeyId] = skill;
            }
        }
        if (headData != null)
        {
            foreach (var skill in headData.skills)
            {
                skills[skill.hotkeyId] = skill;
            }
        }
        if (weaponData != null)
        {
            foreach (var skill in weaponData.skills)
            {
                skills[skill.hotkeyId] = skill;
            }
        }
        if (customEquipmentDict.Count > 0)
        {
            foreach (var customEquipment in customEquipmentDict.Values)
            {
                foreach (var skill in customEquipment.skills)
                {
                    skills[skill.hotkeyId] = skill;
                }
            }
        }
    }

    protected virtual void OnCharacterChanged(int value)
    {
        refreshingSumAddStats = true;
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
        if (customEquipmentDict != null)
        {
            characterModel.ClearCustomModels();
            foreach (var customEquipmentEntry in customEquipmentDict.Values)
            {
                characterModel.SetCustomModel(customEquipmentEntry.containerIndex, customEquipmentEntry.modelObject);
            }
        }
        characterModel.gameObject.SetActive(true);
        UpdateCharacterModelHiddingState();
        UpdateSkills();
    }

    protected virtual void OnHeadChanged(int value)
    {
        refreshingSumAddStats = true;
        selectHead = value;
        headData = GameInstance.GetHead(value);
        if (characterModel != null && headData != null)
            characterModel.SetHeadModel(headData.modelObject);
        UpdateCharacterModelHiddingState();
        UpdateSkills();
    }

    protected virtual void OnWeaponChanged(int value)
    {
        refreshingSumAddStats = true;
        selectWeapon = value;
        if (IsServer)
        {
            if (defaultSelectWeapon == 0)
                defaultSelectWeapon = value;
        }
        weaponData = GameInstance.GetWeapon(value);
        if (characterModel != null && weaponData != null)
            characterModel.SetWeaponModel(weaponData.rightHandObject, weaponData.leftHandObject, weaponData.shieldObject);
        UpdateCharacterModelHiddingState();
        UpdateSkills();
    }

    protected virtual void OnCustomEquipmentsChanged(Operation op, int itemIndex)
    {
        refreshingSumAddStats = true;
        if (characterModel != null)
            characterModel.ClearCustomModels();
        customEquipmentDict.Clear();
        for (var i = 0; i < selectCustomEquipments.Count; ++i)
        {
            var customEquipmentData = GameInstance.GetCustomEquipment(selectCustomEquipments[i]);
            if (customEquipmentData != null &&
                !customEquipmentDict.ContainsKey(customEquipmentData.containerIndex))
            {
                customEquipmentDict[customEquipmentData.containerIndex] = customEquipmentData;
                if (characterModel != null)
                    characterModel.SetCustomModel(customEquipmentData.containerIndex, customEquipmentData.modelObject);
            }
        }
        UpdateCharacterModelHiddingState();
        UpdateSkills();
    }

    protected virtual void OnAttributeAmountsChanged(AttributeAmounts value)
    {
        refreshingSumAddStats = true;
    }

    public void ChangeWeapon(WeaponData weaponData)
    {
        if (weaponData == null)
            return;
        selectWeapon = weaponData.GetHashId();
    }

    public void UpdateCharacterModelHiddingState()
    {
        if (characterModel == null)
            return;
        var renderers = characterModel.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
            renderer.enabled = !IsHidding;
    }

    public virtual Vector3 GetSpawnPosition()
    {
        return GameplayManager.Singleton.GetCharacterSpawnPosition();
    }

    public virtual void OnSpawn() { }

    public void ServerInvincible()
    {
        if (!IsServer)
            return;
        invincibleTime = Time.unscaledTime;
        isInvincible = true;
    }

    public void ServerSpawn(bool isWatchedAds)
    {
        if (!IsServer)
            return;
        if (Respawn(isWatchedAds))
        {
            ServerInvincible();
            OnSpawn();
            var position = GetSpawnPosition();
            CacheTransform.position = position;
            TargetSpawn(ConnectionId, position);
            ServerRevive();
        }
    }

    public void ServerRespawn(bool isWatchedAds)
    {
        if (!IsServer)
            return;
        if (CanRespawn(isWatchedAds))
            ServerSpawn(isWatchedAds);
    }

    public void ServerRevive()
    {
        if (!IsServer)
            return;
        if (defaultSelectWeapon != 0)
            selectWeapon = defaultSelectWeapon;
        isPlayingAttackAnim = false;
        isDead = false;
        Hp = TotalHp;
        holdingUseSkillHotkeyId = -1;
        releasedUseSkillHotkeyId = -1;
    }

    public void CmdReady()
    {
        CallNetFunction(_CmdReady, FunctionReceivers.Server);
    }

    [NetFunction]
    protected void _CmdReady()
    {
        if (!isReady)
        {
            ServerSpawn(false);
            isReady = true;
        }
    }

    public void CmdRespawn(bool isWatchedAds)
    {
        CallNetFunction(_CmdRespawn, FunctionReceivers.Server, isWatchedAds);
    }

    [NetFunction]
    protected void _CmdRespawn(bool isWatchedAds)
    {
        ServerRespawn(isWatchedAds);
    }

    public void CmdAttack()
    {
        CallNetFunction(_CmdAttack, FunctionReceivers.Server);
    }

    [NetFunction]
    protected void _CmdAttack()
    {
        if (weaponData != null)
            attackingActionId = weaponData.GetRandomAttackAnimation().actionId;
        else
            attackingActionId = -1;
    }

    public void CmdStopAttack()
    {
        CallNetFunction(_CmdStopAttack, FunctionReceivers.Server);
    }

    [NetFunction]
    protected void _CmdStopAttack()
    {
        attackingActionId = -1;
    }

    public void CmdUseSkill(sbyte hotkeyId)
    {
        CallNetFunction(_CmdUseSkill, FunctionReceivers.Server, hotkeyId);
    }

    [NetFunction]
    protected void _CmdUseSkill(sbyte hotkeyId)
    {
        if (skills.ContainsKey(hotkeyId))
            usingSkillHotkeyId = hotkeyId;
    }

    public void CmdAddAttribute(int id)
    {
        CallNetFunction(_CmdAddAttribute, FunctionReceivers.Server, id);
    }

    [NetFunction]
    protected void _CmdAddAttribute(int id)
    {
        if (statPoint > 0)
        {
            if (GameplayManager.Singleton.Attributes.ContainsKey(id))
            {
                attributeAmounts = attributeAmounts.Increase(id, 1);
                --statPoint;
            }
        }
    }

    public void CmdDash()
    {
        CallNetFunction(_CmdDash, FunctionReceivers.Server);
    }

    [NetFunction]
    protected void _CmdDash()
    {
        // Play dash animation on other clients
        RpcDash();
    }

    public void RpcApplyStatusEffect(int dataId)
    {
        CallNetFunction(_RpcApplyStatusEffect, FunctionReceivers.All, dataId);
    }

    [NetFunction]
    protected void _RpcApplyStatusEffect(int dataId)
    {
        // Destroy applied status effect, because it cannot be stacked
        RemoveAppliedStatusEffect(dataId);
        StatusEffectEntity statusEffect;
        if (GameInstance.StatusEffects.TryGetValue(dataId, out statusEffect) && Random.value <= statusEffect.applyRate)
        {
            refreshingSumAddStats = true;
            // Found prefab, instantiates to character
            statusEffect = Instantiate(statusEffect, transform.position, transform.rotation, transform);
            // Just in case the game object might be not activated by default
            statusEffect.gameObject.SetActive(true);
            // Set applying character
            statusEffect.Applied(this);
            // Add to applied status effects
            appliedStatusEffects[dataId] = statusEffect;
        }
    }

    public void RemoveAppliedStatusEffect(int dataId)
    {
        StatusEffectEntity statusEffect;
        if (appliedStatusEffects.TryGetValue(dataId, out statusEffect))
        {
            refreshingSumAddStats = true;
            appliedStatusEffects.Remove(dataId);
            if (statusEffect)
                Destroy(statusEffect.gameObject);
        }
    }

    public void RpcEffect(uint triggerId, byte effectType, int dataId, byte actionId)
    {
        CallNetFunction(_RpcEffect, FunctionReceivers.All, triggerId, effectType, dataId, actionId);
    }

    [NetFunction]
    protected void _RpcEffect(uint triggerId, byte effectType, int dataId, byte actionId)
    {
        if (IsHidding)
            return;
        LiteNetLibIdentity triggerObject;
        if (Manager.Assets.TryGetSpawnedObject(triggerId, out triggerObject))
        {
            if (effectType == RPC_EFFECT_DAMAGE_SPAWN || effectType == RPC_EFFECT_DAMAGE_HIT)
            {
                WeaponData weaponData;
                if (GameInstance.Weapons.TryGetValue(dataId, out weaponData))
                {
                    var damagePrefab = weaponData.damagePrefab;
                    if (weaponData.AttackAnimations.ContainsKey(actionId) &&
                        weaponData.AttackAnimations[actionId].damagePrefab != null)
                        damagePrefab = weaponData.AttackAnimations[actionId].damagePrefab;
                    if (damagePrefab)
                    {
                        switch (effectType)
                        {
                            case RPC_EFFECT_DAMAGE_SPAWN:
                                EffectEntity.PlayEffect(damagePrefab.spawnEffectPrefab, effectTransform);
                                break;
                            case RPC_EFFECT_DAMAGE_HIT:
                                EffectEntity.PlayEffect(damagePrefab.hitEffectPrefab, effectTransform);
                                break;
                        }
                    }
                }
            }
            else if (effectType == RPC_EFFECT_TRAP_HIT)
            {
                var trap = triggerObject.GetComponent<TrapEntity>();
                if (trap != null)
                    EffectEntity.PlayEffect(trap.hitEffectPrefab, effectTransform);
            }
            else if (effectType == RPC_EFFECT_SKILL_SPAWN || effectType == RPC_EFFECT_SKILL_HIT)
            {
                SkillData skillData;
                if (GameInstance.Skills.TryGetValue(dataId, out skillData) &&
                    skillData.damagePrefab != null)
                {
                    var damagePrefab = skillData.damagePrefab;
                    switch (effectType)
                    {
                        case RPC_EFFECT_SKILL_SPAWN:
                            EffectEntity.PlayEffect(damagePrefab.spawnEffectPrefab, effectTransform);
                            break;
                        case RPC_EFFECT_SKILL_HIT:
                            EffectEntity.PlayEffect(damagePrefab.hitEffectPrefab, effectTransform);
                            break;
                    }
                }
            }
        }
    }

    public void RpcDash()
    {
        CallNetFunction(_RpcDash, FunctionReceivers.All);
    }

    [NetFunction]
    protected void _RpcDash()
    {
        // Just play dash animation on another clients
        if (!IsOwnerClient)
        {
            isDashing = true;
            dashingTime = Time.unscaledTime;
        }
    }

    public void TargetDead(long conn)
    {
        CallNetFunction(_TargetDead, conn);
    }

    [NetFunction]
    protected void _TargetDead()
    {
        deathTime = Time.unscaledTime;
    }

    public void TargetSpawn(long conn, Vector3 position)
    {
        CallNetFunction(_TargetSpawn, conn, position);
    }

    [NetFunction]
    protected void _TargetSpawn(Vector3 position)
    {
        transform.position = position;
    }

    public void TargetRewardCurrency(long conn, string currencyId, int amount)
    {
        CallNetFunction(_TargetRewardCurrency, conn, currencyId, amount);
    }

    [NetFunction]
    protected void _TargetRewardCurrency(string currencyId, int amount)
    {
        MonetizationManager.Save.AddCurrency(currencyId, amount);
    }
}
