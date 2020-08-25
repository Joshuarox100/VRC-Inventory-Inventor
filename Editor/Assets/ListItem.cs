using System;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

[Serializable]
public class ListItem : ScriptableObject
{
    public ItemType Type { get { return m_Type; } set { m_Type = value; } }
    [SerializeField]
    private ItemType m_Type;

    public AnimationClip Clip { get { return m_Clip; } set { m_Clip = value; } }
    [SerializeField]
    private AnimationClip m_Clip;

    public Page PageReference { get { return m_PageReference; } set { m_PageReference = value; } }
    [SerializeField]
    private Page m_PageReference;

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
    public ListItem()
    {
        Type = ItemType.Toggle;
        Clip = null;
    }
}
