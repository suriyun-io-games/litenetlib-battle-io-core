using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class ItemData : InGameProductData
{
    [Tooltip("If this is true, player have to buy this item to unlock and able to use.")]
    public bool isLock;
    [Header("Attributes")]
    [System.Obsolete("This will be deprecated on next version, use ItemData.stats instead")]
    public int addHp;
    [System.Obsolete("This will be deprecated on next version, use ItemData.stats instead")]
    public int addAttack;
    [System.Obsolete("This will be deprecated on next version, use ItemData.stats instead")]
    public int addDefend;
    [System.Obsolete("This will be deprecated on next version, use ItemData.stats instead")]
    public int addMoveSpeed;
    public CharacterStats stats;

    public virtual bool IsUnlock()
    {
        return !isLock || IsBought();
    }

    public override bool CanBuy()
    {
        canBuyOnlyOnce = true;
        return base.CanBuy();
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        stats.addHp = addHp;
        stats.addAttack = addAttack;
        stats.addDefend = addDefend;
        stats.addMoveSpeed = addMoveSpeed;
        EditorUtility.SetDirty(this);
    }
#endif
}
