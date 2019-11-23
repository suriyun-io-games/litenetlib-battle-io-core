using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIMainMenu : MonoBehaviour
{
    public enum PreviewState
    {
        Idle,
        Run,
        Dead,
    }
    public Text textSelectCharacter;
    public Text textSelectHead;
    public Text textSelectWeapon;
    public Text textHp;
    public Text textAttack;
    public Text textDefend;
    public Text textMoveSpeed;
    public Text textExpRate;
    public Text textScoreRate;
    public Text textHpRecoveryRate;
    public Text textDamageRateLeechHp;
    public Text textSpreadDamages;
    public InputField inputName;
    public Transform characterModelTransform;
    public string onlineNetworkAddress;
    public int onlineNetworkPort;
    public UIEnterNetworkAddress enterNetworkAddressDialog;
    public UILanNetworking lanNetworkingDialog;
    private int selectCharacter = 0;
    private int selectHead = 0;
    private int selectWeapon = 0;
    private bool readyToUpdate;
    // Showing character / items
    public CharacterModel characterModel;
    public CharacterData characterData;
    public HeadData headData;
    public WeaponData weaponData;
    public PreviewState previewState;

    public int SelectCharacter
    {
        get { return selectCharacter; }
        set
        {
            selectCharacter = value;
            if (selectCharacter < 0)
                selectCharacter = MaxCharacter;
            if (selectCharacter > MaxCharacter)
                selectCharacter = 0;
            UpdateCharacter();
        }
    }

    public int SelectHead
    {
        get { return selectHead; }
        set
        {
            selectHead = value;
            if (selectHead < 0)
                selectHead = MaxHead;
            if (selectHead > MaxHead)
                selectHead = 0;
            UpdateHead();
        }
    }

    public int SelectWeapon
    {
        get { return selectWeapon; }
        set
        {
            selectWeapon = value;
            if (selectWeapon < 0)
                selectWeapon = MaxWeapon;
            if (selectWeapon > MaxWeapon)
                selectWeapon = 0;
            UpdateWeapon();
        }
    }

    public int MaxHead
    {
        get { return GameInstance.AvailableHeads.Count - 1; }
    }

    public int MaxCharacter
    {
        get { return GameInstance.AvailableCharacters.Count - 1; }
    }

    public int MaxWeapon
    {
        get { return GameInstance.AvailableWeapons.Count - 1; }
    }

    private void Start()
    {
        StartCoroutine(StartRoutine());
    }

    private IEnumerator StartRoutine()
    {
        yield return null;
        OnClickLoadData();
        readyToUpdate = true;
    }

    private void Update()
    {
        if (!readyToUpdate)
            return;

        if (textSelectCharacter != null)
            textSelectCharacter.text = (SelectCharacter + 1) + "/" + (MaxCharacter + 1);
        if (textSelectHead != null)
            textSelectHead.text = (SelectHead + 1) + "/" + (MaxHead + 1);
        if (textSelectWeapon != null)
            textSelectWeapon.text = (SelectWeapon + 1) + "/" + (MaxWeapon + 1);

        var totalStats = GetTotalStats();

        if (textHp != null)
            textHp.text = totalStats.addHp.ToString("N0");
        if (textAttack != null)
            textAttack.text = totalStats.addAttack.ToString("N0");
        if (textDefend != null)
            textDefend.text = totalStats.addDefend.ToString("N0");
        if (textMoveSpeed != null)
            textMoveSpeed.text = totalStats.addMoveSpeed.ToString("N0");
        if (textExpRate != null)
            textExpRate.text = (totalStats.addExpRate * 100).ToString("N0") + "%";
        if (textScoreRate != null)
            textScoreRate.text = (totalStats.addScoreRate * 100).ToString("N0") + "%";
        if (textHpRecoveryRate != null)
            textHpRecoveryRate.text = (totalStats.addHpRecoveryRate * 100).ToString("N0") + "%";
        if (textDamageRateLeechHp != null)
            textDamageRateLeechHp.text = (totalStats.addDamageRateLeechHp * 100).ToString("N0") + "%";
        if (textSpreadDamages != null)
            textSpreadDamages.text = (totalStats.addSpreadDamages * 100).ToString("N0") + "%";

        if (characterModel != null)
        {
            var animator = characterModel.TempAnimator;
            switch (previewState)
            {
                case PreviewState.Idle:
                    animator.SetBool("IsDead", false);
                    animator.SetFloat("JumpSpeed", 0);
                    animator.SetFloat("MoveSpeed", 0);
                    animator.SetBool("IsGround", true);
                    animator.SetBool("IsDash", false);
                    animator.SetBool("DoAction", false);
                    animator.SetBool("IsIdle", true);
                    break;
                case PreviewState.Run:
                    animator.SetBool("IsDead", false);
                    animator.SetFloat("JumpSpeed", 0);
                    animator.SetFloat("MoveSpeed", 1);
                    animator.SetBool("IsGround", true);
                    animator.SetBool("IsDash", false);
                    animator.SetBool("DoAction", false);
                    animator.SetBool("IsIdle", false);
                    break;
                case PreviewState.Dead:
                    animator.SetBool("IsDead", true);
                    animator.SetFloat("JumpSpeed", 0);
                    animator.SetFloat("MoveSpeed", 0);
                    animator.SetBool("IsGround", true);
                    animator.SetBool("IsDash", false);
                    animator.SetBool("DoAction", false);
                    animator.SetBool("IsIdle", false);
                    break;
            }
        }
    }

    private void UpdateCharacter()
    {
        if (characterModel != null)
            Destroy(characterModel.gameObject);
        characterData = GameInstance.GetAvailableCharacter(SelectCharacter);
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
        var animator = characterModel.TempAnimator;
        animator.SetInteger("WeaponAnimId", weaponData != null ? weaponData.weaponAnimId : 0);
    }

    private void UpdateHead()
    {
        headData = GameInstance.GetAvailableHead(SelectHead);
        if (characterModel != null && headData != null)
            characterModel.SetHeadModel(headData.modelObject);
    }

    private void UpdateWeapon()
    {
        weaponData = GameInstance.GetAvailableWeapon(SelectWeapon);
        if (characterModel != null && weaponData != null)
        {
            characterModel.SetWeaponModel(weaponData.rightHandObject, weaponData.leftHandObject, weaponData.shieldObject);
            var animator = characterModel.TempAnimator;
            animator.SetInteger("WeaponAnimId", weaponData.weaponAnimId);
        }
    }

    public void OnClickBackCharacter()
    {
        --SelectCharacter;
    }

    public void OnClickNextCharacter()
    {
        ++SelectCharacter;
    }

    public void OnClickBackHead()
    {
        --SelectHead;
    }

    public void OnClickNextHead()
    {
        ++SelectHead;
    }

    public void OnClickBackWeapon()
    {
        --SelectWeapon;
    }

    public void OnClickNextWeapon()
    {
        ++SelectWeapon;
    }

    public void OnInputNameChanged(string eventInput)
    {
        PlayerSave.SetPlayerName(inputName.text);
    }

    public void OnClickSaveData()
    {
        PlayerSave.SetCharacter(SelectCharacter);
        PlayerSave.SetHead(SelectHead);
        PlayerSave.SetWeapon(SelectWeapon);
        PlayerSave.SetPlayerName(inputName.text);
    }

    public void OnClickLan()
    {
        OnClickSaveData();
        if (lanNetworkingDialog != null)
            lanNetworkingDialog.Show();
    }

    public void OnClickOnline()
    {
        OnClickSaveData();
        if (!string.IsNullOrEmpty(onlineNetworkAddress) && onlineNetworkPort >= 0)
        {
            var networkManager = GameNetworkManager.Singleton;
            networkManager.networkAddress = onlineNetworkAddress;
            networkManager.networkPort = onlineNetworkPort;
            networkManager.StartClient();
            return;
        }
        if (enterNetworkAddressDialog != null)
            enterNetworkAddressDialog.Show();
    }

    public void OnClickLoadData()
    {
        inputName.text = PlayerSave.GetPlayerName();
        SelectHead = PlayerSave.GetHead();
        SelectCharacter = PlayerSave.GetCharacter();
        SelectWeapon = PlayerSave.GetWeapon();
    }

    public void UpdateAvailableItems()
    {
        GameInstance.Singleton.UpdateAvailableItems();
    }

    public CharacterStats GetTotalStats()
    {
        var totalStats = new CharacterStats();
        if (characterData != null)
            totalStats += characterData.stats;
        if (headData != null)
            totalStats += headData.stats;
        if (weaponData != null)
            totalStats += weaponData.stats;
        return totalStats;
    }
}
