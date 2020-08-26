using Boo.Lang;
using System;
using System.Text.RegularExpressions;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

[Serializable]
public class PageItem : ScriptableObject
{
    public ItemType Type { get { return m_Type; } set { m_Type = value; } }
    [SerializeField]
    private ItemType m_Type;

    public int Sync { get { return m_Sync; } set { m_Sync = value; } }
    [SerializeField]
    private int m_Sync;

    public AnimationClip Clip { get { return m_Clip; } set { m_Clip = value; } }
    [SerializeField]
    private AnimationClip m_Clip;

    public Page PageReference { get { return m_PageReference; } set { m_PageReference = value; } }
    [SerializeField]
    private Page m_PageReference;

    public GroupItem[] Group { get { return m_Group; } set { m_Group = value; } }
    [SerializeField]
    private GroupItem[] m_Group;

    public VRCExpressionsMenu Submenu { get { return m_Submenu; } set { m_Submenu = value; } }
    [SerializeField]
    private VRCExpressionsMenu m_Submenu;

    public Texture2D Icon { get { return m_Icon; } set { m_Icon = value; } }
    [SerializeField]
    private Texture2D m_Icon;

    public enum ItemType
    {
        Toggle = 0,
        Inventory = 1,
        Submenu = 2
    };

    //Default Constructor
    public PageItem()
    {
        Type = ItemType.Toggle;
        Clip = null;
        Sync = 2;
        Group = new GroupItem[0];
    }

    public PageItem[] GetGroupItems()
    {
        PageItem[] items = new PageItem[Group.Length];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = Group[i].Item;
        }
        return items;
    }
}
