using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatusEffectEntity : MonoBehaviour
{
    [Range(0f, 1f)]
    public float applyRate = 1f;
    public int recoveryHpPerSeconds = 0;
    public CharacterStats addStats;
    public float lifeTime;
    private int hashId;
    private CharacterEntity receiverCharacterEntity;
    private CharacterEntity applierCharacterEntity;
    private float startTime;
    private bool destroyed;

    private void Start()
    {
        if (lifeTime >= 0)
            Destroy(gameObject, lifeTime);
        startTime = Time.unscaledTime;
    }

    private void OnDestroy()
    {
        if (receiverCharacterEntity)
            receiverCharacterEntity.RemoveAppliedStatusEffect(GetHashId());
    }

    public int GetHashId()
    {
        return hashId;
    }

    public void SetHashId()
    {
        hashId = name.MakeHashId();
    }

    public void Recovery()
    {
        if (BaseNetworkGameManager.Singleton.IsMatchEnded)
            return;
        if (receiverCharacterEntity && receiverCharacterEntity.Hp > 0)
        {
            if (recoveryHpPerSeconds > 0 || GameplayManager.Singleton.CanReceiveDamage(receiverCharacterEntity, applierCharacterEntity))
                receiverCharacterEntity.Hp += recoveryHpPerSeconds;
            if (receiverCharacterEntity.Hp <= 0)
            {
                if (applierCharacterEntity)
                    applierCharacterEntity.KilledTarget(receiverCharacterEntity);
                Destroy(gameObject);
            }
        }
    }

    public void Applied(CharacterEntity receiverCharacterEntity, CharacterEntity applierCharacterEntity)
    {
        this.receiverCharacterEntity = receiverCharacterEntity;
        this.applierCharacterEntity = applierCharacterEntity;
        InvokeRepeating(nameof(Recovery), 0, 1);
    }
}
