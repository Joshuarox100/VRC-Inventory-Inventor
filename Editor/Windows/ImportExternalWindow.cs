using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace InventoryInventor.Preset
{
    public class ImportExternalWindow : EditorWindow
    {
        // Tracker data
        public static ImportExternalWindow Instance { get; private set; }
        private static bool IsOpen;

        // Preset being modified
        private InventoryPreset preset;

        // Menu to Import
        private VRCExpressionsMenu importMenu;

        // Import Submenus
        private bool enableRecursion = false;

        private void OnEnable()
        {
            IsOpen = true;
            EditorApplication.wantsToQuit += WantsToQuit;
        }

        private void OnDestroy()
        {
            IsOpen = false;
            EditorApplication.wantsToQuit -= WantsToQuit;
        }

        public static void ImportExternalWindowInit(InventoryPreset preset)
        {
            Instance = (ImportExternalWindow)GetWindow(typeof(ImportExternalWindow), false, preset.name);
            Instance.titleContent = new GUIContent(preset.name);
            Instance.minSize = new Vector2(375f, 100f);
            Instance.maxSize = new Vector2(375f, 100f);
            Instance.preset = preset;
            Instance.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            // Header
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Import Settings", EditorStyles.boldLabel);
            DrawLine(false);
            GUILayout.FlexibleSpace();

            // Menu to Import
            importMenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Expressions Menu", "The Expressions Menu to add to the Preset."), importMenu, typeof(VRCExpressionsMenu), true);

            // Import Submenus
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Import Submenus", "Add any Submenus found as well."));
            enableRecursion = !Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(!enableRecursion), new string[] { "Yes", "No" }));
            EditorGUILayout.EndHorizontal();

            // Confirm Button
            GUILayout.FlexibleSpace();
            DrawLine(false);
            EditorGUILayout.Space();
            if (GUILayout.Button("Import"))
                ImportMenus();

            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }

        // Converts and imports the menus.
        private void ImportMenus()
        {
            // Null checks
            if (importMenu == null)
            {
                EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: No menu selected.", "Close");
                return;
            }
            else if (preset == null)
            {
                EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: No preset selected.", "Close");
                return;
            }

            // Identify menus
            List<VRCExpressionsMenu> allMenus = new List<VRCExpressionsMenu>();
            if (enableRecursion)
                FindMenus(importMenu, ref allMenus);
            else
                allMenus.Add(importMenu);

            // Convert menus
            List<Page> newPages = new List<Page>();
            foreach (VRCExpressionsMenu menu in allMenus)
                newPages.Add(ConvertMenu(menu));

            // Update Page references (if needed)
            if (enableRecursion)
                foreach (Page page in newPages)
                    foreach (PageItem item in page.Items)
                        if (item.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && item.Control.subMenu != null)
                        {
                            item.Type = PageItem.ItemType.Subpage;
                            item.PageReference = newPages[allMenus.IndexOf(item.Control.subMenu)];
                        }

            // Append to Preset
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            foreach (Page page in newPages)
            {
                page.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(page, _path);
                foreach (PageItem item in page.Items)
                {
                    item.hideFlags = HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(item, _path);
                }
            }
            preset.Pages.AddRange(newPages);

            // Save changes
            InventoryPresetEditor.SaveChanges(preset);
            Close();
            EditorUtility.DisplayDialog("Inventory Inventor", "All menus imported successfully.", "Close");
        }

        // Indexes through a given page and returns all associated pages inside a referenced list
        private void FindMenus(VRCExpressionsMenu menu, ref List<VRCExpressionsMenu> menuList)
        {
            menuList.Add(menu);
            foreach (VRCExpressionsMenu.Control control in menu.controls)
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != null && !menuList.Contains(control.subMenu))
                    FindMenus(control.subMenu, ref menuList);
        }

        // Converts a menu into a page
        private Page ConvertMenu(VRCExpressionsMenu menu)
        {
            // Create Page
            Page page = CreateInstance<Page>();
            page.name = menu.name;

            // Convert Controls to Items
            foreach (VRCExpressionsMenu.Control control in menu.controls)
            {
                PageItem item = CreateInstance<PageItem>();
                item.name = control.name;
                item.Type = PageItem.ItemType.Control;
                item.Control = control;
                page.Items.Add(item);
            }

            // Return Conversion
            return page;
        }

        static bool WantsToQuit()
        {
            if (IsOpen)
            {
                Instance.Close();
                DestroyImmediate(Instance);
            }
            return true;
        }

        // Draws a line across the GUI.
        private void DrawLine(bool addSpace = true)
        {
            var rect = EditorGUILayout.BeginHorizontal();
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(rect.x - 15, rect.y), new Vector2(rect.width + 15, rect.y));
            EditorGUILayout.EndHorizontal();
            if (addSpace)
            {
                EditorGUILayout.Space();
            }
        }
    }
}
