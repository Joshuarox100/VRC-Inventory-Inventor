using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Preset", menuName = "Inventory Inventor/Preset", order = 1)]
public class InventoryPreset : ScriptableObject
{   
    public int Version { get { return m_Version; } set { m_Version = value; } }
    [SerializeField]
    private int m_Version;

    public List<Page> Pages { get { return m_Pages; } set { m_Pages = value; } }
    [SerializeField]
    private List<Page> m_Pages = new List<Page>();
}