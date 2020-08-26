using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

[Serializable]
public class Page : ScriptableObject
{
    public PageType Type { get { return m_Type; } set { m_Type = value; } }
    [SerializeField]
    private PageType m_Type;

    public List<PageItem> Items { get { return m_Items; } set { m_Items = value; } }
    [SerializeField]
    private List<PageItem> m_Items;

    public VRCExpressionsMenu Submenu { get { return m_Submenu; } set { m_Submenu = value; } }
    [SerializeField]
    private VRCExpressionsMenu m_Submenu;

    public Texture2D Icon { get { return m_Icon; } set { m_Icon = value; } }
    [SerializeField]
    private Texture2D m_Icon;

    public enum PageType
    {
        Inventory = 0,
        Submenu = 1,
    };

    //Constructors
    public Page()
    {
        Type = PageType.Inventory;
        Items = new List<PageItem>();
    }

    //Returns array of item names
    public string[] GetNames()
    {
        List<string> names = new List<string>();
        foreach (PageItem item in Items)
        {
            names.Add(item.name);
        }
        return names.ToArray();
    }

    //Returns array of item clips
    public AnimationClip[] GetClips()
    {
        List<AnimationClip> clips = new List<AnimationClip>();
        foreach (PageItem item in Items)
        {
            clips.Add(item.Clip);
        }
        return clips.ToArray();
    }
}