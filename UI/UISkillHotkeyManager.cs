using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UISkillHotkeyManager : MonoBehaviour
{
    public UISkillHotkey[] hotkeys;
    public RectTransform cancelArea;
    private bool cancelButtonDown;
    private bool isAnyDragging;
    private Vector2 localMousePosition;

    private void Update()
    {
        isAnyDragging = false;
        foreach (var hotkey in hotkeys)
        {
            if (hotkey.IsDragging)
            {
                isAnyDragging = true;
                if (!cancelButtonDown)
                {
                    localMousePosition = cancelArea.InverseTransformPoint(hotkey.CurrentPosition);
                    if (cancelArea.rect.Contains(localMousePosition))
                    {
                        InputManager.SetButtonDown("CancelUsingSkill");
                        cancelButtonDown = true;
                    }
                }
                break;
            }
        }

        if (cancelArea != null)
            cancelArea.gameObject.SetActive(isAnyDragging);
    }

    private void LateUpdate()
    {

        if (cancelButtonDown && !isAnyDragging)
        {
            InputManager.SetButtonUp("CancelUsingSkill");
            cancelButtonDown = false;
        }
    }
}
