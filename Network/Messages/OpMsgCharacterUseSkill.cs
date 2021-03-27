using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib.Utils;

public class OpMsgCharacterUseSkill : BaseOpMsg
{
    public override ushort OpId
    {
        get
        {
            return 11002;
        }
    }

    public int skillId;
    public Vector3 targetPosition;
    public uint attackerNetId;
    public float addRotationX;
    public float addRotationY;

    public override void Deserialize(NetDataReader reader)
    {
        skillId = reader.GetInt();
        targetPosition = reader.GetVector3();
        attackerNetId = reader.GetPackedUInt();
        addRotationX = reader.GetFloat();
        addRotationY = reader.GetFloat();
    }

    public override void Serialize(NetDataWriter writer)
    {
        writer.Put(skillId);
        writer.PutVector3(targetPosition);
        writer.PutPackedUInt(attackerNetId);
        writer.Put(addRotationX);
        writer.Put(addRotationY);
    }
}
