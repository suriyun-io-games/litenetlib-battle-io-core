﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AttackAnimation
{
    [Range(0, 254)]
    public byte actionId;
    public float animationDuration;
    public float launchDuration;
    public float speed = 1f;
    public bool isAnimationForLeftHandWeapon;
    [Tooltip("If this is empty it will launch `Damage Entity` from `Weapon Data`")]
    public DamageEntity damagePrefab;
}
