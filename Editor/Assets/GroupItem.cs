using System;
using UnityEngine;

[Serializable]
public class GroupItem : ScriptableObject
{
    public PageItem Item { get { return m_Item; } set { m_Item = value; } }
    [SerializeField]
    private PageItem m_Item;

    public GroupType Reaction { get { return m_Reaction; } set { m_Reaction = value; } }
    [SerializeField]
    private GroupType m_Reaction;

    public enum GroupType
    {
        AlwaysDisable = 0,
        DisableIfEnabled = 1,
        Toggle = 2,
        EnableIfDisabled = 3,
        AlwaysEnable = 4
    };

    //Constructors
    public GroupItem()
    {
        Item = null;
        Reaction = GroupType.Toggle;
    }
}