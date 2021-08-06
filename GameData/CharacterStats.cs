using LiteNetLib.Utils;

[System.Serializable]
public struct CharacterStats : INetSerializable
{
    public int addHp;
    public int addAttack;
    public int addDefend;
    public int addMoveSpeed;
    public float addExpRate;
    public float addScoreRate;
    public float addHpRecoveryRate;
    public float addBlockReduceDamageRate;
    public float addDamageRateLeechHp;
    public int addSpreadDamages;
    public float increaseDamageRate;
    public float reduceReceiveDamageRate;

    public void Deserialize(NetDataReader reader)
    {
        addHp = reader.GetInt();
        addAttack = reader.GetInt();
        addDefend = reader.GetInt();
        addMoveSpeed = reader.GetInt();
        addExpRate = reader.GetFloat();
        addScoreRate = reader.GetFloat();
        addHpRecoveryRate = reader.GetFloat();
        addDamageRateLeechHp = reader.GetFloat();
        addSpreadDamages = reader.GetInt();
        increaseDamageRate = reader.GetFloat();
        reduceReceiveDamageRate = reader.GetFloat();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(addHp);
        writer.Put(addAttack);
        writer.Put(addDefend);
        writer.Put(addMoveSpeed);
        writer.Put(addExpRate);
        writer.Put(addScoreRate);
        writer.Put(addHpRecoveryRate);
        writer.Put(addDamageRateLeechHp);
        writer.Put(addSpreadDamages);
        writer.Put(increaseDamageRate);
        writer.Put(reduceReceiveDamageRate);
    }
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
        result.addBlockReduceDamageRate = a.addBlockReduceDamageRate + b.addBlockReduceDamageRate;
        result.addDamageRateLeechHp = a.addDamageRateLeechHp + b.addDamageRateLeechHp;
        result.addSpreadDamages = a.addSpreadDamages + b.addSpreadDamages;
        result.increaseDamageRate = a.increaseDamageRate + b.increaseDamageRate;
        result.reduceReceiveDamageRate = a.reduceReceiveDamageRate + b.reduceReceiveDamageRate;
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
        result.addBlockReduceDamageRate = a.addBlockReduceDamageRate - b.addBlockReduceDamageRate;
        result.addDamageRateLeechHp = a.addDamageRateLeechHp - b.addDamageRateLeechHp;
        result.addSpreadDamages = a.addSpreadDamages - b.addSpreadDamages;
        result.increaseDamageRate = a.increaseDamageRate - b.increaseDamageRate;
        result.reduceReceiveDamageRate = a.reduceReceiveDamageRate - b.reduceReceiveDamageRate;
        return result;
    }

    public static CharacterStats operator *(CharacterStats a, short b)
    {
        var result = new CharacterStats();
        result.addHp = a.addHp * b;
        result.addAttack = a.addAttack * b;
        result.addDefend = a.addDefend * b;
        result.addMoveSpeed = a.addMoveSpeed * b;
        result.addExpRate = a.addExpRate * b;
        result.addScoreRate = a.addScoreRate * b;
        result.addHpRecoveryRate = a.addHpRecoveryRate * b;
        result.addBlockReduceDamageRate = a.addBlockReduceDamageRate * b;
        result.addDamageRateLeechHp = a.addDamageRateLeechHp * b;
        result.addSpreadDamages = a.addSpreadDamages * b;
        result.increaseDamageRate = a.increaseDamageRate * b;
        result.reduceReceiveDamageRate = a.reduceReceiveDamageRate * b;
        return result;
    }

}
