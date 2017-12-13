using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct CharacterStats
{
    public int addHp;
    public int addAttack;
    public int addDefend;
    public int addMoveSpeed;
    public float addExpRate;
    public float addScoreRate;
    public float addHpRecoveryRate;
    public float addDamageRateLeechHp;
    public int addSpreadDamages;

    public static CharacterStats operator +(CharacterStats a, CharacterStats b)
    {
        var result = new CharacterStats();
        result.addHp = a.addHp + b.addHp;
        result.addAttack = a.addAttack + b.addAttack;
        result.addDefend = a.addDefend + b.addDefend;
        result.addMoveSpeed = a.addMoveSpeed + b.addMoveSpeed;
        result.addExpRate = a.addExpRate + b.addExpRate;
        result.addScoreRate = a.addScoreRate + b.addScoreRate;
        result.addHpRecoveryRate = a.addHpRecoveryRate + b.addHpRecoveryRate;
        result.addDamageRateLeechHp = a.addDamageRateLeechHp + b.addDamageRateLeechHp;
        result.addSpreadDamages = a.addSpreadDamages + b.addSpreadDamages;
        return result;
    }

    public static CharacterStats operator -(CharacterStats a, CharacterStats b)
    {
        var result = new CharacterStats();
        result.addHp = a.addHp - b.addHp;
        result.addAttack = a.addAttack - b.addAttack;
        result.addDefend = a.addDefend - b.addDefend;
        result.addMoveSpeed = a.addMoveSpeed - b.addMoveSpeed;
        result.addExpRate = a.addExpRate - b.addExpRate;
        result.addScoreRate = a.addScoreRate - b.addScoreRate;
        result.addHpRecoveryRate = a.addHpRecoveryRate - b.addHpRecoveryRate;
        result.addDamageRateLeechHp = a.addDamageRateLeechHp - b.addDamageRateLeechHp;
        result.addSpreadDamages = a.addSpreadDamages - b.addSpreadDamages;
        return result;
    }
}
