using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(MobileMovementJoystick))]
public class UISkillHotkey : MonoBehaviour
{
    [Range(0, 7)]
    public sbyte hotkeyId;
    public Image iconImage;
    public Image coolDownGage;
    public Sprite emptySprite;
    private MobileMovementJoystick joyStick;

    public bool IsDragging
    {
        get { return joyStick.IsDragging; }
    }

    public Vector2 CurrentPosition
    {
        get { return joyStick.CurrentPosition; }
    }

    private void Awake()
    {
        joyStick = GetComponent<MobileMovementJoystick>();
        joyStick.axisXName = "Skill X " + hotkeyId;
        joyStick.axisYName = "Skill Y " + hotkeyId;
    }

    private void Update()
    {
        var localCharacter = BaseNetworkGameCharacter.Local as CharacterEntity;
        SkillData skill;
        if (localCharacter == null || !localCharacter.Skills.TryGetValue(hotkeyId, out skill))
        {
            if (iconImage != null)
            {
                iconImage.sprite = emptySprite;
            }
            if (coolDownGage != null)
            {
                coolDownGage.fillAmount = 0;
            }
            joyStick.Interactable = false;
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = skill.icon;
        }
        if (coolDownGage != null)
        {
            coolDownGage.raycastTarget = false;
            coolDownGage.fillAmount = 1 - (localCharacter.GetSkillCoolDownCount(hotkeyId) / skill.coolDown);
        }

        joyStick.Interactable = localCharacter.GetSkillCoolDownCount(hotkeyId) >= skill.coolDown;
    }
}
