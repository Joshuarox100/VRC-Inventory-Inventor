#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace InventoryInventor.Preset 
{
    public class InventoryPresetUtility
    {
        // Version tracker used for upgrading old presets.
        public static readonly int currentVersion = 3;
        private static object pageContentsDict;

        // Sends a item to a designated page
        public static void SendItemToPage(object args)
        {
            // Mark Preset as dirty
            EditorUtility.SetDirty((InventoryPreset)((object[])args)[3]);

            // Cast arguments to correct types
            PageItem item = ((object[])args)[0] as PageItem;
            Page oldPage = ((object[])args)[1] as Page;
            Page newPage = ((object[])args)[2] as Page;

            // Record the change
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            // Add item to new page
            Undo.RecordObject(newPage, "Move Item");
            newPage.Items.Add(item);

            // Remove item from old page
            Undo.RecordObject(oldPage, "Move Item");
            oldPage.Items.Remove(item);

            // Save Undo operation
            Undo.CollapseUndoOperations(group);
        }

        // Copies the settings of a page to the system buffer
        public static void CopyPageSettings(object args)
        {
            Page page = ((object[])args)[0] as Page;

            /*
             * Format:
             * IDENTIFIER
             * TYPE
             * PAGE NAME
             * ICON PATH
             */

            StringBuilder pageEncode = new StringBuilder();
            pageEncode.AppendLine("INVENTORY INVENTOR ENCODE");
            pageEncode.AppendLine("PAGE");
            pageEncode.AppendLine(page.name);
            pageEncode.AppendLine((page.Icon != null) ? AssetDatabase.GetAssetPath(page.Icon.GetInstanceID()) : "NULL");
            //Debug.Log(pageEncode.ToString());
            EditorGUIUtility.systemCopyBuffer = pageEncode.ToString();
        }

        // Pastes the settings of a page from the system buffer
        public static void PastePageSettings(object args)
        {
            string[] settings = EditorGUIUtility.systemCopyBuffer.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (settings[0] != "INVENTORY INVENTOR ENCODE" || settings[1] != "PAGE")
                return;

            Page page = ((object[])args)[0] as Page;

            // Mark Preset as dirty
            EditorUtility.SetDirty((InventoryPreset)((object[])args)[1]);

            // Record the change
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            // Paste page settings
            Undo.RecordObject(page, "Pasted Settings");
            page.name = settings[2];
            if (settings[3] != "NULL")
                page.Icon = (Texture2D)AssetDatabase.LoadAssetAtPath(settings[3], typeof(Texture2D));

            // Save Undo operation
            Undo.CollapseUndoOperations(group);
        }

        // Duplicates a page
        public static void DuplicatePage(object args)
        {
            InventoryPreset preset = (InventoryPreset)((object[])args)[1];
            Page oldPage = ((object[])args)[0] as Page;

            // Mark the preset as dirty and record the creation of a new page.
            EditorUtility.SetDirty(preset);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Page page = ScriptableObject.CreateInstance<Page>();
            Undo.RegisterCreatedObjectUndo(page, "Duplicate Page");

            // Configure the page and add it to the Asset.
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            page.hideFlags = HideFlags.HideInHierarchy;
            page.name = oldPage.name;
            AssetDatabase.AddObjectToAsset(page, _path);

            foreach (PageItem i in oldPage.Items)
            {
                // Copy an item to the page.
                PageItem item = ScriptableObject.CreateInstance<PageItem>();
                Undo.RegisterCreatedObjectUndo(item, "Duplicate Page");

                // Configure the new item and add it to the Asset.
                item.hideFlags = HideFlags.HideInHierarchy;
                item.name = i.name;
                item.Icon = i.Icon;
                item.Type = i.Type;
                item.UseAnimations = i.UseAnimations;
                item.EnableClip = i.EnableClip;
                item.DisableClip = i.DisableClip;
                item.TransitionType = i.TransitionType;
                item.TransitionDuration = i.TransitionDuration;
                item.ObjectReference = i.ObjectReference;
                item.InitialState = i.InitialState;
                item.Sync = i.Sync;
                item.Saved = i.Saved;
                item.EnableGroup = new GroupItem[i.EnableGroup.Length];
                for (int j = 0; j < item.EnableGroup.Length; j++)
                {
                    GroupItem item2 = ScriptableObject.CreateInstance<GroupItem>();
                    Undo.RegisterCreatedObjectUndo(item2, "Duplicate Page");

                    // Configure and add the new item to the array.
                    item2.name = i.EnableGroup[j].name;
                    item2.hideFlags = HideFlags.HideInHierarchy;
                    item2.Reaction = i.EnableGroup[j].Reaction;
                    item2.Item = null;
                    foreach (Page page1 in preset.Pages)
                        if (item2.Item == null)
                        {
                            foreach (PageItem item3 in page1.Items)
                                if (item3 == i.EnableGroup[j].Item)
                                {
                                    item2.Item = i.EnableGroup[j].Item;
                                    break;
                                }
                        }
                        else
                            break;
                    string _path2 = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                    AssetDatabase.AddObjectToAsset(item2, _path2);
                    Undo.RecordObject(item, "Duplicate Page");
                    item.EnableGroup[j] = item2;
                }
                item.DisableGroup = new GroupItem[i.DisableGroup.Length];
                for (int j = 0; j < item.DisableGroup.Length; j++)
                {
                    GroupItem item2 = ScriptableObject.CreateInstance<GroupItem>();
                    Undo.RegisterCreatedObjectUndo(item2, "Duplicate Page");

                    // Configure and add the new item to the array.
                    item2.name = i.DisableGroup[j].name;
                    item2.hideFlags = HideFlags.HideInHierarchy;
                    item2.Reaction = i.DisableGroup[j].Reaction;
                    item2.Item = null;
                    foreach (Page page1 in preset.Pages)
                        if (item2.Item == null)
                        {
                            foreach (PageItem item3 in page1.Items)
                                if (item3 == i.DisableGroup[j].Item)
                                {
                                    item2.Item = i.DisableGroup[j].Item;
                                    break;
                                }
                        }
                        else
                            break;
                    string _path2 = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                    AssetDatabase.AddObjectToAsset(item2, _path2);
                    Undo.RecordObject(item, "Duplicate Page");
                    item.DisableGroup[j] = item2;
                }
                item.ButtonGroup = new GroupItem[i.ButtonGroup.Length];
                for (int j = 0; j < item.ButtonGroup.Length; j++)
                {
                    GroupItem item2 = ScriptableObject.CreateInstance<GroupItem>();
                    Undo.RegisterCreatedObjectUndo(item2, "Duplicate Page");

                    // Configure and add the new item to the array.
                    item2.name = i.ButtonGroup[j].name;
                    item2.hideFlags = HideFlags.HideInHierarchy;
                    item2.Reaction = i.ButtonGroup[j].Reaction;
                    item2.Item = null;
                    foreach (Page page1 in preset.Pages)
                        if (item2.Item == null)
                        {
                            foreach (PageItem item3 in page1.Items)
                                if (item3 == i.ButtonGroup[j].Item)
                                {
                                    item2.Item = i.ButtonGroup[j].Item;
                                    break;
                                }
                        }
                        else
                            break;
                    string _path2 = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                    AssetDatabase.AddObjectToAsset(item2, _path2);
                    Undo.RecordObject(item, "Duplicate Page");
                    item.ButtonGroup[j] = item2;
                }
                item.PageReference = (i.PageReference != null && preset.Pages.Contains(i.PageReference)) ? i.PageReference : null;
                item.Control = new VRCExpressionsMenu.Control()
                {
                    name = i.Control.name,
                    icon = i.Control.icon,
                    labels = (VRCExpressionsMenu.Control.Label[])i.Control.labels.Clone(),
                    parameter = i.Control.parameter,
                    style = i.Control.style,
                    subMenu = i.Control.subMenu,
                    subParameters = (VRCExpressionsMenu.Control.Parameter[])i.Control.subParameters.Clone(),
                    type = i.Control.type,
                    value = i.Control.value
                };
                AssetDatabase.AddObjectToAsset(item, _path);
                page.Items.Add(item);
            }

            // Record the current state of the preset and add the new page.
            Undo.RecordObject(preset, "Duplicate Page");
            preset.Pages.Add(page);
            Undo.CollapseUndoOperations(group);
        }

        // Copies the settings of an item to the system buffer
        public static void CopyItemSettings(object args)
        {
            PageItem item = ((object[])args)[0] as PageItem;
            InventoryPreset preset = ((object[])args)[1] as InventoryPreset;

            /*
             * Format:
             * IDENTIFIER
             * TYPE
             * ITEM NAME
             * ICON PATH
             * ITEM TYPE
             * USE ANIMATIONS
             * ENABLE CLIP PATH
             * DISABLE CLIP PATH
             * TRANSITION TYPE
             * TRANSITION DURATION
             * OBJECT REFERENCE
             * INITIAL STATE
             * SYNC
             * SAVED
             * ENABLE GROUP LENGTH
             * ENABLE GROUP ITEMS (PAGE | ITEM | REACTION)
             * DISABLE GROUP LENGTH
             * DISABLE GROUP ITEMS (PAGE | ITEM | REACTION)
             * BUTTON GROUP LENGTH
             * BUTTON GROUP ITEMS (PAGE | ITEM | REACTION)
             * PAGE REFERENCE
             * CONTROL NAME
             * CONTROL ICON
             * CONTROL LABELS LENGTH
             * CONTROL LABELS (NAME | ICON PATH)
             * CONTROL PARAMETER NAME
             * CONTROL STYLE
             * CONTROL SUBMENU
             * CONTROL SUBPARAMETERS LENGTH
             * CONTROL SUBPARAMETERS
             * CONTROL TYPE
             * CONTROL VALUE
             */

            StringBuilder itemEncode = new StringBuilder();
            itemEncode.AppendLine("INVENTORY INVENTOR ENCODE");
            itemEncode.AppendLine("ITEM");
            itemEncode.AppendLine(item.name);
            itemEncode.AppendLine((item.Icon != null) ? AssetDatabase.GetAssetPath(item.Icon.GetInstanceID()) : "NULL");
            switch (item.Type)
            {
                case PageItem.ItemType.Toggle:
                    itemEncode.AppendLine("TOGGLE");
                    break;
                case PageItem.ItemType.Button:
                    itemEncode.AppendLine("BUTTON");
                    break;
                case PageItem.ItemType.Subpage:
                    itemEncode.AppendLine("SUBPAGE");
                    break;
                case PageItem.ItemType.Control:
                    itemEncode.AppendLine("CONTROL");
                    break;
            }
            itemEncode.AppendLine(item.UseAnimations ? "TRUE" : "FALSE");
            itemEncode.AppendLine((item.EnableClip != null) ? AssetDatabase.GetAssetPath(item.EnableClip.GetInstanceID()) : "NULL");
            itemEncode.AppendLine((item.DisableClip != null) ? AssetDatabase.GetAssetPath(item.DisableClip.GetInstanceID()) : "NULL");
            itemEncode.AppendLine(item.TransitionType ? "TRUE" : "FALSE");
            itemEncode.AppendLine(item.TransitionDuration.ToString());
            itemEncode.AppendLine(item.ObjectReference != null ? item.ObjectReference : "NULL");
            itemEncode.AppendLine(item.InitialState ? "TRUE" : "FALSE");
            switch (item.Sync)
            {
                case PageItem.SyncMode.Auto:
                    itemEncode.AppendLine("AUTO");
                    break;
                case PageItem.SyncMode.Manual:
                    itemEncode.AppendLine("MANUAL");
                    break;
                case PageItem.SyncMode.Off:
                    itemEncode.AppendLine("OFF");
                    break;
            }
            itemEncode.AppendLine(item.Saved ? "TRUE" : "FALSE");
            itemEncode.AppendLine(item.EnableGroup.Length.ToString());
            foreach (GroupItem groupItem in item.EnableGroup)
            {
                if (groupItem.Item != null)
                {
                    bool found = false;
                    foreach (Page page in preset.Pages)
                        if (!found)
                        {
                            foreach (PageItem pageItem in page.Items)
                                if (pageItem == groupItem.Item)
                                {
                                    itemEncode.AppendLine(page.name);
                                    break;
                                }
                        }
                        else
                        {
                            itemEncode.AppendLine("NULL");
                            break;
                        }
                    itemEncode.AppendLine(groupItem.Item.name);
                }
                else
                {
                    itemEncode.AppendLine("NULL");
                    itemEncode.AppendLine("NULL");
                }
                switch (groupItem.Reaction)
                {
                    case GroupItem.GroupType.Enable:
                        itemEncode.AppendLine("ENABLE");
                        break;
                    case GroupItem.GroupType.Disable:
                        itemEncode.AppendLine("DISABLE");
                        break;
                }
            }
            itemEncode.AppendLine(item.DisableGroup.Length.ToString());
            foreach (GroupItem groupItem in item.DisableGroup)
            {
                if (groupItem.Item != null)
                {
                    bool found = false;
                    foreach (Page page in preset.Pages)
                        if (!found)
                        {
                            foreach (PageItem pageItem in page.Items)
                                if (pageItem == groupItem.Item)
                                {
                                    itemEncode.AppendLine(page.name);
                                    break;
                                }
                        }
                        else
                        {
                            itemEncode.AppendLine("NULL");
                            break;
                        }
                    itemEncode.AppendLine(groupItem.Item.name);
                }
                else
                {
                    itemEncode.AppendLine("NULL");
                    itemEncode.AppendLine("NULL");
                }
                switch (groupItem.Reaction)
                {
                    case GroupItem.GroupType.Enable:
                        itemEncode.AppendLine("ENABLE");
                        break;
                    case GroupItem.GroupType.Disable:
                        itemEncode.AppendLine("DISABLE");
                        break;
                }
            }
            itemEncode.AppendLine(item.ButtonGroup.Length.ToString());
            foreach (GroupItem groupItem in item.ButtonGroup)
            {
                if (groupItem.Item != null)
                {
                    bool found = false;
                    foreach (Page page in preset.Pages)
                        if (!found)
                        {
                            foreach (PageItem pageItem in page.Items)
                                if (pageItem == groupItem.Item)
                                {
                                    itemEncode.AppendLine(page.name);
                                    break;
                                }
                        }
                        else
                        {
                            itemEncode.AppendLine("NULL");
                            break;
                        }
                    itemEncode.AppendLine(groupItem.Item.name);
                }
                else
                {
                    itemEncode.AppendLine("NULL");
                    itemEncode.AppendLine("NULL");
                }
                switch (groupItem.Reaction)
                {
                    case GroupItem.GroupType.Enable:
                        itemEncode.AppendLine("ENABLE");
                        break;
                    case GroupItem.GroupType.Disable:
                        itemEncode.AppendLine("DISABLE");
                        break;
                }
            }
            itemEncode.AppendLine(item.PageReference != null ? item.PageReference.name : "NULL");
            itemEncode.AppendLine(item.Control.name);
            itemEncode.AppendLine((item.Control.icon != null) ? AssetDatabase.GetAssetPath(item.Control.icon.GetInstanceID()) : "NULL");
            itemEncode.AppendLine(item.Control.labels.Length.ToString());
            foreach (VRCExpressionsMenu.Control.Label label in item.Control.labels)
            {
                itemEncode.AppendLine(label.name);
                itemEncode.AppendLine((label.icon != null) ? AssetDatabase.GetAssetPath(label.icon.GetInstanceID()) : "NULL");
            }
            itemEncode.AppendLine(item.Control.parameter.name);
            switch (item.Control.style)
            {
                case VRCExpressionsMenu.Control.Style.Style1:
                    itemEncode.AppendLine("STYLE1");
                    break;
                case VRCExpressionsMenu.Control.Style.Style2:
                    itemEncode.AppendLine("STYLE2");
                    break;
                case VRCExpressionsMenu.Control.Style.Style3:
                    itemEncode.AppendLine("STYLE3");
                    break;
                case VRCExpressionsMenu.Control.Style.Style4:
                    itemEncode.AppendLine("STYLE4");
                    break;
            }
            itemEncode.AppendLine((item.Control.subMenu != null) ? AssetDatabase.GetAssetPath(item.Control.subMenu.GetInstanceID()) : "NULL");
            itemEncode.AppendLine(item.Control.subParameters.Length.ToString());
            foreach (VRCExpressionsMenu.Control.Parameter parameter in item.Control.subParameters)
            {
                itemEncode.AppendLine(parameter.name);
            }
            switch (item.Control.type)
            {
                case VRCExpressionsMenu.Control.ControlType.Button:
                    itemEncode.AppendLine("BUTTON");
                    break;
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    itemEncode.AppendLine("TOGGLE");
                    break;
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    itemEncode.AppendLine("SUBMENU");
                    break;
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    itemEncode.AppendLine("RADIAL PUPPET");
                    break;
                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    itemEncode.AppendLine("TWO AXIS PUPPET");
                    break;
                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    itemEncode.AppendLine("FOUR AXIS PUPPET");
                    break;
            }
            itemEncode.AppendLine(item.Control.value.ToString());
            //Debug.Log(itemEncode.ToString());
            EditorGUIUtility.systemCopyBuffer = itemEncode.ToString();
        }

        // Pastes the settings of an item from the system buffer
        public static void PasteItemSettings(object args)
        {
            string[] settings = EditorGUIUtility.systemCopyBuffer.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (settings[0] != "INVENTORY INVENTOR ENCODE" || settings[1] != "ITEM")
                return;

            PageItem item = ((object[])args)[0] as PageItem;
            InventoryPreset preset = ((object[])args)[1] as InventoryPreset;

            // Mark Preset as dirty
            EditorUtility.SetDirty(preset);

            // Record the change
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            // Paste item settings
            Undo.RecordObject(item, "Pasted Settings");
            item.name = settings[2];
            if (settings[3] != "NULL")
                item.Icon = (Texture2D)AssetDatabase.LoadAssetAtPath(settings[3], typeof(Texture2D));
            switch (settings[4])
            {
                case "TOGGLE":
                    item.Type = PageItem.ItemType.Toggle;
                    break;
                case "BUTTON":
                    item.Type = PageItem.ItemType.Button;
                    break;
                case "SUBPAGE":
                    item.Type = PageItem.ItemType.Subpage;
                    break;
                case "CONTROL":
                    item.Type = PageItem.ItemType.Control;
                    break;
            }
            item.UseAnimations = (settings[5] == "TRUE") ? true : false;
            if (settings[6] != "NULL")
                item.EnableClip = (AnimationClip)AssetDatabase.LoadAssetAtPath(settings[6], typeof(AnimationClip));
            if (settings[7] != "NULL")
                item.DisableClip = (AnimationClip)AssetDatabase.LoadAssetAtPath(settings[7], typeof(AnimationClip));
            item.TransitionType = (settings[8] == "TRUE") ? true : false;
            item.TransitionDuration = float.Parse(settings[9]);
            item.ObjectReference = settings[10];
            item.InitialState = (settings[11] == "TRUE") ? true : false;
            switch (settings[12])
            {
                case "AUTO":
                    item.Sync = PageItem.SyncMode.Auto;
                    break;
                case "MANUAL":
                    item.Sync = PageItem.SyncMode.Manual;
                    break;
                case "OFF":
                    item.Sync = PageItem.SyncMode.Off;
                    break;
            }
            item.Saved = (settings[13] == "TRUE") ? true : false;
            GroupItem[] itemGroup = new GroupItem[int.Parse(settings[14])];
            int index = 15;
            for (int i = 0; i < itemGroup.Length; i++)
            {
                GroupItem item2 = ScriptableObject.CreateInstance<GroupItem>();
                Undo.RegisterCreatedObjectUndo(item2, "Pasted Settings");

                // Configure and add the new item to the array.
                if (settings[index] != "NULL")
                    item2.name = settings[index] + ": " + settings[index + 1];
                else
                    item2.name = "None";
                item2.hideFlags = HideFlags.HideInHierarchy;
                switch (settings[index + 2])
                {
                    case "ENABLE":
                        item2.Reaction = GroupItem.GroupType.Enable;
                        break;
                    case "DISABLE":
                        item2.Reaction = GroupItem.GroupType.Disable;
                        break;
                }
                item2.Item = null;
                if (settings[index] != "NULL")
                {
                    foreach (Page page in preset.Pages)
                        if (page.name == settings[index])
                        {
                            foreach (PageItem item3 in page.Items)
                                if (item3.name == settings[index + 1])
                                {
                                    item2.Item = item3;
                                    break;
                                }
                            break;
                        }
                }
                string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                AssetDatabase.AddObjectToAsset(item2, _path);
                itemGroup[i] = item2;
                index += 3;
            }
            item.EnableGroup = itemGroup;
            itemGroup = new GroupItem[int.Parse(settings[index++])];
            for (int i = 0; i < itemGroup.Length; i++)
            {
                GroupItem item2 = ScriptableObject.CreateInstance<GroupItem>();
                Undo.RegisterCreatedObjectUndo(item2, "Pasted Settings");

                // Configure and add the new item to the array.
                if (settings[index] != "NULL")
                    item2.name = settings[index] + ": " + settings[index + 1];
                else
                    item2.name = "None";
                item2.hideFlags = HideFlags.HideInHierarchy;
                switch (settings[index + 2])
                {
                    case "ENABLE":
                        item2.Reaction = GroupItem.GroupType.Enable;
                        break;
                    case "DISABLE":
                        item2.Reaction = GroupItem.GroupType.Disable;
                        break;
                }
                item2.Item = null;
                if (settings[index] != "NULL")
                {
                    foreach (Page page in preset.Pages)
                        if (page.name == settings[index])
                        {
                            foreach (PageItem item3 in page.Items)
                                if (item3.name == settings[index + 1])
                                {
                                    item2.Item = item3;
                                    break;
                                }
                            break;
                        }
                }
                string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                AssetDatabase.AddObjectToAsset(item2, _path);
                itemGroup[i] = item2;
                index += 3;
            }
            item.DisableGroup = itemGroup;
            itemGroup = new GroupItem[int.Parse(settings[index++])];
            for (int i = 0; i < itemGroup.Length; i++)
            {
                GroupItem item2 = ScriptableObject.CreateInstance<GroupItem>();
                Undo.RegisterCreatedObjectUndo(item2, "Pasted Settings");

                // Configure and add the new item to the array.
                if (settings[index] != "NULL")
                    item2.name = settings[index] + ": " + settings[index + 1];
                else
                    item2.name = "None";
                item2.hideFlags = HideFlags.HideInHierarchy;
                switch (settings[index + 2])
                {
                    case "ENABLE":
                        item2.Reaction = GroupItem.GroupType.Enable;
                        break;
                    case "DISABLE":
                        item2.Reaction = GroupItem.GroupType.Disable;
                        break;
                }
                item2.Item = null;
                if (settings[index] != "NULL")
                {
                    foreach (Page page in preset.Pages)
                        if (page.name == settings[index])
                        {
                            foreach (PageItem item3 in page.Items)
                                if (item3.name == settings[index + 1])
                                {
                                    item2.Item = item3;
                                    break;
                                }
                            break;
                        }
                }
                string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                AssetDatabase.AddObjectToAsset(item2, _path);
                itemGroup[i] = item2;
                index += 3;
            }
            item.ButtonGroup = itemGroup;
            foreach (Page page in preset.Pages)
                if (page.name == settings[index])
                {
                    item.PageReference = page;
                    break;
                }
            item.Control = new VRCExpressionsMenu.Control();
            item.Control.name = settings[index++];
            if (settings[index++] != "NULL")
                item.Control.icon = (Texture2D)AssetDatabase.LoadAssetAtPath(settings[index], typeof(Texture2D));
            index += 1;
            item.Control.labels = new VRCExpressionsMenu.Control.Label[int.Parse(settings[index])];
            index += 1;
            for (int i = 0; i < item.Control.labels.Length; i++)
            {
                item.Control.labels[i] = new VRCExpressionsMenu.Control.Label();
                item.Control.labels[i].name = settings[index];
                index += 1;
                if (settings[index] != "NULL")
                    item.Control.labels[i].icon = (Texture2D)AssetDatabase.LoadAssetAtPath(settings[index], typeof(Texture2D));
                index += 1;
            }
            item.Control.parameter = new VRCExpressionsMenu.Control.Parameter() { name = settings[index] };
            index += 1;
            switch (settings[index])
            {
                case "STYLE1":
                    item.Control.style = VRCExpressionsMenu.Control.Style.Style1;
                    break;
                case "STYLE2":
                    item.Control.style = VRCExpressionsMenu.Control.Style.Style2;
                    break;
                case "STYLE3":
                    item.Control.style = VRCExpressionsMenu.Control.Style.Style3;
                    break;
                case "STYLE4":
                    item.Control.style = VRCExpressionsMenu.Control.Style.Style4;
                    break;
            }
            index += 1;
            if (settings[index] != "NULL")
                item.Control.subMenu = (VRCExpressionsMenu)AssetDatabase.LoadAssetAtPath(settings[index], typeof(VRCExpressionsMenu));
            index += 1;
            item.Control.subParameters = new VRCExpressionsMenu.Control.Parameter[int.Parse(settings[index])];
            index += 1;
            for (int i = 0; i < item.Control.subParameters.Length; i++)
            {
                item.Control.subParameters[i] = new VRCExpressionsMenu.Control.Parameter();
                item.Control.subParameters[i].name = settings[index];
                index += 1;
            }
            switch (settings[index])
            {
                case "BUTTON":
                    item.Control.type = VRCExpressionsMenu.Control.ControlType.Button;
                    break;
                case "TOGGLE":
                    item.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                    break;
                case "SUBMENU":
                    item.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
                    break;
                case "RADIAL PUPPET":
                    item.Control.type = VRCExpressionsMenu.Control.ControlType.RadialPuppet;
                    break;
                case "TWO AXIS PUPPET":
                    item.Control.type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
                    break;
                case "FOUR AXIS PUPPET":
                    item.Control.type = VRCExpressionsMenu.Control.ControlType.FourAxisPuppet;
                    break;
            }
            index += 1;
            item.Control.value = float.Parse(settings[index]);

            // Save Undo operation
            Undo.CollapseUndoOperations(group);
        }

        // Duplicates an item within the same page
        public static void DuplicateItem(object args)
        {
            InventoryPreset preset = (InventoryPreset)((object[])args)[2];
            Page oldPage = ((object[])args)[0] as Page;
            PageItem oldItem = ((object[])args)[1] as PageItem;

            // Configure the path.
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());

            // Copy an item to the page.
            EditorUtility.SetDirty(preset);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            PageItem item = ScriptableObject.CreateInstance<PageItem>();
            Undo.RegisterCreatedObjectUndo(item, "Duplicate Item");

            // Configure the new item and add it to the Asset.
            item.hideFlags = HideFlags.HideInHierarchy;
            item.name = oldItem.name;
            item.Icon = oldItem.Icon;
            item.Type = oldItem.Type;
            item.UseAnimations = oldItem.UseAnimations;
            item.EnableClip = oldItem.EnableClip;
            item.DisableClip = oldItem.DisableClip;
            item.TransitionType = oldItem.TransitionType;
            item.TransitionDuration = oldItem.TransitionDuration;
            item.ObjectReference = oldItem.ObjectReference;
            item.InitialState = oldItem.InitialState;
            item.Sync = oldItem.Sync;
            item.Saved = oldItem.Saved;
            item.EnableGroup = new GroupItem[oldItem.EnableGroup.Length];
            for (int j = 0; j < item.EnableGroup.Length; j++)
            {
                GroupItem item2 = ScriptableObject.CreateInstance<GroupItem>();
                Undo.RegisterCreatedObjectUndo(item2, "Duplicate Item");

                // Configure and add the new item to the array.
                item2.name = oldItem.EnableGroup[j].name;
                item2.hideFlags = HideFlags.HideInHierarchy;
                item2.Reaction = oldItem.EnableGroup[j].Reaction;
                item2.Item = null;
                foreach (Page page1 in preset.Pages)
                    if (item2.Item == null)
                    {
                        foreach (PageItem item3 in page1.Items)
                            if (item3 == oldItem.EnableGroup[j].Item)
                            {
                                item2.Item = oldItem.EnableGroup[j].Item;
                                break;
                            }
                    }
                    else
                        break;
                string _path2 = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                AssetDatabase.AddObjectToAsset(item2, _path2);
                Undo.RecordObject(item, "Duplicate Item");
                item.EnableGroup[j] = item2;
            }
            item.DisableGroup = new GroupItem[oldItem.DisableGroup.Length];
            for (int j = 0; j < item.DisableGroup.Length; j++)
            {
                GroupItem item2 = ScriptableObject.CreateInstance<GroupItem>();
                Undo.RegisterCreatedObjectUndo(item2, "Duplicate Item");

                // Configure and add the new item to the array.
                item2.name = oldItem.DisableGroup[j].name;
                item2.hideFlags = HideFlags.HideInHierarchy;
                item2.Reaction = oldItem.DisableGroup[j].Reaction;
                item2.Item = null;
                foreach (Page page1 in preset.Pages)
                    if (item2.Item == null)
                    {
                        foreach (PageItem item3 in page1.Items)
                            if (item3 == oldItem.DisableGroup[j].Item)
                            {
                                item2.Item = oldItem.DisableGroup[j].Item;
                                break;
                            }
                    }
                    else
                        break;
                string _path2 = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                AssetDatabase.AddObjectToAsset(item2, _path2);
                Undo.RecordObject(item, "Duplicate Item");
                item.DisableGroup[j] = item2;
            }
            item.ButtonGroup = new GroupItem[oldItem.ButtonGroup.Length];
            for (int j = 0; j < item.ButtonGroup.Length; j++)
            {
                GroupItem item2 = ScriptableObject.CreateInstance<GroupItem>();
                Undo.RegisterCreatedObjectUndo(item2, "Duplicate Item");

                // Configure and add the new item to the array.
                item2.name = oldItem.ButtonGroup[j].name;
                item2.hideFlags = HideFlags.HideInHierarchy;
                item2.Reaction = oldItem.ButtonGroup[j].Reaction;
                item2.Item = null;
                foreach (Page page1 in preset.Pages)
                    if (item2.Item == null)
                    {
                        foreach (PageItem item3 in page1.Items)
                            if (item3 == oldItem.ButtonGroup[j].Item)
                            {
                                item2.Item = oldItem.ButtonGroup[j].Item;
                                break;
                            }
                    }
                    else
                        break;
                string _path2 = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                AssetDatabase.AddObjectToAsset(item2, _path2);
                Undo.RecordObject(item, "Duplicate Item");
                item.ButtonGroup[j] = item2;
            }
            item.PageReference = (oldItem.PageReference != null && preset.Pages.Contains(oldItem.PageReference)) ? oldItem.PageReference : null;
            item.Control = new VRCExpressionsMenu.Control()
            {
                name = oldItem.Control.name,
                icon = oldItem.Control.icon,
                labels = (VRCExpressionsMenu.Control.Label[])oldItem.Control.labels.Clone(),
                parameter = oldItem.Control.parameter,
                style = oldItem.Control.style,
                subMenu = oldItem.Control.subMenu,
                subParameters = (VRCExpressionsMenu.Control.Parameter[])oldItem.Control.subParameters.Clone(),
                type = oldItem.Control.type,
                value = oldItem.Control.value
            };
            AssetDatabase.AddObjectToAsset(item, _path);
            Undo.RecordObject(oldPage, "Duplicate Item");
            oldPage.Items.Add(item);
            Undo.CollapseUndoOperations(group);
        }

        // Copies the settings of a group to the system buffer
        public static void CopyGroupSettings(object args)
        {
            PageItem item = ((object[])args)[0] as PageItem;
            int groupType = (int)((object[])args)[1];

            InventoryPreset preset = ((object[])args)[2] as InventoryPreset;

            /*
             * IDENTIFIER
             * TYPE
             * GROUP LENGTH
             * GROUP ITEMS (PAGE | ITEM | REACTION)
             */

            StringBuilder groupEncode = new StringBuilder();
            groupEncode.AppendLine("INVENTORY INVENTOR ENCODE");
            groupEncode.AppendLine("GROUP");
            GroupItem[] activeGroup = new GroupItem[0];
            switch (groupType)
            {
                case 0:
                    activeGroup = item.EnableGroup;
                    break;
                case 1:
                    activeGroup = item.DisableGroup;
                    break;
                case 2:
                    activeGroup = item.ButtonGroup;
                    break;
            }
            groupEncode.AppendLine(activeGroup.Length.ToString());
            foreach (GroupItem groupItem in activeGroup)
            {
                if (groupItem.Item != null)
                {
                    bool found = false;
                    foreach (Page page in preset.Pages)
                        if (!found)
                        {
                            foreach (PageItem pageItem in page.Items)
                                if (pageItem == groupItem.Item)
                                {
                                    groupEncode.AppendLine(page.name);
                                    break;
                                }
                        }
                        else
                        {
                            groupEncode.AppendLine("NULL");
                            break;
                        }
                    groupEncode.AppendLine(groupItem.Item.name);
                }
                else
                {
                    groupEncode.AppendLine("NULL");
                    groupEncode.AppendLine("NULL");
                }
                switch (groupItem.Reaction)
                {
                    case GroupItem.GroupType.Enable:
                        groupEncode.AppendLine("ENABLE");
                        break;
                    case GroupItem.GroupType.Disable:
                        groupEncode.AppendLine("DISABLE");
                        break;
                }
            }
            //Debug.Log(groupEncode.ToString());
            EditorGUIUtility.systemCopyBuffer = groupEncode.ToString();
        }

        // Pastes the settings of a group from the system buffer
        public static void PasteGroupSettings(object args)
        {
            string[] settings = EditorGUIUtility.systemCopyBuffer.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (settings[0] != "INVENTORY INVENTOR ENCODE" || settings[1] != "GROUP")
                return;

            PageItem item = ((object[])args)[0] as PageItem;
            int groupType = (int)((object[])args)[1];

            // Mark Preset as dirty
            InventoryPreset preset = (InventoryPreset)((object[])args)[2];
            EditorUtility.SetDirty(preset);

            // Record the change
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            // Paste group settings
            GroupItem[] itemGroup = new GroupItem[int.Parse(settings[2])];
            int index = 3;
            for (int i = 0; i < itemGroup.Length; i++)
            {
                GroupItem item2 = ScriptableObject.CreateInstance<GroupItem>();
                Undo.RegisterCreatedObjectUndo(item2, "Pasted Settings");

                // Configure and add the new item to the array.
                if (settings[index] != "NULL")
                    item2.name = settings[index] + ": " + settings[index + 1];
                else
                    item2.name = "None";
                item2.hideFlags = HideFlags.HideInHierarchy;
                switch (settings[index + 2])
                {
                    case "ENABLE":
                        item2.Reaction = GroupItem.GroupType.Enable;
                        break;
                    case "DISABLE":
                        item2.Reaction = GroupItem.GroupType.Disable;
                        break;
                }
                item2.Item = null;
                if (settings[index] != "NULL")
                {
                    foreach (Page page in preset.Pages)
                        if (page.name == settings[index])
                        {
                            foreach (PageItem item3 in page.Items)
                                if (item3.name == settings[index + 1])
                                {
                                    item2.Item = item3;
                                    break;
                                }
                            break;
                        }
                }
                string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                AssetDatabase.AddObjectToAsset(item2, _path);
                itemGroup[i] = item2;
                index += 3;
            }
            Undo.RecordObject(item, "Pasted Settings");
            switch (groupType)
            {
                case 0:
                    item.EnableGroup = itemGroup;
                    break;
                case 1:
                    item.DisableGroup = itemGroup;
                    break;
                case 2:
                    item.ButtonGroup = itemGroup;
                    break;
            }

            // Save Undo operation
            Undo.CollapseUndoOperations(group);
        }

        // Removes unused Sub-Assets from the file and saves any changes made to the remaining Assets.
        public static void SaveChanges(InventoryPreset preset)
        {
            // Save any changes made to Assets within the file.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Retrieve all Assets contained within the file.
            UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(preset.GetInstanceID()));

            // Loop through each Asset and check if it is used within the preset.
            bool[] used = new bool[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                //switch can't be used here because typeof doesn't return a constant.

                // InventoryPreset
                if (objects[i].GetType() == typeof(InventoryPreset))
                {
                    if ((InventoryPreset)objects[i] == preset)
                        used[i] = true;
                    else
                        continue;
                }

                // Page
                else if (objects[i].GetType() == typeof(Page))
                {
                    if (preset.Pages.Contains((Page)objects[i]))
                        used[i] = true;
                    else
                        continue;
                }

                // PageItem
                else if (objects[i].GetType() == typeof(PageItem))
                {
                    foreach (Page page in preset.Pages)
                    {
                        if (page.Items.Contains((PageItem)objects[i]))
                        {
                            used[i] = true;
                            break;
                        }
                    }
                }

                // GroupItem
                else if (objects[i].GetType() == typeof(GroupItem))
                {
                    foreach (Page page in preset.Pages)
                    {
                        foreach (PageItem item in page.Items)
                        {
                            if (Array.IndexOf(item.EnableGroup, (GroupItem)objects[i]) != -1 || Array.IndexOf(item.DisableGroup, (GroupItem)objects[i]) != -1 || Array.IndexOf(item.ButtonGroup, (GroupItem)objects[i]) != -1)
                            {
                                used[i] = true;
                                break;
                            }
                        }
                        if (used[i])
                            break;
                    }
                }
            }

            // Loop through all the Assets and remove the unused ones from the file.
            for (int i = 0; i < objects.Length; i++)
                if (!used[i])
                    AssetDatabase.RemoveObjectFromAsset(objects[i]);

            // Save changes.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Upgrades older Assets while retaining their settings.
        public static void UpgradePreset(InventoryPreset preset)
        {
            EditorUtility.SetDirty(preset);
            while (preset.Version < currentVersion)
            {
                switch (preset.Version)
                {
                    case 0:
                        bool subMenuExists = false;
                        foreach (Page page in preset.Pages)
                            foreach (PageItem item in page.Items)
                            {
                                switch (item.Type)
                                {
                                    case PageItem.ItemType.Toggle:
                                        // Turn on Animations for all existing Toggles that had any.
                                        if (item.EnableClip != null || item.DisableClip != null)
                                            item.UseAnimations = true;
                                        break;
                                    case PageItem.ItemType.Subpage:
                                        // Set the name of the item to the name of the Page.
                                        item.name = item.PageReference.name;
                                        break;
                                    case PageItem.ItemType.Control:
                                        // Submenu data is no longer stored, so inform the user to readd them manually afterwards.
                                        subMenuExists = true;
                                        item.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
                                        break;
                                }
                            }
                        if (subMenuExists)
                            EditorUtility.DisplayDialog("Inventory Inventor", "One or more Submenu Items were discovered on this Preset while upgrading. These have automatically turned into Control Items and will need to be reassigned their Expressions Menus manually.", "Close");
                        break;
                    case 1:
                        // Reimport to fix class reference
                        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(preset.GetInstanceID()), ImportAssetOptions.ImportRecursive);
                        break;
                    case 2:
                        // Assign default path to all presets
                        preset.LastPath = "";

                        // Set some new default values
                        foreach (Page page in preset.Pages)
                            foreach (PageItem item in page.Items)
                            {
                                // Turn on Normalized time for all items.
                                item.TransitionType = true;
                            }    
                        break;
                    case 3:
                        // Future upgrade placeholder
                        break;
                }
                preset.Version++;
            }
            SaveChanges(preset);
        }

        // Upgrades all presets found in the project
        public static void UpgradeAll(bool notify)
        {
            // Get all presets
            string[] guids = AssetDatabase.FindAssets("t:InventoryPreset");
            int counter = guids.Length;
            foreach (string g in guids)
            {
                // Get the preset object
                InventoryPreset preset = (InventoryPreset)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(g), typeof(InventoryPreset));

                if (preset != null)
                {
                    // Upgrade it if necessary
                    if (preset.Pages.Count == 0)
                        preset.Version = currentVersion;
                    else if (preset.Version < currentVersion)
                        UpgradePreset(preset);
                    else
                        counter--;
                }
            }
            if (notify)
            {
                if (counter > 0)
                    EditorUtility.DisplayDialog("Inventory Inventor", counter + " presets were upgraded.", "Close");
                else
                    EditorUtility.DisplayDialog("Inventory Inventor", "No outdated presets found.", "Close");
            }
                
        }
    }
}
#endif