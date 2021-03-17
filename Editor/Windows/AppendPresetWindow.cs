using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace InventoryInventor.Preset
{
    public class AppendPresetWindow : EditorWindow
    {
        // Tracker data
        public static AppendPresetWindow Instance { get; private set; }
        public static bool IsOpen;
        private Vector2 scroll = new Vector2();

        // Preset being modified
        private InventoryPreset preset;

        // Preset to Append
        private InventoryPreset appendPreset;

        // Pages to Add
        private string[] pageNames;
        private bool[] selectedPages;

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

        public static void AppendPresetWindowInit(InventoryPreset preset)
        {
            Instance = (AppendPresetWindow)GetWindow(typeof(AppendPresetWindow), false, preset.name);
            Instance.titleContent = new GUIContent(preset.name);
            Instance.minSize = new Vector2(375f, 200f);
            Instance.maxSize = new Vector2(375f, Instance.maxSize.y);
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

            // Preset to Append
            EditorGUI.BeginChangeCheck();
            appendPreset = (InventoryPreset)EditorGUILayout.ObjectField(new GUIContent("Preset to Append", "The Preset to append."), appendPreset, typeof(InventoryPreset), true);
            if (appendPreset != null && (EditorGUI.EndChangeCheck() || (pageNames != null && pageNames.Length != appendPreset.Pages.Count)))
            {
                pageNames = new string[appendPreset.Pages.Count];
                for (int i = 0; i < appendPreset.Pages.Count; i++)
                    pageNames[i] = appendPreset.Pages[i].name;
                selectedPages = new bool[appendPreset.Pages.Count];
            }

            // Page Display and Confirmation
            if (appendPreset != null)
            {
                EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
                EditorGUILayout.BeginHorizontal();

                var r = EditorGUILayout.BeginVertical();
                if (pageNames.Length > 0 && selectedPages.Length > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("Add", EditorStyles.boldLabel, GUILayout.MaxWidth(32f));
                    GUILayout.Space(6);
                    EditorGUILayout.LabelField("Pages", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();
                    // Separator
                    var rect = EditorGUILayout.BeginHorizontal();
                    Handles.color = Color.gray;
                    Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.x + rect.width + 5f, rect.y + 1));
                    EditorGUILayout.EndHorizontal();

                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    for (int i = 0; i < pageNames.Length; i++)
                    {
                        if (i != 0)
                        {
                            // Separator
                            rect = EditorGUILayout.BeginHorizontal();
                            Handles.color = Color.gray;
                            Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.x + rect.width + 5f, rect.y + 1));
                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(16);
                        selectedPages[i] = EditorGUILayout.Toggle(selectedPages[i], GUILayout.MaxWidth(32f));
                        EditorGUILayout.LabelField(pageNames[i]);
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndScrollView();
                }
                EditorGUILayout.EndVertical();

                // Separator
                Handles.color = Color.gray;
                Handles.DrawLine(new Vector2(r.x + 48, r.y), new Vector2(r.x + 48, r.y + r.height));

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")), GUILayout.MaxHeight(120f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("<i>No Preset Selected</i>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
            }
            GUILayout.FlexibleSpace();

            // Confirm Button
            DrawLine(false);
            EditorGUILayout.Space();
            if (GUILayout.Button("Append"))
                AppendPreset(preset, appendPreset, selectedPages);

            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }

        // Appends pages from one preset to the current one.
        private static void AppendPreset(InventoryPreset preset, InventoryPreset appendPreset, bool[] selectedPages)
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
            for (int i = 0; i < oldPages.Count; i++)
            {
                if (selectedPages[i])
                {
                    Page newPage = DeepClonePage(oldPages[i]);
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
            InventoryPresetUtility.SaveChanges(preset);
            if (Instance != null)
                Instance.Close();
            EditorUtility.DisplayDialog("Inventory Inventor", "All menus imported successfully.", "Close");
        }

        // Deep clones a page
        private static Page DeepClonePage(Page page)
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
        private static PageItem DeepCloneItem(PageItem item)
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
        private static GroupItem DeepCloneGroupItem(GroupItem groupItem)
        {
            GroupItem newGroupItem = CreateInstance<GroupItem>();
            newGroupItem.name = groupItem.name;
            newGroupItem.hideFlags = groupItem.hideFlags;
            newGroupItem.Item = groupItem.Item;
            newGroupItem.Reaction = groupItem.Reaction;
            return newGroupItem;
        }

        // Finds a cloned page
        private static Page FindClonedPage(Page oldPage, ref List<Page> newPages)
        {
            if (oldPage == null)
                return null;
            foreach (Page page in newPages)
                if (page.name == oldPage.name)
                    return page;
            return newPages[0];
        }

        // Finds a cloned item
        private static PageItem FindClonedItem(PageItem oldItem, ref List<Page> newPages, ref List<Page> oldPages)
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
