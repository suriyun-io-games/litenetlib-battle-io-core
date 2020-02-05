using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StatusEffectEntity : MonoBehaviour
{
    public int GetHashId()
    {
        return hashId;
    }

    [SerializeField]
    private int hashId;

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
        {
            characterEntity.RemoveAppliedStatusEffect(GetHashId());
        }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (hashId != name.MakeHashId())
        {
            hashId = name.MakeHashId();
            EditorUtility.SetDirty(this);
        }
#endif
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
