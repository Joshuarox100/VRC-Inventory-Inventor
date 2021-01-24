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

    public bool Saved { get { return m_Saved; } set { m_Saved = value; } }
    [SerializeField]
    private bool m_Saved;

    public bool InitialState { get { return m_InitialState; } set { m_InitialState = value; } }
    [SerializeField]
    private bool m_InitialState;

    public string ObjectReference { get { return m_ObjectReference; } set { m_ObjectReference = value; } }
    [SerializeField]
    private string m_ObjectReference;

    public bool UseAnimations { get { return m_UseAnimations; } set { m_UseAnimations = value; } }
    [SerializeField]
    private bool m_UseAnimations;

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

    public GroupItem[] ButtonGroup { get { return m_ButtonGroup; } set { m_ButtonGroup = value; } }
    [SerializeField]
    private GroupItem[] m_ButtonGroup;

    public VRCExpressionsMenu.Control Control { get { return m_Control; } set { m_Control = value; } }
    [SerializeField]
    private VRCExpressionsMenu.Control m_Control;

    public Texture2D Icon { get { return m_Icon; } set { m_Icon = value; } }
    [SerializeField]
    private Texture2D m_Icon;

    public enum ItemType
    {
        Toggle = 0,
        Subpage = 1,
        Control = 2,
        Button = 3,
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
        ObjectReference = "";
        UseAnimations = false;
        EnableClip = null;
        DisableClip = null;
        Sync = SyncMode.Auto;
        Saved = true;
        EnableGroup = new GroupItem[0];
        DisableGroup = new GroupItem[0];
        ButtonGroup = new GroupItem[0];
        Control = new VRCExpressionsMenu.Control();
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
    public PageItem[] GetButtonGroupItems()
    {
        PageItem[] items = new PageItem[ButtonGroup.Length];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = ButtonGroup[i].Item;
        }
        return items;
    }
}
