using System;
using UnityEngine;

namespace InventoryInventor.Preset
{
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
            Disable = 0,
            Enable = 1
        };

        //Constructors
        public GroupItem()
        {
            Item = null;
            Reaction = GroupType.Enable;
        }
    }
}
