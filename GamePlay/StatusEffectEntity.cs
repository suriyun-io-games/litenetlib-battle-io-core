using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatusEffectEntity : MonoBehaviour
{
    public string GetId()
    {
        return name;
    }

    public int GetHashId()
    {
        return GetId().MakeHashId();
    }

    [Range(0f, 1f)]
    public float applyRate = 1f;
    public int recoveryHpPerSeconds = 0;
    public CharacterStats addStats;
    public float lifeTime;
    private CharacterEntity characterEntity;

    private void Start()
    {
        if (lifeTime > 0)
            Destroy(gameObject, lifeTime);
    }

    private void OnDestroy()
    {
        if (characterEntity)
            characterEntity.RemoveAppliedStatusEffect(GetHashId());
    }

    public void Recovery()
    {
        if (characterEntity)
            characterEntity.Hp += recoveryHpPerSeconds;
    }

    public void Applied(CharacterEntity characterEntity)
    {
        this.characterEntity = characterEntity;
        InvokeRepeating("Recovery", 0, 1);
    }
}
