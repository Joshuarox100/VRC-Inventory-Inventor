using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Avatars.Components;

[CustomEditor(typeof(InventoryPreset))]
public class InventoryPresetEditor : Editor 
{
    // The Asset being edited.
    private InventoryPreset preset;

    // The Avatar being configured.
    private VRCAvatarDescriptor avatar;

    // Reorderable Lists to be displayed.
    private readonly Dictionary<string, ReorderableList> pageContentsDict = new Dictionary<string, ReorderableList>();
    private ReorderableList pageDirectory;
    private ReorderableList enableGroupContents;
    private ReorderableList disableGroupContents;

    // Tracked values for indexing
    private int focusedItemPage = 0;
    private bool focusedOnItem = false;
    private bool draggingPage = false;

    // Default values obtained and used for displaying ReorderableLists.
    private float defaultHeaderHeight;
    private float defaultFooterHeight;

    // Scrollbar Vectors.
    private Vector2 directoryScroll;
    private Vector2 enableScroll;
    private Vector2 disableScroll;

    // Pixel colors for various GUIStyles.
    private Texture2D[] barColors;

    // Foldout booleans.
    private bool usageFoldout = false;
    private bool missingFoldout = false;
    private readonly List<bool> pagesFoldout = new List<bool>();
    private bool groupFoldout = false;

    // Get the targeted object and initialize ReorderableLists.
    public void OnEnable()
    {
        preset = (InventoryPreset)target;
        pagesFoldout.AddRange(new bool[preset.Pages.Count]);

        // Setup bar colors.
        barColors = new Texture2D[2] { new Texture2D(1, 1), new Texture2D(1, 1) };
        barColors[0].SetPixel(0, 0, Color.yellow * new Color(1, 1, 1, 0.9f));
        barColors[0].Apply();
        barColors[1].SetPixel(0, 0, Color.red * new Color(1, 1, 1, 0.9f));
        barColors[1].Apply();

        // Wait until the Asset has been fully created if it hasn't already.
        string _hasPath = AssetDatabase.GetAssetPath(preset.GetInstanceID());
        if (_hasPath == "")
        {
            return;
        }

        pageDirectory = new ReorderableList(preset.Pages, typeof(Page), true, false, false, false)
        {
            elementHeight = 18f,
        };
        pageDirectory.drawHeaderCallback += (Rect rect) =>
        {
            GUI.Label(rect, "Directory");
        };
        pageDirectory.drawElementCallback += (Rect rect, int index, bool active, bool focused) =>
        {
            // The element being drawn.
            Page item = preset.Pages[index];

            // Deal with pages that share the same name.
            List<string> names = new List<string>();
            foreach (Page page in preset.Pages)
            {
                if (page != item)
                    names.Add(page.name);
            }
            if (names.Contains(item.name) && names.IndexOf(item.name) != index)
            {
                int occurance = 0;
                while (names.Contains(item.name + " " + occurance))
                {
                    occurance++;
                }
                item.name = item.name + " " + occurance;
            }

            // Draw the page name and type. If the element is first in the list, display "Default" as the type.
            pagesFoldout[index] = EditorGUI.Foldout(new Rect(rect.x + 8, rect.y + EditorGUIUtility.singleLineHeight / 4, rect.width, 18f), pagesFoldout[index], item.name + ((index == 0) ? " (Default)" : ""));

            if (pagesFoldout[index] && !draggingPage)
            {
                EditorGUI.indentLevel++;
                string listKey = AssetDatabase.GetAssetPath(item) + "/" + item.name;
                ReorderableList innerList;
                if (pageContentsDict.ContainsKey(listKey))
                {
                    // fetch the reorderable list in dict
                    innerList = pageContentsDict[listKey];
                }
                else
                {
                    // create reorderable list and store it in dict
                    innerList = new ReorderableList(item.Items, typeof(PageItem))
                    {
                        displayAdd = false,
                        displayRemove = false,
                        draggable = true,

                        headerHeight = 2f,
                        elementHeight = 18f,
                    };
                    innerList.drawElementCallback += (Rect rect2, int index2, bool active2, bool focused2) =>
                    {
                        // The item being drawn.
                        PageItem item2 = item.Items[index2];

                        if (focused2)
                        {
                            focusedItemPage = index;
                        }

                        // Draw the item's name and type.
                        EditorGUI.indentLevel--;
                        EditorGUI.LabelField(new Rect(rect2.x, rect2.y, rect2.width / 2, rect2.height), (item2.Type == PageItem.ItemType.Page) ? ((item2.PageReference != null) ? item2.PageReference.name : "None") : item2.name);
                        EditorGUI.LabelField(new Rect(rect2.x + (rect2.width / 2), rect2.y, rect2.width / 2, rect2.height), "Type: " + ((item2.Type == PageItem.ItemType.Toggle) ? "Toggle" : (item2.Type == PageItem.ItemType.Page) ? "Page" : "Submenu"));
                        Handles.color = Color.gray;
                        Handles.DrawLine(new Vector2(rect2.x + ((rect2.width - 15f) / 2), rect2.y), new Vector2(rect2.x + ((rect2.width - 15f) / 2), rect2.y + rect2.height));
                        if (index2 != 0)
                            Handles.DrawLine(new Vector2(rect2.x - 15, rect2.y), new Vector2(rect2.x + rect2.width, rect2.y));
                        EditorGUI.indentLevel++;
                    };
                    innerList.onAddCallback += (ReorderableList list2) =>
                    {
                        // Continue if there is less than eight items on the page.
                        if (list2.list.Count < 8)
                        {
                            // Mark the preset as dirty and record the creation of a new page item.
                            EditorUtility.SetDirty(preset);
                            Undo.IncrementCurrentGroup();
                            int group = Undo.GetCurrentGroup();
                            PageItem item2 = CreateInstance<PageItem>();
                            Undo.RegisterCreatedObjectUndo(item2, "Add Page Item");

                            // Configure the new item and add it to the Asset.
                            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                            item2.hideFlags = HideFlags.HideInHierarchy;
                            item2.name = "Slot " + (list2.list.Count + 1);
                            AssetDatabase.AddObjectToAsset(item2, _path);

                            // Record the state of the current page before add the item to it.
                            Undo.RecordObject(preset.Pages[index], "Add Page Item");
                            preset.Pages[index].Items.Add(item2);
                            Undo.CollapseUndoOperations(group);

                            // Focus the list on the new item.
                            list2.index = list2.list.Count - 1;
                        }
                    };
                    innerList.onRemoveCallback += (ReorderableList list2) =>
                    {
                        // Only continue if there is more than a single item on the page.
                        if (list2.list.Count > 1)
                        {
                            // Mark the preset as dirty and record the affected page before removing the item.
                            EditorUtility.SetDirty(preset);
                            Undo.RecordObject(preset.Pages[index], "Remove Page Item");
                            preset.Pages[index].Items.RemoveAt(list2.index);

                            // Focus the list on the prior item.
                            if (list2.index > 0)
                                list2.index -= 1;
                        }
                    };
                    innerList.drawFooterCallback += (Rect footerRect) =>
                    {
                        DrawButtons(innerList, item.Items.Count < 8, item.Items.Count > 1, "Create Item", "Remove Item", footerRect);
                    };

                    // Make sure that there is at least one item in the page.
                    if (item.Items.Count < 1)
                    {
                        PageItem innerItem = CreateInstance<PageItem>();
                        string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                        innerItem.hideFlags = HideFlags.HideInHierarchy;
                        AssetDatabase.AddObjectToAsset(innerItem, _path);
                        innerItem.name = "Slot 1";
                        preset.Pages[index].Items.Add(innerItem);
                    }

                    pageContentsDict[listKey] = innerList;
                }

                // Determine whether or not to display the add or remove buttons this Repaint.
                Rect innerRect = new Rect(rect.x, rect.y + 18f + EditorGUIUtility.singleLineHeight / 4, rect.width, rect.height);
                innerList.DoList(innerRect);

                EditorGUI.indentLevel--;
            }

            // Draw a separator line;
            Handles.color = Color.gray;
            if (index != 0)
                Handles.DrawLine(new Vector2(rect.x - 15, rect.y), new Vector2(rect.width + 15, rect.y));
        };
        pageDirectory.elementHeightCallback += (int index) =>
        {
            float propertyHeight = 18f;
            if (pagesFoldout[index] && !draggingPage)
            {
                propertyHeight += propertyHeight * preset.Pages[index].Items.Count + 36f;
            }

            float spacing = EditorGUIUtility.singleLineHeight / 2;

            return propertyHeight + spacing;
        };
        pageDirectory.onAddCallback += (ReorderableList list) =>
        {
            // Mark the preset as dirty and record the creation of a new page.
            EditorUtility.SetDirty(preset);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Page page = CreateInstance<Page>();
            Undo.RegisterCreatedObjectUndo(page, "Add Page");

            // Configure the page and add it to the Asset.
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            page.hideFlags = HideFlags.HideInHierarchy;
            page.name = "Page " + (pageDirectory.list.Count + 1);
            AssetDatabase.AddObjectToAsset(page, _path);

            // Record the current state of the preset and add the new page.
            Undo.RecordObject(preset, "Add Page");
            preset.Pages.Add(page);
            Undo.CollapseUndoOperations(group);

            // Focus the list on the new page.
            list.index = list.list.Count - 1;
            pagesFoldout.Add(false);
            directoryScroll.y = float.MaxValue;
        };
        pageDirectory.onRemoveCallback += (ReorderableList list) =>
        {
            // Only continue if there is more than a single page.
            if (preset.Pages.Count > 1)
            {
                // Mark the preset as dirty and record the preset before the page's deletion. 
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(preset, "Remove Page");
                preset.Pages.RemoveAt(list.index);
                pagesFoldout.RemoveAt(list.index);

                // Focus on the prior page.
                if (list.index > 0)
                    list.index -= 1;
            }
        };
        pageDirectory.onReorderCallbackWithDetails += (ReorderableList list, int oldIndex, int newIndex) =>
        {
            bool temp = pagesFoldout[oldIndex];
            pagesFoldout.RemoveAt(oldIndex);
            pagesFoldout.Insert(newIndex, temp);
            draggingPage = false;
        };
        pageDirectory.onMouseDragCallback += (ReorderableList list) => { draggingPage = true; };
        pageDirectory.onMouseUpCallback += (ReorderableList list) => { draggingPage = false; };

        defaultHeaderHeight = pageDirectory.headerHeight;
        defaultFooterHeight = pageDirectory.footerHeight;
        pageDirectory.footerHeight = 0f;
        pageDirectory.headerHeight = 0f;

        enableGroupContents = new ReorderableList(null, typeof(GroupItem), true, false, false, false)
        {
            elementHeight = 20f,
            headerHeight = 0,
            footerHeight = 0
        };
        enableGroupContents.drawHeaderCallback += (Rect rect) =>
        {
            GUI.Label(rect, new GUIContent("When Enabled...", "Modifies listed toggles when this toggle is enabled."));
        };
        enableGroupContents.drawElementCallback += (Rect rect, int index, bool active, bool focused) =>
        {
            EditorGUI.indentLevel--;
            // Accessor string
            string listKey = AssetDatabase.GetAssetPath(preset.Pages[focusedItemPage]) + "/" + preset.Pages[focusedItemPage].name;

            // The item being drawn.
            GroupItem item = preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup[index];

            // Create a list of all toggles, toggles not currently in the group, and the names of those later toggles.
            List<PageItem> allToggles = new List<PageItem>();
            List<PageItem> remainingToggles = new List<PageItem>();
            List<string> toggleNames = new List<string>();
            foreach (Page page in preset.Pages)
            {
                foreach (PageItem pageItem in page.Items)
                {
                    if (pageItem.Type == PageItem.ItemType.Toggle)
                    {
                        allToggles.Add(pageItem);

                        // Only add the toggle as a selectable one if it is one not currently in the group.
                        if (pageItem != preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index] && ((item.Item != null && pageItem == item.Item) || Array.IndexOf(preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].GetEnableGroupItems(), pageItem) == -1))
                        {
                            remainingToggles.Add(pageItem);
                            toggleNames.Add(page.name + ": " + pageItem.name);
                        }
                    }
                }
            }

            if (remainingToggles.Count > 0)
            {
                // Set the item to use the first remaining toggle if it has none assigned.
                if (item.Item == null)
                {
                    item.Item = remainingToggles[0];
                }

                // Display a dropdown selector for which toggle the item affects.
                EditorGUI.BeginChangeCheck();
                PageItem selected = allToggles[allToggles.IndexOf(remainingToggles[EditorGUI.Popup(new Rect(rect.x, rect.y + (rect.height - 18f) / 2, (rect.width / 2) - 15f, rect.height), (remainingToggles.IndexOf(item.Item) != -1) ? remainingToggles.IndexOf(item.Item) : 0, toggleNames.ToArray())])];
                if (EditorGUI.EndChangeCheck())
                {
                    // Mark the preset as dirty, record the item, and update it.
                    EditorUtility.SetDirty(preset);
                    Undo.RecordObject(item, "Group Modified");
                    item.Item = selected;
                }
            }
            else
            {
                // Display a placeholder dropdown selector.
                EditorGUI.Popup(new Rect(rect.x, rect.y + (rect.height - 18f) / 2, (rect.width / 2) - 15f, rect.height), 0, new string[] { "None" });
            }

            // Separator
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(rect.x + ((rect.width - 15f) / 2), rect.y), new Vector2(rect.x + ((rect.width - 15f) / 2), rect.y + rect.height));
            if (index != 0)
                Handles.DrawLine(new Vector2(rect.x - 15, rect.y), new Vector2(rect.x + rect.width, rect.y));

            // Display a dropdown selector to change how the item's toggle is affected when this group is fired.
            EditorGUI.BeginChangeCheck();
            GroupItem.GroupType itemType = (GroupItem.GroupType)EditorGUI.EnumPopup(new Rect(rect.x + (rect.width / 2), rect.y + (rect.height - 18f) / 2, rect.width / 2, rect.height), item.Reaction);
            if (EditorGUI.EndChangeCheck())
            {
                // Mark the preset as dirty, record the item, and update it.
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(item, "Group Modified");
                item.Reaction = itemType;
            }
            EditorGUI.indentLevel++;
        };
        enableGroupContents.onAddCallback += (ReorderableList list) =>
        {
            // Accessor string
            string listKey = AssetDatabase.GetAssetPath(preset.Pages[focusedItemPage]) + "/" + preset.Pages[focusedItemPage].name;

            // If no elements are present.
            if (list.count == 0)
            {
                EditorUtility.SetDirty(preset);
                Undo.IncrementCurrentGroup();
                int group2 = Undo.GetCurrentGroup();
                GroupItem item2 = CreateInstance<GroupItem>();
                Undo.RegisterCreatedObjectUndo(item2, "Add Group Item");

                // Configure and add the new item to the array.
                item2.name = "Group Item";
                item2.hideFlags = HideFlags.HideInHierarchy;
                item2.Reaction = GroupItem.GroupType.Enable;
                string _path2 = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                AssetDatabase.AddObjectToAsset(item2, _path2);
                GroupItem[] newArray2 = new GroupItem[preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup.Length + 1];
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup.CopyTo(newArray2, 0);
                newArray2[newArray2.GetUpperBound(0)] = item2;

                Undo.RecordObject(preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index], "Add Group Item");
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup = newArray2;
                Undo.CollapseUndoOperations(group2);
                return;
            }

            // Obtain the number of toggles contained within the preset.
            int totalUsage = 0;
            foreach (Page page in preset.Pages)
            {
                foreach (PageItem pageItem in page.Items)
                {
                    if (pageItem.Type == PageItem.ItemType.Toggle)
                    {
                        totalUsage += 1;
                    }
                }
            }

            // Don't continue if there are no other toggles.
            if (totalUsage - 1 == list.list.Count)
            {
                return;
            }

            // Mark the preset as dirty and record the creation of the new item.
            EditorUtility.SetDirty(preset);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            GroupItem item = CreateInstance<GroupItem>();
            Undo.RegisterCreatedObjectUndo(item, "Add Group Item");

            // Configure the new item, add it to the Asset, and append it to the group.
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            item.name = "Group Item";
            item.hideFlags = HideFlags.HideInHierarchy;
            item.Reaction = GroupItem.GroupType.Enable;
            AssetDatabase.AddObjectToAsset(item, _path);
            GroupItem[] newArray = new GroupItem[preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup.Length + 1];
            preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup.CopyTo(newArray, 0);
            newArray[newArray.GetUpperBound(0)] = item;

            // Record the selected page item and add the new item to the group.
            Undo.RecordObject(preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index], "Add Group Item");
            preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup = newArray;
            Undo.CollapseUndoOperations(group);

            // Focus the list on the new item.
            list.index = list.list.Count;
            enableScroll.y = float.MaxValue;
        };
        enableGroupContents.onRemoveCallback += (ReorderableList list) =>
        {
            // Only continue if the list contains an element.
            if (list.list.Count > 0)
            {
                // Accessor string
                string listKey = AssetDatabase.GetAssetPath(preset.Pages[focusedItemPage]) + "/" + preset.Pages[focusedItemPage].name;

                // Mark the preset as dirty, record the current page item, and set the item to null.
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index], "Remove Group Item");
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup[list.index] = null;

                // Copy the group into an array shortened by one element.
                GroupItem[] newArray = new GroupItem[preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup.GetUpperBound(0)];
                int index = 0;
                for (int i = 0; i < preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup.Length; i++)
                {
                    // Only copy the group item if it isn't null.
                    if (preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup[i] != null)
                    {
                        newArray[index] = preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup[i];
                        index++;
                    }
                }
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup = newArray;

                // Focus the list on the prior item.
                if (list.index > 0)
                    list.index -= 1;
            }
        };

        disableGroupContents = new ReorderableList(null, typeof(GroupItem), true, false, false, false)
        {
            elementHeight = 20f,
            headerHeight = 0,
            footerHeight = 0,
        };
        disableGroupContents.drawHeaderCallback += (Rect rect) =>
        {
            GUI.Label(rect, new GUIContent("When Disabled...", "Modifies listed toggles when this toggle is disabled."));
        };
        disableGroupContents.drawElementCallback += (Rect rect, int index, bool active, bool focused) =>
        {
            EditorGUI.indentLevel--;
            // Accessor string
            string listKey = AssetDatabase.GetAssetPath(preset.Pages[focusedItemPage]) + "/" + preset.Pages[focusedItemPage].name;

            // The item being drawn.
            GroupItem item = preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup[index];

            // Create a list of all toggles, toggles not currently in the group, and the names of those later toggles.
            List<PageItem> allToggles = new List<PageItem>();
            List<PageItem> remainingToggles = new List<PageItem>();
            List<string> toggleNames = new List<string>();
            foreach (Page page in preset.Pages)
            {
                foreach (PageItem pageItem in page.Items)
                {
                    if (pageItem.Type == PageItem.ItemType.Toggle)
                    {
                        allToggles.Add(pageItem);

                        // Only add the toggle as a selectable one if it is one not currently in the group.
                        if (pageItem != preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index] && ((item.Item != null && pageItem == item.Item) || Array.IndexOf(preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].GetDisableGroupItems(), pageItem) == -1))
                        {
                            remainingToggles.Add(pageItem);
                            toggleNames.Add(page.name + ": " + pageItem.name);
                        }
                    }
                }
            }

            if (remainingToggles.Count > 0)
            {
                // Set the item to use the first remaining toggle if it has none assigned.
                if (item.Item == null)
                {
                    item.Item = remainingToggles[0];
                }

                // Display a dropdown selector for which toggle the item affects.
                EditorGUI.BeginChangeCheck();
                PageItem selected = allToggles[allToggles.IndexOf(remainingToggles[EditorGUI.Popup(new Rect(rect.x, rect.y + (rect.height - 18f) / 2, (rect.width / 2) - 15f, rect.height), (remainingToggles.IndexOf(item.Item) != -1) ? remainingToggles.IndexOf(item.Item) : 0, toggleNames.ToArray())])];
                if (EditorGUI.EndChangeCheck())
                {
                    // Mark the preset as dirty, record the item, and update it.
                    EditorUtility.SetDirty(preset);
                    Undo.RecordObject(item, "Group Modified");
                    item.Item = selected;
                }
            }
            else
            {
                // Display a placeholder dropdown selector.
                EditorGUI.Popup(new Rect(rect.x, rect.y + (rect.height - 18f) / 2, (rect.width / 2) - 15f, rect.height), 0, new string[] { "None" });
            }

            // Separator
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(rect.x + ((rect.width - 15f) / 2), rect.y), new Vector2(rect.x + ((rect.width - 15f) / 2), rect.y + rect.height));
            if (index != 0)
                Handles.DrawLine(new Vector2(rect.x - 15, rect.y), new Vector2(rect.x + rect.width, rect.y));

            // Display a dropdown selector to change how the item's toggle is affected when this group is fired.
            EditorGUI.BeginChangeCheck();
            GroupItem.GroupType itemType = (GroupItem.GroupType)EditorGUI.EnumPopup(new Rect(rect.x + (rect.width / 2), rect.y + (rect.height - 18f) / 2, rect.width / 2, rect.height), item.Reaction);
            if (EditorGUI.EndChangeCheck())
            {
                // Mark the preset as dirty, record the item, and update it.
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(item, "Group Modified");
                item.Reaction = itemType;
            }
            EditorGUI.indentLevel++;
        };
        disableGroupContents.onAddCallback += (ReorderableList list) =>
        {
            // Accessor string
            string listKey = AssetDatabase.GetAssetPath(preset.Pages[focusedItemPage]) + "/" + preset.Pages[focusedItemPage].name;

            // If no elements are present.
            if (list.count == 0)
            {
                EditorUtility.SetDirty(preset);
                Undo.IncrementCurrentGroup();
                int group2 = Undo.GetCurrentGroup();
                GroupItem item2 = CreateInstance<GroupItem>();
                Undo.RegisterCreatedObjectUndo(item2, "Add Group Item");

                item2.name = "Group Item";
                item2.hideFlags = HideFlags.HideInHierarchy;
                item2.Reaction = GroupItem.GroupType.Disable;
                string _path2 = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                AssetDatabase.AddObjectToAsset(item2, _path2);
                GroupItem[] newArray2 = new GroupItem[preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup.Length + 1];
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup.CopyTo(newArray2, 0);
                newArray2[newArray2.GetUpperBound(0)] = item2;

                Undo.RecordObject(preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index], "Add Group Item");
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup = newArray2;
                Undo.CollapseUndoOperations(group2);
                return;
            }

            // Obtain the number of toggles contained within the preset.
            int totalUsage = 0;
            foreach (Page page in preset.Pages)
            {
                foreach (PageItem pageItem in page.Items)
                {
                    if (pageItem.Type == PageItem.ItemType.Toggle)
                    {
                        totalUsage += 1;
                    }
                }
            }

            // Don't continue if there are no other toggles.
            if (totalUsage - 1 == list.list.Count)
            {
                return;
            }

            // Mark the preset as dirty and record the creation of the new item.
            EditorUtility.SetDirty(preset);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            GroupItem item = CreateInstance<GroupItem>();
            Undo.RegisterCreatedObjectUndo(item, "Add Group Item");

            // Configure the new item, add it to the Asset, and append it to the group.
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            item.name = "Group Item";
            item.hideFlags = HideFlags.HideInHierarchy;
            item.Reaction = GroupItem.GroupType.Disable;
            AssetDatabase.AddObjectToAsset(item, _path);
            GroupItem[] newArray = new GroupItem[preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup.Length + 1];
            preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup.CopyTo(newArray, 0);
            newArray[newArray.GetUpperBound(0)] = item;

            // Record the selected page item and add the new item to the group.
            Undo.RecordObject(preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index], "Add Group Item");
            preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup = newArray;
            Undo.CollapseUndoOperations(group);

            // Focus the list on the new item.
            list.index = list.list.Count;
            enableScroll.y = float.MaxValue;
        };
        disableGroupContents.onRemoveCallback += (ReorderableList list) =>
        {
            // Only continue if the list contains an element.
            if (list.list.Count > 0)
            {
                // Accessor string
                string listKey = AssetDatabase.GetAssetPath(preset.Pages[focusedItemPage]) + "/" + preset.Pages[focusedItemPage].name;

                // Mark the preset as dirty, record the current page item, and set the item to null.
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index], "Remove Group Item");
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup[list.index] = null;

                // Copy the group into an array shortened by one element.
                GroupItem[] newArray = new GroupItem[preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup.GetUpperBound(0)];
                int index = 0;
                for (int i = 0; i < preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup.Length; i++)
                {
                    // Only copy the group item if it isn't null.
                    if (preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup[i] != null)
                    {
                        newArray[index] = preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup[i];
                        index++;
                    }
                }
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup = newArray;

                // Focus the list on the prior item.
                if (list.index > 0)
                    list.index -= 1;
            }
        };

        EditorApplication.wantsToQuit += WantsToQuit;
    }

    // Remove callbacks to prevent memory leak.
    public void OnDisable()
    {
        if (pageDirectory != null)
        {
            pageDirectory.drawHeaderCallback -= pageDirectory.drawHeaderCallback;
            pageDirectory.drawElementCallback -= pageDirectory.drawElementCallback;
            pageDirectory.onAddCallback -= pageDirectory.onAddCallback;
            pageDirectory.onRemoveCallback -= pageDirectory.onRemoveCallback;
            pageDirectory.onReorderCallbackWithDetails -= pageDirectory.onReorderCallbackWithDetails;
            pageDirectory.elementHeightCallback -= pageDirectory.elementHeightCallback;
            pageDirectory.onMouseDragCallback -= pageDirectory.onMouseDragCallback;
            pageDirectory.onMouseUpCallback -= pageDirectory.onMouseUpCallback;
        }

        foreach (ReorderableList list in pageContentsDict.Values)
            if (list != null)
            {
                list.drawElementCallback -= list.drawElementCallback;
                list.onAddCallback -= list.onAddCallback;
                list.onRemoveCallback -= list.onRemoveCallback;
                list.drawFooterCallback -= list.drawFooterCallback;
            }
        pageContentsDict.Clear();

        if (enableGroupContents != null)
        {
            enableGroupContents.drawHeaderCallback -= enableGroupContents.drawHeaderCallback;
            enableGroupContents.drawElementCallback -= enableGroupContents.drawElementCallback;
            enableGroupContents.onAddCallback -= enableGroupContents.onAddCallback;
            enableGroupContents.onRemoveCallback -= enableGroupContents.onRemoveCallback;
        }

        if (disableGroupContents != null)
        {
            disableGroupContents.drawHeaderCallback -= disableGroupContents.drawHeaderCallback;
            disableGroupContents.drawElementCallback -= disableGroupContents.drawElementCallback;
            disableGroupContents.onAddCallback -= disableGroupContents.onAddCallback;
            disableGroupContents.onRemoveCallback -= disableGroupContents.onRemoveCallback;
        }

        SaveChanges();
        EditorApplication.wantsToQuit -= WantsToQuit;
    }

    //Emergency save if the Editor is closing while a preset is being inspected.
    static bool WantsToQuit()
    {
        AssetDatabase.SaveAssets();
        return true;
    }

    public override void OnInspectorGUI()
    {
        // Debug Inspector GUI
        //base.OnInspectorGUI();

        // Wait until the Asset has been fully created.
        string _hasPath = AssetDatabase.GetAssetPath(preset.GetInstanceID());
        if (_hasPath == "")
        {
            return;
        }

        // Separator rect.
        Rect rect;

        // Check for avatars in the scene.
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        EditorGUILayout.BeginVertical(GUILayout.Height(20f));
        GUILayout.FlexibleSpace();
        SelectAvatarDescriptor();
        if (avatar == null)
        {
            EditorGUILayout.HelpBox("No Avatars found in the current Scene!", MessageType.Warning);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();

        // Check if toggle limit exceeded.
        int totalUsage = 1; // 0 is reserved.

        // Check if an object is missing.
        List<string> objectsMissing = new List<string>();
        List<string> objectsMissingPath = new List<string>();

        // Loop through each item in every Inventory page.
        foreach (Page page in preset.Pages)
        {
            foreach (PageItem pageItem in page.Items)
            {
                if (pageItem.Type == PageItem.ItemType.Toggle)
                {
                    switch (pageItem.Sync)
                    {
                        // If the toggle is local, add one for the menu and one for each group used.
                        case PageItem.SyncMode.Off:
                            totalUsage += 1;
                            if (pageItem.EnableGroup.Length > 0)
                                totalUsage++;
                            if (pageItem.DisableGroup.Length > 0)
                                totalUsage++;
                            break;
                        // If the toggle is manual, add three: one for the menu, being enabled, and being disabled.
                        case PageItem.SyncMode.Manual:
                            totalUsage += 3;
                            break;
                        // If the toggle is auto-synced, add the same as manual, plus one for each group used.
                        case PageItem.SyncMode.Auto:
                            totalUsage += pageItem.Saved ? 1 : 3;
                            if (pageItem.EnableGroup.Length > 0)
                                totalUsage++;
                            if (pageItem.DisableGroup.Length > 0)
                                totalUsage++;
                            break;
                    }
                    // Check for missing items.
                    if (avatar != null && !pageItem.UseAnimations && !pageItem.ObjectReference.Equals("") && avatar.transform.Find(pageItem.ObjectReference) == null)
                    {
                        objectsMissing.Add(pageItem.ObjectReference);
                        objectsMissingPath.Add(page.name + ": " + pageItem.name);
                    }
                }
            }
        }

        // Display missing references.
        if (objectsMissing.Count > 0)
        {
            // Separator
            rect = EditorGUILayout.BeginHorizontal();
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.width + 15, rect.y + 1));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("One or more objects used in this preset were not found on the Avatar.\nYou can view these missing items in the foldout below.", MessageType.Warning);

            // Separator
            rect = EditorGUILayout.BeginHorizontal();
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.width + 15, rect.y + 1));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
            EditorGUI.indentLevel++;
            if (missingFoldout = EditorGUILayout.Foldout(missingFoldout, "Missing Objects", true))
            {
                // Separator
                rect = EditorGUILayout.BeginHorizontal();
                Handles.color = Color.gray;
                Handles.DrawLine(new Vector2(rect.x + 18f, rect.y + 1), new Vector2(rect.width + 15, rect.y + 1));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < objectsMissing.Count; i++)
                {
                    
                    rect = EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("");
                    EditorGUI.LabelField(new Rect(rect.x, rect.y, (rect.x + ((rect.width - 15f) / 2)) - rect.x, rect.height), new GUIContent(objectsMissingPath[i], objectsMissingPath[i]));
                    Handles.color = Color.gray;
                    Handles.DrawLine(new Vector2(rect.x + ((rect.width - 15f) / 2), rect.y), new Vector2(rect.x + ((rect.width - 15f) / 2), rect.y + rect.height));
                    EditorGUI.LabelField(new Rect(rect.x + ((rect.width - 30f) / 2), rect.y, rect.xMax - (rect.x + ((rect.width - 30f) / 2)), rect.height), new GUIContent(objectsMissing[i].IndexOf("/") != 0 ? objectsMissing[i].Substring(objectsMissing[i].LastIndexOf("/") + 1): objectsMissing[i], objectsMissing[i]));
                    EditorGUILayout.EndHorizontal();
                } 
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndVertical();
        DrawLine();

        // Usage bar
        Rect barPos = EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        EditorGUILayout.BeginHorizontal();
        float percentage = Mathf.Clamp((totalUsage - 1) / 255f, 0f, 1f);
        GUIStyle barBackStyle = new GUIStyle(GUI.skin.FindStyle("ProgressBarBack") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("ProgressBarBack"));
        GUIStyle barFrontStyle = new GUIStyle(GUI.skin.FindStyle("ProgressBarBar") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("ProgressBarBar"));
        GUIStyle barTextColor = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            richText = true
        };
        string barText = "Data: " + (totalUsage - 1) + " of 255";
        if (percentage >= 0.85f)
        {
            barFrontStyle.normal.background = barColors[1];
            barTextColor.normal.textColor = Color.white;
            barText = "<b>Data: " + (totalUsage - 1) + " of 255</b>";
        }
        else if (percentage >= 0.7f)
        {
            barFrontStyle.normal.background = barColors[0];
        }
        DoCustomProgressBar(new Rect(barPos.x + 4, barPos.y + 4, barPos.width - 8, 16), percentage, barBackStyle, barFrontStyle);
        EditorGUI.LabelField(new Rect(barPos.x + 4, barPos.y + 4, barPos.width - 8, 16), barText, barTextColor);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(18);
        if (totalUsage > 256)
        {
            EditorGUILayout.HelpBox("This preset exceeds the maximum amount of data usable.", MessageType.Warning);
        }
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        EditorGUI.indentLevel++;
        if (usageFoldout = EditorGUILayout.Foldout(usageFoldout, "How Data is Measured", true))
        {
            EditorGUI.indentLevel--;
            EditorGUILayout.HelpBox("Data usage depends on both sync mode and group usage:\n\nOff = 1 + (1 for each Toggle Group used)\nManual = 3\nAuto = 1 + (2 if the Toggle isn't saved) + (1 for each Toggle Group used)", MessageType.None);
            EditorGUI.indentLevel++;
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        DrawLine();

        // Begin Asset modification.
        serializedObject.Update();
        EditorGUILayout.BeginVertical();

        // Begin page settings section.

        // Update the pageDirectory list if the list was modified.
        if (pageDirectory.list != preset.Pages)
        {
            pageDirectory.list = preset.Pages;
        }  

        // Make sure at least one page exists.
        if (pageDirectory.list.Count == 0)
        {
            Page page = CreateInstance<Page>();
            page.name = "Page " + (pageDirectory.list.Count + 1);
            page.hideFlags = HideFlags.HideInHierarchy;
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            AssetDatabase.AddObjectToAsset(page, _path);
            preset.Pages.Add(page);
            pagesFoldout.Add(false);
        }       

        // Create a list of indicies to use in the dropdown page select.
        string[] pageNames = new string[pageDirectory.list.Count];
        for (int i = 0; i < pageDirectory.list.Count; i++)
        {
            pageNames[i] = (i + 1).ToString();
        }

        // Draw the pageDirectory list (with scrollbar).
        if (pageDirectory != null)
        {
            DrawCustomHeader(pageDirectory);
            directoryScroll = EditorGUILayout.BeginScrollView(directoryScroll, GUILayout.Height(Mathf.Clamp(pageDirectory.GetHeight(), 0, pageDirectory.elementHeight * 20 + 10)));
            pageDirectory.DoLayoutList();
            EditorGUILayout.EndScrollView();
            DrawButtons(pageDirectory, true, preset.Pages.Count > 1, "Create Page", "Remove Page", Rect.zero);
        }
        EditorGUILayout.Space();
        DrawLine();

        // Item Settings

        if (pageDirectory.HasKeyboardControl())
        {
            focusedOnItem = false;
            foreach (ReorderableList list in pageContentsDict.Values)
                list.index = -1;
        }
        else
        {
            if (pageContentsDict.ContainsKey(AssetDatabase.GetAssetPath(preset.Pages[focusedItemPage]) + "/" + preset.Pages[focusedItemPage].name) && pageContentsDict[AssetDatabase.GetAssetPath(preset.Pages[focusedItemPage]) + "/" + preset.Pages[focusedItemPage].name].HasKeyboardControl())
            {
                focusedOnItem = true;
                pageDirectory.index = -1;
                foreach (ReorderableList list in pageContentsDict.Values)
                    if (list != pageContentsDict[AssetDatabase.GetAssetPath(preset.Pages[focusedItemPage]) + "/" + preset.Pages[focusedItemPage].name])
                        list.index = -1;
            }
        }
        // Check that the selected list is available, otherwise wait until it is.
        if (pageContentsDict.ContainsKey(AssetDatabase.GetAssetPath(preset.Pages[focusedItemPage]) + "/" + preset.Pages[focusedItemPage].name) && !draggingPage && focusedOnItem)
        {
            string listKey = AssetDatabase.GetAssetPath(preset.Pages[focusedItemPage]) + "/" + preset.Pages[focusedItemPage].name;

            // Correct index if it has left the list bounds.
            if (pageContentsDict[listKey].index >= pageContentsDict[listKey].list.Count)
            {
                pageContentsDict[listKey].index = pageContentsDict[listKey].list.Count - 1;
            }
            else if (pageContentsDict[listKey].index < 0)
            {
                pageContentsDict[listKey].index = 0;
            }

            // Draw item control container.
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));

            // Other item settings.
            EditorGUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal(GUILayout.Height(5f));
            EditorGUILayout.EndHorizontal();

            // Store the current state of each item to use for each control (less code overall).
            PageItem currentItem = preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index];

            string itemName = currentItem.name;
            Texture2D itemIcon = currentItem.Icon;
            PageItem.ItemType itemType = currentItem.Type;
            bool itemState = currentItem.InitialState;
            Transform itemObject = avatar != null && !currentItem.ObjectReference.Equals("") ? avatar.transform.Find(currentItem.ObjectReference) : null;
            bool itemAnimations = currentItem.UseAnimations;
            AnimationClip itemEnable = currentItem.EnableClip;
            AnimationClip itemDisable = currentItem.DisableClip;
            PageItem.SyncMode itemSync = currentItem.Sync;
            bool itemSaved = currentItem.Saved;
            Page itemPage = currentItem.PageReference;
            VRCExpressionsMenu itemMenu = currentItem.Submenu;          

            // Item type.
            EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft }, GUILayout.Height(24f));
            itemType = (PageItem.ItemType)EditorGUILayout.EnumPopup(new GUIContent("Type", "The type of item."), itemType);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal(GUILayout.Height(5f));
            EditorGUILayout.EndHorizontal();

            // Item icon (only if the item is not of type Page).
            if (itemType != PageItem.ItemType.Page)
            {
                // Separator
                rect = EditorGUILayout.BeginHorizontal();
                Handles.color = Color.gray;
                Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.width + 15, rect.y + 1));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();

                // Draw item renamer.
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Name", "The name of the item."));
                itemName = EditorGUILayout.TextField(itemName, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Icon", "The icon to use for the control."));
                itemIcon = (Texture2D)EditorGUILayout.ObjectField(itemIcon, typeof(Texture2D), false);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }

            // Separator
            rect = EditorGUILayout.BeginHorizontal();
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.width + 15, rect.y + 1));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // Type based settings.
            bool resetReference = false;
            switch (itemType)
            {
                case PageItem.ItemType.Toggle:
                    // Item animation use.
                    EditorGUILayout.BeginHorizontal();
                    itemAnimations = Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(itemAnimations), new string[] { "Game Object", "Animation Clips" }));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")) { padding = new RectOffset(GUI.skin.box.padding.left, GUI.skin.box.padding.right, 5, 5) });
                    if (itemAnimations)
                    {
                        // Item enabled clip.
                        itemEnable = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Enable", "The Animation to play when the toggle is enabled."), itemEnable, typeof(AnimationClip), false);

                        // Item disabled clip.
                        itemDisable = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Disable", "The Animation to play when the toggle is disabled."), itemDisable, typeof(AnimationClip), false);
                    }
                    else
                    {
                        // Item object reference.
                        if (avatar != null)
                        {
                            if (itemObject == null && currentItem.ObjectReference != "")
                            {
                                EditorGUILayout.HelpBox("The Game Object with the path \"" + currentItem.ObjectReference + "\" is not present on the Active Avatar.", MessageType.Warning);
                                EditorGUILayout.BeginHorizontal();
                                resetReference = GUILayout.Button(new GUIContent("Delete Reference", "Remove the reference to the missing Game Object."));
                                EditorGUILayout.EndHorizontal();
                            }                                
                            else
                            {
                                itemObject = (Transform)EditorGUILayout.ObjectField(new GUIContent("Object", "The Game Object that this toggle should affect."), itemObject, typeof(Transform), true);
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox(new GUIContent("An avatar must be present in order to switch objects."));
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.PrefixLabel(new GUIContent("Object Path", "The stored path to the affected Game Object. (Read-Only)"));
                            EditorGUILayout.LabelField(currentItem.ObjectReference, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    EditorGUILayout.EndVertical();

                    // Separator
                    EditorGUILayout.Space();
                    rect = EditorGUILayout.BeginHorizontal();
                    Handles.color = Color.gray;
                    Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.width + 15, rect.y + 1));
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space();

                    // Item starting state.
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(new GUIContent("Start", "What state the toggle starts in."));
                    itemState = Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(itemState), new string[] { "Disable", "Enable" }));
                    EditorGUILayout.EndHorizontal();

                    // Separator
                    EditorGUILayout.Space();
                    rect = EditorGUILayout.BeginHorizontal();
                    Handles.color = Color.gray;
                    Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.width + 15, rect.y + 1));
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space();

                    // Item sync setting.
                    itemSync = (PageItem.SyncMode)EditorGUILayout.EnumPopup(new GUIContent("Sync", "How the toggle should sync with others."), itemSync);
                    if (itemSync == PageItem.SyncMode.Auto)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("Saved", "Whether to save the state of this item when unloading the avatar."));
                        itemSaved = Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(itemSaved), new string[] { "Disable", "Enable" }));
                        EditorGUILayout.EndHorizontal();
                    }            

                    // Like EditorGUILayout.Space(), but smaller.
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.EndVertical();
                    break;
                case PageItem.ItemType.Page:
                    // Check if another page besides the current one exists.
                    if (preset.Pages.Count - 1 > 0)
                    {
                        string[] names = new string[preset.Pages.Count - 1];
                        Page[] pages = new Page[preset.Pages.Count - 1];

                        // Store each page's name and index (excluding the currently selected page).
                        int index = 0;
                        for (int i = 0; i < preset.Pages.Count; i++)
                        {
                            if (i != focusedItemPage)
                            {
                                names[index] = preset.Pages[i].name;
                                pages[index] = preset.Pages[i];
                                index++;
                            }
                        }

                        // Item page reference.
                        itemPage = preset.Pages[preset.Pages.IndexOf(pages[EditorGUILayout.Popup(new GUIContent("Page", "The page to direct to."), itemPage != null ? Array.IndexOf(pages, itemPage) : 0, names)])];
                    }
                    else
                    {
                        // Display an empty list.
                        EditorGUILayout.Popup(new GUIContent("Page", "The page to direct to."), 0, new string[] { "None" });
                    }
                    break;
                case PageItem.ItemType.Submenu:
                    // Item submenu.
                    itemMenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Submenu", "The menu to use."), itemMenu, typeof(VRCExpressionsMenu), false);
                    break;
            }
            EditorGUILayout.EndVertical();

            // Record and update the current item if a setting was changed.
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(currentItem, "Item Modified");

                // Correct the name if it is blank.
                if (itemName == "")
                {
                    itemName = "Slot " + (pageContentsDict[listKey].index + 1);
                }

                // Update the item's values.
                currentItem.name = itemName;
                currentItem.Icon = itemIcon;
                currentItem.Type = itemType;
                currentItem.InitialState = itemState;
                if (!currentItem.UseAnimations && !itemAnimations)
                    currentItem.ObjectReference = avatar != null && (resetReference || avatar.transform.Find(currentItem.ObjectReference) != null && (InventoryInventor.GetGameObjectPath(itemObject).IndexOf(InventoryInventor.GetGameObjectPath(avatar.transform)) != -1) || itemObject == null) ? (resetReference || itemObject == null ? "" : InventoryInventor.GetGameObjectPath(itemObject).Substring(InventoryInventor.GetGameObjectPath(itemObject).IndexOf(InventoryInventor.GetGameObjectPath(avatar.transform)) + InventoryInventor.GetGameObjectPath(avatar.transform).Length + 1)) : currentItem.ObjectReference;
                currentItem.UseAnimations = itemAnimations;
                currentItem.EnableClip = itemEnable;
                currentItem.DisableClip = itemDisable;
                currentItem.Sync = itemSync;
                currentItem.Saved = itemSaved;
                currentItem.PageReference = itemPage;
                currentItem.Submenu = itemMenu;

                // Reassign the item to the list (might be redundant, haven't checked).
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index] = currentItem;
            }
            EditorGUILayout.Space();

            // Item Groups
            if (itemType == PageItem.ItemType.Toggle)
            {
                // Separator
                rect = EditorGUILayout.BeginHorizontal();
                Handles.color = Color.gray;
                Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.width + 15, rect.y + 1));
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel++;
                groupFoldout = EditorGUILayout.Foldout(groupFoldout, "Groups");
                if (groupFoldout)
                {
                    // Update enableGroupContents if it is using the wrong list or it has been modified.
                    if (enableGroupContents.list != preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup)
                    {
                        enableGroupContents.list = preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup;
                    }

                    // If the group is empty, display a button for creating it. Otherwise, display the contents.
                    DrawCustomHeader(enableGroupContents);
                    if (enableGroupContents.list.Count != 0)
                    {
                        // Display the enableGroupContents list (with scrollbar).
                        enableScroll = EditorGUILayout.BeginScrollView(enableScroll, GUILayout.Height(Mathf.Clamp(enableGroupContents.GetHeight(), 0, enableGroupContents.elementHeight * 10 + 10)));
                        enableGroupContents.DoLayoutList();
                        EditorGUILayout.EndScrollView();
                    }
                    DrawButtons(enableGroupContents, true, true, "Create Member", "Remove Member", Rect.zero);

                    // Add some empty space between the two groups.
                    EditorGUILayout.Space();

                    // Repeat for the Disable Group.
                    if (disableGroupContents.list != preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup)
                    {
                        disableGroupContents.list = preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup;
                    }

                    DrawCustomHeader(disableGroupContents);
                    if (disableGroupContents.list.Count != 0)
                    {
                        disableScroll = EditorGUILayout.BeginScrollView(disableScroll, GUILayout.Height(Mathf.Clamp(disableGroupContents.GetHeight(), 0, enableGroupContents.elementHeight * 10 + 10)));
                        disableGroupContents.DoLayoutList();
                        EditorGUILayout.EndScrollView();
                    }
                    DrawButtons(disableGroupContents, true, true, "Create Member", "Remove Member", Rect.zero);
                    EditorGUILayout.Space();
                }
                EditorGUI.indentLevel--;
            }

            // End item settings container.
            EditorGUILayout.EndVertical();
        }
        else if (!focusedOnItem && pageDirectory.index > -1)
        {
            // Draw page control container.
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")) { padding = new RectOffset(GUI.skin.box.padding.left, GUI.skin.box.padding.right, 5, 5) });

            // Draw and check for changes in the page name control.
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Name", "The name of the item."));
            string pageName = EditorGUILayout.TextField(preset.Pages[pageDirectory.index].name, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                // Revert to default name if left blank.
                if (pageName == "")
                {
                    pageName = "Page " + (pageDirectory.index + 1);
                }

                // Deal with pages that share the same name.
                List<string> names = new List<string>();
                foreach (Page page in preset.Pages)
                {
                    if (page != preset.Pages[pageDirectory.index])
                        names.Add(page.name);
                }
                if (names.Contains(pageName))
                {
                    int occurance = 0;
                    while (names.Contains(pageName + " " + occurance))
                    {
                        occurance++;
                    }
                    pageName = pageName + " " + occurance;
                }

                // Record the current page name and update it.
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(preset.Pages[pageDirectory.index], "Page Modified");
                preset.Pages[pageDirectory.index].name = pageName;
            }

            // Other page settings.
            EditorGUILayout.BeginVertical();

            // Page icon.
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Icon", "Icon to use for the control."));
            EditorGUI.BeginChangeCheck();
            Texture2D pageIcon = (Texture2D)EditorGUILayout.ObjectField(preset.Pages[pageDirectory.index].Icon, typeof(Texture2D), false);
            EditorGUILayout.EndHorizontal();

            // Record the current state of the page and update any modified values.
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(preset.Pages[pageDirectory.index], "Page Modified");
                preset.Pages[pageDirectory.index].Icon = pageIcon;
            }
            EditorGUILayout.EndVertical();

            // Close page setting container.
            EditorGUILayout.EndVertical();
        }

        // Apply changes to the main Asset.
        EditorGUILayout.EndVertical();
        serializedObject.ApplyModifiedProperties();

        // Save Changes before entering Play Mode
        if (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
        {
            SaveChanges();
        }
    }

    // Removes unused Sub-Assets from the file and saves any changes made to the remaining Assets.
    private void SaveChanges()
    {
        // Save any changes made to Assets within the file.
        AssetDatabase.SaveAssets();

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
                        if (Array.IndexOf(item.EnableGroup, (GroupItem)objects[i]) != -1 || Array.IndexOf(item.DisableGroup, (GroupItem)objects[i]) != -1)
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
    }

    // Draws a line across the GUI
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

    /*
    // Reorderable List Code
    */

    // Modified version of Unity's Header Drawing code for ReorderableList
    private void DrawCustomHeader(ReorderableList list)
    {
        // Obtain the Rect for the header.
        Rect rect = GUILayoutUtility.GetRect(0, defaultHeaderHeight, GUILayout.ExpandWidth(true));
        
        GUIStyle headerBackground = "RL Header";

        // Draw the header background on Repaint Events.
        if (Event.current.type == EventType.Repaint)
            headerBackground.Draw(rect, false, false, false, false);

        // Apply padding to the rect.
        rect.xMin += ReorderableList.Defaults.padding;
        rect.xMax -= ReorderableList.Defaults.padding;
        rect.height -= 2;
        rect.y += 1;

        // Invoke the drawHeaderCallback using the rect.
        list.drawHeaderCallback?.Invoke(rect);

        // If there is no callback provided, the default is not drawn.
    }

    // Modified version of Unity's Button Drawing code for ReorderableList.
    private void DrawButtons(ReorderableList list, bool displayAdd, bool displayRemove, string addText, string removeText, Rect given)
    {
        // Obtain the Rect for the footer.
        Rect rect = given != Rect.zero ? given : GUILayoutUtility.GetRect(4, defaultFooterHeight, GUILayout.ExpandWidth(true));

        // Button contents.
        //GUIContent iconToolbarPlus = EditorGUIUtility.TrIconContent("Toolbar Plus", "Add to list");
        //GUIContent iconToolbarPlusMore = EditorGUIUtility.TrIconContent("Toolbar Plus More", "Choose to add to list");
        //GUIContent iconToolbarMinus = EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove selection from list");

        GUIStyle preButton = "RL FooterButton";
        GUIStyle newButton = new GUIStyle(preButton);
        newButton.normal.textColor = Color.Lerp(Color.black, Color.grey, 0.65f);
        GUIStyle footerBackground = "RL Footer";

        // Modify the footer rect for the two buttons.
        float rightEdge = rect.xMax;
        float leftEdge = rect.xMin;
        //if (displayAdd)
        //    leftEdge -= 25;
        //if (displayRemove)
        //    leftEdge -= 25;
        rect = new Rect(leftEdge, rect.y, rightEdge - leftEdge, rect.height);

        // Get Rects for each button.
        Rect addRect = new Rect(leftEdge + 4, rect.y - 3, rect.width / 2, 13);
        Rect removeRect = new Rect(rightEdge - 4 - (rect.width / 2), rect.y - 3, rect.width / 2, 13);
        
        // Draw the background for the footer on Repaint Events.
        if (Event.current.type == EventType.Repaint)
        {
            footerBackground.Draw(rect, false, false, false, false);
            // Separator
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(rect.xMin + rect.width / 2, rect.yMin), new Vector2(rect.xMin + rect.width / 2, rect.yMin + defaultFooterHeight));
        }
        
        // Makes button unable to be used while conditions are met.
        using (new EditorGUI.DisabledScope(!displayAdd ||
            (list.onCanAddCallback != null && !list.onCanAddCallback(list))))
        {
            // Invoke the onAddCallback when the button is clicked followed by onChangedCallback.
            if (GUI.Button(addRect, addText, new GUIStyle(newButton)))
            {
                if (list.onAddDropdownCallback != null)
                    list.onAddDropdownCallback(addRect, list);
                else
                    list.onAddCallback?.Invoke(list);

                list.onChangedCallback?.Invoke(list);

                // If neither callback was provided, nothing will happen when the button is clicked.
            }
        }

        // Exact same as above, just with the other button and removal callbacks.
        using (new EditorGUI.DisabledScope(
            list.index < 0 || list.index >= list.count || !displayRemove ||
            (list.onCanRemoveCallback != null && !list.onCanRemoveCallback(list))))
        {
            if (GUI.Button(removeRect, removeText, newButton))
            {
                list.onRemoveCallback?.Invoke(list);

                list.onChangedCallback?.Invoke(list);

                // If neither callback was provided, nothing will happen when the button is clicked.
            }
        }     
    }

    /*
    // Custom Progress Bar
    */

    private void DoCustomProgressBar(Rect position, float value, GUIStyle progressBarBackgroundStyle, GUIStyle progressBarStyle)
    {
        bool mouseHover = position.Contains(Event.current.mousePosition);
        int id = GUIUtility.GetControlID("s_ProgressBarHash".GetHashCode(), FocusType.Keyboard, position);
        Event evt = Event.current;
        switch (evt.GetTypeForControl(id))
        {
            case EventType.Repaint:
                progressBarBackgroundStyle.Draw(position, mouseHover, false, false, false);
                if (value > 0.0f)
                {
                    value = Mathf.Clamp01(value);
                    var barRect = new Rect(position);
                    barRect.width *= value;
                    if (barRect.width >= 1f)
                        progressBarStyle.Draw(barRect, GUIContent.none, mouseHover, false, false, false);
                }
                else if (value == -1.0f)
                {
                    float barWidth = position.width * 0.2f;
                    float halfBarWidth = barWidth / 2.0f;
                    float cos = Mathf.Cos((float)EditorApplication.timeSinceStartup * 2f);
                    float rb = position.x + halfBarWidth;
                    float re = position.xMax - halfBarWidth;
                    float scale = (re - rb) / 2f;
                    float cursor = scale * cos;
                    var barRect = new Rect(position.x + cursor + scale, position.y, barWidth, position.height);
                    progressBarStyle.Draw(barRect, GUIContent.none, mouseHover, false, false, false);
                }
                break;
        }
    }

    /*
     * These next two functions are literally just code from the Expression Menu for selecting the avatar.
     */

    void SelectAvatarDescriptor()
    {
        var descriptors = FindObjectsOfType<VRCAvatarDescriptor>();
        if (descriptors.Length > 0)
        {
            //Compile list of names
            string[] names = new string[descriptors.Length];
            for (int i = 0; i < descriptors.Length; i++)
                names[i] = descriptors[i].gameObject.name;

            //Select
            var currentIndex = Array.IndexOf(descriptors, avatar);
            var nextIndex = EditorGUILayout.Popup(new GUIContent("Active Avatar", "The Avatar you want to manage an inventory for."), currentIndex, names);
            if (nextIndex < 0)
                nextIndex = 0;
            if (nextIndex != currentIndex)
                SelectAvatarDescriptor(descriptors[nextIndex]);
        }
        else
            SelectAvatarDescriptor(null);
    }
    void SelectAvatarDescriptor(VRCAvatarDescriptor desc)
    {
        if (desc == avatar)
            return;

        avatar = desc;
    }

}