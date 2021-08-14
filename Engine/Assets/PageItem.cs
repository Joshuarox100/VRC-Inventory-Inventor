using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace InventoryInventor.Preset
{
    [Serializable]
    public class PageItem : ScriptableObject
    {
        /// <summary>
        /// Unique identifier to be used in names.
        /// </summary>
        public string ID => m_ID;
        [SerializeField]
        private string m_ID = IdGenerator.Generate();
        
        public ItemType Type { get => m_Type; set => m_Type = value; }
        [SerializeField]
        private ItemType m_Type = ItemType.Toggle;

        public SyncMode Sync { get => m_Sync; set => m_Sync = value; }
        [SerializeField]
        private SyncMode m_Sync = SyncMode.Auto;

        public bool Saved { get => m_Saved; set => m_Saved = value; }
        [SerializeField]
        private bool m_Saved = true;

        public bool InitialState { get => m_InitialState; set => m_InitialState = value; }
        [SerializeField]
        private bool m_InitialState;

        public string ObjectReference { get => m_ObjectReference; set => m_ObjectReference = value; }
        [SerializeField]
        private string m_ObjectReference = "";

        public bool UseAnimations { get => m_UseAnimations; set => m_UseAnimations = value; }
        [SerializeField]
        private bool m_UseAnimations;

        public bool TransitionType { get => m_TransitionType; set => m_TransitionType = value; }
        [SerializeField]
        private bool m_TransitionType = true;

        public float TransitionDuration { get => m_TransitionDuration; set => m_TransitionDuration = value; }
        [SerializeField]
        private float m_TransitionDuration;

        public bool TransitionOffset { get => m_TransitionOffset; set => m_TransitionOffset = value; }
        [SerializeField]
        private bool m_TransitionOffset;

        public AnimationClip EnableClip { get => m_EnableClip; set => m_EnableClip = value; }
        [SerializeField]
        private AnimationClip m_EnableClip;

        public AnimationClip DisableClip { get => m_DisableClip; set => m_DisableClip = value; }
        [SerializeField]
        private AnimationClip m_DisableClip;

        public Page PageReference { get => m_PageReference; set => m_PageReference = value; }
        [SerializeField]
        private Page m_PageReference;

        public GroupItem[] EnableGroup { get => m_EnableGroup; set => m_EnableGroup = value; }
        [SerializeField]
        private GroupItem[] m_EnableGroup = Array.Empty<GroupItem>();

        public GroupItem[] DisableGroup { get => m_DisableGroup; set => m_DisableGroup = value; }
        [SerializeField]
        private GroupItem[] m_DisableGroup = Array.Empty<GroupItem>();

        public GroupItem[] ButtonGroup { get => m_ButtonGroup; set => m_ButtonGroup = value; }
        [SerializeField]
        private GroupItem[] m_ButtonGroup = Array.Empty<GroupItem>();

        public VRCExpressionsMenu.Control Control { get => m_Control; set => m_Control = value; }
        [SerializeField]
        private VRCExpressionsMenu.Control m_Control = new VRCExpressionsMenu.Control();

        public Texture2D Icon { get { return m_Icon; } set { m_Icon = value; } }
        [SerializeField]
        private Texture2D m_Icon;

        public enum ItemType
        {
            Toggle = 0,
            Subpage = 1,
            Control = 2,
            Button = 3,
        }

        public enum SyncMode
        {
            Off = 0,
            Manual = 1,
            Auto = 2,
        }

        private string _generatedForName;
        private string _normalizedName;
        public string NormalizedName
        {
            get
            {
                if (_normalizedName == null || _generatedForName != name)
                {
                    var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
                    var invalidReStr = $@"[\s{invalidChars}]+";
                    var sanitizedName = Regex.Replace(name, invalidReStr, "_");
                    _normalizedName = $"{sanitizedName}_{ID}";
                    _generatedForName = name;
                }
                
                return _normalizedName;
            }
        }

        public PageItem[] GetEnableGroupItems() => EnableGroup.Select((item => item.Item)).ToArray();

        public PageItem[] GetDisableGroupItems() => DisableGroup.Select((item => item.Item)).ToArray();
        
        public PageItem[] GetButtonGroupItems() => ButtonGroup.Select((item => item.Item)).ToArray();

        public VRCExpressionParameters.Parameter[] ExpressionParameters
        {
            get
            {
                if (Type == ItemType.Toggle && Sync != SyncMode.Manual)
                {
                    return new[]
                    {
                        new VRCExpressionParameters.Parameter
                        {
                            name = $@"Inventory {NormalizedName}",
                            valueType = VRCExpressionParameters.ValueType.Bool,
                            defaultValue = InitialState ? 1 : 0,
                            saved = true,
                        },
                    };
                }
                
                return Array.Empty<VRCExpressionParameters.Parameter>();
            }
        }

        public AnimatorControllerParameter[] AnimatorParameters
        {
            get
            {
                return new[]
                {
                    new AnimatorControllerParameter
                    {
                        name = $@"Inventory {NormalizedName}",
                        type = AnimatorControllerParameterType.Bool,
                        defaultBool = InitialState,
                    }
                };
            }
        }
        
        public int RequiredStates
        {
            get
            {
                switch (Type)
                {
                    case ItemType.Button:
                        return 1;
                    case ItemType.Toggle:
                        var totalUsage = 0;

                        if (Sync != SyncMode.Manual)
                        {
                            if (EnableGroup.Length > 0)
                                totalUsage++;
                            
                            if (DisableGroup.Length > 0) 
                                totalUsage++;
                        }
                        
                        switch (Sync)
                        {
                            case SyncMode.Off:
                                totalUsage += 1;
                                break;
                            case SyncMode.Manual:
                                totalUsage += 3;
                                break;
                            case SyncMode.Auto:
                                totalUsage += Saved ? 1 : 3;
                                break;
                        }

                        return totalUsage;
                    default:
                        return 0;
                }
            }
        }
        
        protected bool CheckAnimationClipCompatibility(AnimationClip clip)
        {
            if (clip == null)
            {
                return true;
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type == typeof(Transform) || binding.type == typeof(Animator))
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsEnableClipCompatible
        {
            get => CheckAnimationClipCompatibility(EnableClip);
        }

        public bool IsDisableClipCompatible
        {
            get => CheckAnimationClipCompatibility(DisableClip);
        }

        public VRCExpressionsMenu.Control MakeControl(StateSequence stateSequence)
        {
            switch (Type)
            {
                case ItemType.Toggle:
                    return new VRCExpressionsMenu.Control
                    {
                        name = name,
                        icon = Icon,
                        type = VRCExpressionsMenu.Control.ControlType.Toggle,
                        parameter = new VRCExpressionsMenu.Control.Parameter() { name = "Inventory" },
                        value = stateSequence.Next(),
                    };
                case ItemType.Subpage:
                    return new VRCExpressionsMenu.Control
                    {
                        name = name,
                        icon = PageReference != null ? PageReference.Icon : null,
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = null, // ToDo: somehow contextually be able to retrieve the right instance of a control
                    };
                case ItemType.Control:
                    return new VRCExpressionsMenu.Control
                    {
                        name = Control.name,
                        icon = Control.icon,
                        type = Control.type,
                        parameter = Control.parameter,
                        value = Control.value,
                        style = Control.style,
                        labels = Control.labels,
                        subMenu = Control.subMenu,
                        subParameters = Control.subParameters,
                    };
                case ItemType.Button:
                    return new VRCExpressionsMenu.Control
                    {
                        name = name, 
                        icon = Icon,
                        type = VRCExpressionsMenu.Control.ControlType.Button,
                        parameter = new VRCExpressionsMenu.Control.Parameter() { name = "Inventory" },
                        value = stateSequence.Next(),
                    };
                default:
                    return null;
            }
        }
    }
}
