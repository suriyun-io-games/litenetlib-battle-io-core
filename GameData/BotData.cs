using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BotData
{
    public string name;
    public int headDataIndex;
    public int characterDataIndex;
    public int weaponDataIndex;

    public string GetSelectHead()
    {
        var headKeys = new List<string>(GameInstance.Heads.Keys);
        return headDataIndex < 0 || headDataIndex > headKeys.Count ? headKeys[Random.Range(0, headKeys.Count)] : headKeys[headDataIndex];
    }

    public string GetSelectCharacter()
    {
        var characterKeys = new List<string>(GameInstance.Characters.Keys);
        return characterDataIndex < 0 || characterDataIndex > characterKeys.Count ? characterKeys[Random.Range(0, characterKeys.Count)] : characterKeys[characterDataIndex];
    }

    public string GetSelectWeapon()
    {
        var weaponKeys = new List<string>(GameInstance.Weapons.Keys);
        return weaponDataIndex < 0 || weaponDataIndex > weaponKeys.Count ? weaponKeys[Random.Range(0, weaponKeys.Count)] : weaponKeys[weaponDataIndex];
    }
}
