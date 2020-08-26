using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Preset", menuName = "Inventory Inventor/Preset", order = 1)]
public class InventoryPreset : ScriptableObject
{
    public string MenuName { get { return m_MenuName; } set { m_MenuName = value; } }
    [SerializeField]
    private string m_MenuName = "New Inventory Preset";
    
    public List<Page> Pages { get { return m_Pages; } set { m_Pages = value; } }
    [SerializeField]
    private List<Page> m_Pages = new List<Page>();

    public List<Page> ExtraPages { get { return m_ExtraPages; } set { m_ExtraPages = value; } }
    [SerializeField]
    private List<Page> m_ExtraPages = new List<Page>();

    public Texture2D Icon { get { return m_Icon; } set { m_Icon = value; } }
    [SerializeField]
    private Texture2D m_Icon;
}