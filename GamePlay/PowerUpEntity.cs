﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;

public class PowerUpEntity : LiteNetLibBehaviour
{
    public const float DestroyDelay = 1f;
    // We're going to respawn this power up so I decide to keep its prefab name to spawning when character triggered
    [HideInInspector]
    public string prefabName;
    [Header("Recovery / Currencies")]
    public int hp;
    public int exp;
    public WeaponData changingWeapon;
    public InGameCurrency[] currencies;
    [Header("Effect")]
    public EffectEntity powerUpEffect;

    private bool isDead;

    private void Awake()
    {
        gameObject.layer = GenericUtils.IgnoreRaycastLayer;
        var collider = GetComponent<Collider>();
        collider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead)
            return;

        if (other.gameObject.layer == GenericUtils.IgnoreRaycastLayer)
            return;

        var character = other.GetComponent<CharacterEntity>();
        if (character != null && character.Hp > 0)
        {
            isDead = true;
            if (!character.IsHidding)
                EffectEntity.PlayEffect(powerUpEffect, character.effectTransform);
            if (IsServer)
            {
                character.Hp += Mathf.CeilToInt(hp * character.TotalHpRecoveryRate);
                character.Exp += Mathf.CeilToInt(exp * character.TotalExpRate);
                if (changingWeapon != null)
                    character.ChangeWeapon(changingWeapon);
            }
            if (character.IsOwnerClient && !(character is BotEntity) && !(character is MonsterEntity))
            {
                foreach (var currency in currencies)
                {
                    MonetizationManager.Save.AddCurrency(currency.id, currency.amount);
                }
            }
            StartCoroutine(DestroyRoutine());
        }
    }

    IEnumerator DestroyRoutine()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = false;
        }
        yield return new WaitForSeconds(DestroyDelay);
        // Destroy this on all clients
        if (IsServer)
        {
            NetworkDestroy();
            GameplayManager.Singleton.SpawnPowerUp(prefabName);
        }
    }
}
