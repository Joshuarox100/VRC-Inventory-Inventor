using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace InventoryInventor.Preset
{
    [Serializable]
    public class Page : ScriptableObject
    {
        public readonly string ID = IdGenerator.Generate();
        
        public List<PageItem> Items { get { return m_Items; } set { m_Items = value; } }
        [SerializeField]
        private List<PageItem> m_Items = new List<PageItem>();

        public Texture2D Icon { get { return m_Icon; } set { m_Icon = value; } }
        [SerializeField]
        private Texture2D m_Icon;

        //Returns array of item names
        public string[] GetNames() => Items.Select((item) => item.name).ToArray();

        //Returns array of item clips
        public AnimationClip[] GetClips() => Items.Select((item) => item.EnableClip).ToArray();
    }
}
