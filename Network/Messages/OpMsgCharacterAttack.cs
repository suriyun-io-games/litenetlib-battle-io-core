﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class OpMsgCharacterAttack : BaseOpMsg
{
    public override short OpId
    {
        get
        {
            return 11001;
        }
    }

    public int weaponId;
    public byte actionId;
    public Vector3 direction;
    public NetworkInstanceId attackerNetId;
    public float addRotationX;
    public float addRotationY;
}
