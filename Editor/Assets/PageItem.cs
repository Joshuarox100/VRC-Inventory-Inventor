using System;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

[Serializable]
public class PageItem : ScriptableObject
{
    public ItemType Type { get { return m_Type; } set { m_Type = value; } }
    [SerializeField]
    private ItemType m_Type;

    public SyncMode Sync { get { return m_Sync; } set { m_Sync = value; } }
    [SerializeField]
    private SyncMode m_Sync;

    public bool InitialState { get { return m_InitialState; } set { m_InitialState = value; } }
    [SerializeField]
    private bool m_InitialState;

    public AnimationClip EnableClip { get { return m_EnableClip; } set { m_EnableClip = value; } }
    [SerializeField]
    private AnimationClip m_EnableClip;

    public AnimationClip DisableClip { get { return m_DisableClip; } set { m_DisableClip = value; } }
    [SerializeField]
    private AnimationClip m_DisableClip;

    public Page PageReference { get { return m_PageReference; } set { m_PageReference = value; } }
    [SerializeField]
    private Page m_PageReference;

    public GroupItem[] EnableGroup { get { return m_EnableGroup; } set { m_EnableGroup = value; } }
    [SerializeField]
    private GroupItem[] m_EnableGroup;

    public GroupItem[] DisableGroup { get { return m_DisableGroup; } set { m_DisableGroup = value; } }
    [SerializeField]
    private GroupItem[] m_DisableGroup;

    public VRCExpressionsMenu Submenu { get { return m_Submenu; } set { m_Submenu = value; } }
    [SerializeField]
    private VRCExpressionsMenu m_Submenu;

    public Texture2D Icon { get { return m_Icon; } set { m_Icon = value; } }
    [SerializeField]
    private Texture2D m_Icon;

    public enum ItemType
    {
        Toggle = 0,
        Page = 1,
        Submenu = 2
    };

    public enum SyncMode
    {
        Off = 0,
        Manual = 1,
        Auto = 2
    }

    //Default Constructor
    public PageItem()
    {
        Type = ItemType.Toggle;
        InitialState = false;
        EnableClip = null;
        DisableClip = null;
        Sync = SyncMode.Auto;
        EnableGroup = new GroupItem[0];
        DisableGroup = new GroupItem[0];
    }

    public PageItem[] GetEnableGroupItems()
    {
        PageItem[] items = new PageItem[EnableGroup.Length];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = EnableGroup[i].Item;
        }
        return items;
    }
    public PageItem[] GetDisableGroupItems()
    {
        PageItem[] items = new PageItem[DisableGroup.Length];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = DisableGroup[i].Item;
        }
        return items;
    }
}
