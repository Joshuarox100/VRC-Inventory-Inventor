using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

[Serializable]
public class Page : ScriptableObject
{
    public List<PageItem> Items { get { return m_Items; } set { m_Items = value; } }
    [SerializeField]
    private List<PageItem> m_Items;

    public Texture2D Icon { get { return m_Icon; } set { m_Icon = value; } }
    [SerializeField]
    private Texture2D m_Icon;

    //Constructors
    public Page()
    {
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
            clips.Add(item.EnableClip);
        }
        return clips.ToArray();
    }
}