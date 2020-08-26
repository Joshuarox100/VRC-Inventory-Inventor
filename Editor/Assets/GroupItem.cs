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
        DisableOnDisable = 1,
        DisableOnEnable = 2,
        Toggle = 3,
        EnableOnDisable = 4,
        EnableOnEnable = 5,
        AlwaysEnable = 6
    };

    //Constructors
    public GroupItem()
    {
        Item = null;
        Reaction = GroupType.Toggle;
    }
}