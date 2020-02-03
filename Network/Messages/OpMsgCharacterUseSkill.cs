using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class OpMsgCharacterUseSkill : BaseOpMsg
{
    public override short OpId
    {
        get
        {
            return 11002;
        }
    }

    public int skillId;
    public Vector3 direction;
    public NetworkInstanceId attackerNetId;
    public float addRotationX;
    public float addRotationY;
}
