using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class UIGameplay : MonoBehaviour
{
    public Text textLevel;
    public Text textExp;
    public Image fillExp;
    public Text textStatPoint;
    public Text textHp;
    public Text textAttack;
    public Text textDefend;
    public Text textMoveSpeed;
    public Text textRespawnCountDown;
    public Text textWatchedAdsCount;
    public UIBlackFade blackFade;
    public GameObject respawnUiContainer;
    public GameObject respawnButtonContainer;
    public UIRandomAttributes randomAttributes;
    public UIUserRanking[] userRankings;
    public UIUserRanking localRanking;
    public GameObject[] mobileOnlyUis;
    public GameObject[] hidingIfDedicateServerUis;
    private bool isRespawnShown;
    private bool isRandomedAttributesShown;
    private bool canRandomAttributes;

    private void Awake()
    {
        foreach (var mobileOnlyUi in mobileOnlyUis)
        {
            mobileOnlyUi.SetActive(Application.isMobilePlatform);
        }
        foreach (var hidingIfDedicateUi in hidingIfDedicateServerUis)
        {
            hidingIfDedicateUi.SetActive(!NetworkServer.active || NetworkServer.localClientActive);
        }
        if (NetworkServer.active)
            FadeOut();
    }

    private void OnEnable()
    {
        StartCoroutine(SetupCanRandomAttributes());
    }

    IEnumerator SetupCanRandomAttributes()
    {
        canRandomAttributes = false;
        yield return new WaitForSeconds(1);
        canRandomAttributes = true;
    }

    private void Update()
    {
        var localCharacter = CharacterEntity.Local;
        if (localCharacter == null)
            return;

        var level = localCharacter.level;
        var exp = localCharacter.Exp;
        var nextExp = GameplayManager.Singleton.GetExp(level);
        if (textLevel != null)
            textLevel.text = "LV" + level.ToString("N0");

        if (textExp != null)
            textExp.text = exp.ToString("N0") + "/" + nextExp.ToString("N0");

        if (fillExp != null)
            fillExp.fillAmount = (float)exp / (float)nextExp;

        if (textStatPoint != null)
            textStatPoint.text = localCharacter.statPoint.ToString("N0");

        if (textHp != null)
            textHp.text = localCharacter.TotalHp.ToString("N0");

        if (textAttack != null)
            textAttack.text = localCharacter.TotalAttack.ToString("N0");

        if (textDefend != null)
            textDefend.text = localCharacter.TotalDefend.ToString("N0");

        if (textMoveSpeed != null)
            textMoveSpeed.text = localCharacter.TotalMoveSpeed.ToString("N0");

        if (localCharacter.Hp <= 0)
        {
            if (!isRespawnShown)
            {
                if (respawnUiContainer != null)
                    respawnUiContainer.SetActive(true);
                isRespawnShown = true;
            }
            if (isRespawnShown)
            {
                var remainTime = GameplayManager.Singleton.respawnDuration - (Time.unscaledTime - localCharacter.deathTime);
                var watchAdsRespawnAvailable = GameplayManager.Singleton.watchAdsRespawnAvailable;
                if (remainTime < 0)
                    remainTime = 0;
                if (textRespawnCountDown != null)
                    textRespawnCountDown.text = Mathf.Abs(remainTime).ToString("N0");
                if (textWatchedAdsCount != null)
                    textWatchedAdsCount.text = (watchAdsRespawnAvailable - localCharacter.watchAdsCount) + "/" + watchAdsRespawnAvailable;
                if (respawnButtonContainer != null)
                    respawnButtonContainer.SetActive(remainTime == 0);
            }
        }
        else
        {
            if (respawnUiContainer != null)
                respawnUiContainer.SetActive(false);
            isRespawnShown = false;
        }

        if (localCharacter.Hp > 0 && localCharacter.statPoint > 0 && canRandomAttributes)
        {
            if (!isRandomedAttributesShown)
            {
                if (randomAttributes != null)
                {
                    randomAttributes.uiGameplay = this;
                    randomAttributes.gameObject.SetActive(true);
                    randomAttributes.Random();
                }
                isRandomedAttributesShown = true;
            }
        }
        else
        {
            if (randomAttributes != null)
                randomAttributes.gameObject.SetActive(false);
            isRandomedAttributesShown = false;
        }
    }

    public void UpdateRankings(UserRanking[] rankings)
    {
        for (var i = 0; i < userRankings.Length; ++i)
        {
            var userRanking = userRankings[i];
            if (i < rankings.Length)
            {
                var ranking = rankings[i];
                userRanking.SetData(i + 1, ranking);
            }
            else
                userRanking.Clear();
        }
    }

    public void UpdateLocalRank(int rank, UserRanking ranking)
    {
        if (localRanking != null)
            localRanking.SetData(rank, ranking);
    }

    public void AddAttribute(string name)
    {
        var character = CharacterEntity.Local;
        if (character == null || character.statPoint == 0)
            return;
        character.CmdAddAttribute(name);
        StartCoroutine(SetupCanRandomAttributes());
    }

    public void Respawn()
    {
        var character = CharacterEntity.Local;
        if (character == null)
            return;
        character.CmdRespawn(false);
    }

    public void WatchAdsRespawn()
    {
        var character = CharacterEntity.Local;
        if (character == null)
            return;

        if (character.watchAdsCount >= GameplayManager.Singleton.watchAdsRespawnAvailable)
        {
            character.CmdRespawn(false);
            return;
        }
        MonetizationManager.ShowAd(GameInstance.Singleton.watchAdsRespawnPlacement, OnWatchAdsRespawnResult);
    }

    private void OnWatchAdsRespawnResult(MonetizationManager.RemakeShowResult result)
    {
        if (result == MonetizationManager.RemakeShowResult.Finished)
        {
            var character = CharacterEntity.Local;
            character.CmdRespawn(true);
        }
    }

    public void ExitGame()
    {
        GameNetworkManager.Singleton.StopHost();
    }

    public void FadeIn()
    {
        if (blackFade != null)
            blackFade.FadeIn();
    }

    public void FadeOut()
    {
        if (blackFade != null)
            blackFade.FadeOut();
    }
}
