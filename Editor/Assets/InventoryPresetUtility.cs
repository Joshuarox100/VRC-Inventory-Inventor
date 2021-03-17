using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace InventoryInventor.Preset 
{
    public class InventoryPresetUtility
    {
        // Version tracker used for upgrading old presets.
        public static readonly int currentVersion = 2;

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
                        // Future upgrade placeholder
                        break;
                }
                preset.Version++;
            }
            SaveChanges(preset);
        }

        // Upgrades all presets found in the project
        public static void UpgradeAll()
        {
            // Get all presets
            string[] guids = AssetDatabase.FindAssets("t:InventoryPreset");
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
                }
            }
        }
    }
}
