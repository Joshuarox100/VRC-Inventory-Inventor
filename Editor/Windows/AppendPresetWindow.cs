using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace InventoryInventor.Preset
{
    public class AppendPresetWindow : EditorWindow
    {
        // Tracker data
        public static AppendPresetWindow Instance { get; private set; }
        public static bool IsOpen
        {
            get { return Instance != null; }
        }

        // Preset being modified
        private InventoryPreset preset;

        // Preset to Append
        private InventoryPreset appendPreset;

        public static void AppendPresetWindowInit(InventoryPreset preset)
        {
            Instance = (AppendPresetWindow)GetWindow(typeof(AppendPresetWindow), false, "Inventory Inventor");
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
            EditorGUILayout.LabelField("Append Settings", EditorStyles.boldLabel);
            DrawLine(false);
            GUILayout.FlexibleSpace();

            // Preset to Append
            appendPreset = (InventoryPreset)EditorGUILayout.ObjectField(new GUIContent("Preset to Append", "The Preset to append."), appendPreset, typeof(InventoryPreset), true);

            // Confirm Button
            GUILayout.FlexibleSpace();
            DrawLine(false);
            EditorGUILayout.Space();
            if (GUILayout.Button("Append"))
                AppendPreset(appendPreset);

            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }

        // Appends pages from one preset to the current one.
        private void AppendPreset(InventoryPreset appendPreset)
        {
            // Null checks
            if (appendPreset == null || preset == null)
            {
                EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: No preset selected.", "Close");
                return;
            }

            // Append to Preset
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            List<Page> newPages = new List<Page>();
            List<Page> oldPages = appendPreset.Pages;
            foreach (Page page in oldPages)
            {
                Page newPage = DeepClonePage(page);
                newPages.Add(newPage);
                AssetDatabase.AddObjectToAsset(newPage, _path);
                foreach (PageItem item in newPage.Items)
                {
                    AssetDatabase.AddObjectToAsset(item, _path);
                    foreach (GroupItem groupItem in item.ButtonGroup)
                        AssetDatabase.AddObjectToAsset(groupItem, _path);
                    foreach (GroupItem groupItem in item.EnableGroup)
                        AssetDatabase.AddObjectToAsset(groupItem, _path);
                    foreach (GroupItem groupItem in item.DisableGroup)
                        AssetDatabase.AddObjectToAsset(groupItem, _path);
                }
            }
            preset.Pages.AddRange(newPages);

            // Correct references
            
            foreach (Page page in newPages)
            {
                foreach (PageItem item in page.Items)
                {
                    item.PageReference = FindClonedPage(item.PageReference, ref newPages);
                    foreach (GroupItem groupItem in item.ButtonGroup)
                        groupItem.Item = FindClonedItem(groupItem.Item, ref newPages, ref oldPages);
                    foreach (GroupItem groupItem in item.EnableGroup)
                        groupItem.Item = FindClonedItem(groupItem.Item, ref newPages, ref oldPages);
                    foreach (GroupItem groupItem in item.DisableGroup)
                        groupItem.Item = FindClonedItem(groupItem.Item, ref newPages, ref oldPages);
                }
            }

            // Save changes
            InventoryPresetEditor.SaveChanges(preset);
            Close();
            EditorUtility.DisplayDialog("Inventory Inventor", "All menus imported successfully.", "Close");
        }

        // Deep clones a page
        private Page DeepClonePage(Page page)
        {
            Page newPage = CreateInstance<Page>();
            newPage.name = page.name;
            newPage.Icon = page.Icon;
            newPage.hideFlags = page.hideFlags;
            foreach (PageItem item in page.Items)
            {
                PageItem newItem = DeepCloneItem(item);
                newPage.Items.Add(newItem);
            }
            return newPage;
        }

        // Deep clones an item
        private PageItem DeepCloneItem(PageItem item)
        {
            PageItem newItem = CreateInstance<PageItem>();
            newItem.name = item.name;
            newItem.Icon = item.Icon;
            newItem.hideFlags = item.hideFlags;
            newItem.InitialState = item.InitialState;
            newItem.ObjectReference = item.ObjectReference;
            newItem.PageReference = item.PageReference;
            newItem.Saved = item.Saved;
            newItem.Sync = item.Sync;
            newItem.Type = item.Type;
            newItem.UseAnimations = item.UseAnimations;
            newItem.Control = item.Control;
            newItem.EnableClip = item.EnableClip;
            newItem.DisableClip = item.DisableClip;
            List<GroupItem> tempItems = new List<GroupItem>();
            foreach (GroupItem groupItem in item.ButtonGroup)
            {
                GroupItem newGroupItem = DeepCloneGroupItem(groupItem);
                tempItems.Add(newGroupItem);
            }
            newItem.ButtonGroup = tempItems.ToArray();
            tempItems.Clear();
            foreach (GroupItem groupItem in item.EnableGroup)
            {
                GroupItem newGroupItem = DeepCloneGroupItem(groupItem);
                tempItems.Add(newGroupItem);
            }
            newItem.EnableGroup = tempItems.ToArray();
            tempItems.Clear();
            foreach (GroupItem groupItem in item.DisableGroup)
            {
                GroupItem newGroupItem = DeepCloneGroupItem(groupItem);
                tempItems.Add(newGroupItem);
            }
            newItem.DisableGroup = tempItems.ToArray();
            tempItems.Clear();
            return newItem;
        }

        // Deep clones a group item
        private GroupItem DeepCloneGroupItem(GroupItem groupItem)
        {
            GroupItem newGroupItem = CreateInstance<GroupItem>();
            newGroupItem.name = groupItem.name;
            newGroupItem.hideFlags = groupItem.hideFlags;
            newGroupItem.Item = groupItem.Item;
            newGroupItem.Reaction = groupItem.Reaction;
            return newGroupItem;
        }

        // Finds a cloned page
        private Page FindClonedPage(Page oldPage, ref List<Page> newPages)
        {
            if (oldPage == null)
                return null;
            foreach (Page page in newPages)
                if (page.name == oldPage.name)
                    return page;
            return null;
        }

        // Finds a cloned item
        private PageItem FindClonedItem(PageItem oldItem, ref List<Page> newPages, ref List<Page> oldPages)
        {
            if (oldItem == null)
                return null;
            Page oldPage = null;
            int index = 0;
            foreach (Page page in oldPages)
                if (page.Items.Contains(oldItem))
                {
                    index = page.Items.IndexOf(oldItem);
                    oldPage = page;
                    break;
                }
            Page newPage = FindClonedPage(oldPage, ref newPages);
            return newPage.Items[index];
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
