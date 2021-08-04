using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Avatars.Components;
using InventoryInventor.Preset;
using InventoryInventor;

[CustomEditor(typeof(InventoryPreset))]
public class InventoryPresetEditor : Editor
{
    // The Asset being edited.
    private InventoryPreset preset;

    // The Avatar being configured.
    private VRCAvatarDescriptor avatar;
    private ControlDrawer controlDrawer;

    // Reorderable Lists to be displayed.
    private readonly Dictionary<string, ReorderableList> pageContentsDict = new Dictionary<string, ReorderableList>();
    private ReorderableList pageDirectory;
    private ReorderableList enableGroupContents;
    private ReorderableList disableGroupContents;
    private ReorderableList buttonGroupContents;

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

    // Static values
    private static readonly GUIContent[] typePopupName = new GUIContent[4] { new GUIContent("Toggle"), new GUIContent("Button"), new GUIContent("Subpage"), new GUIContent("Control") };
    private static readonly int[] typePopupVal = new int[4] { 0, 3, 1, 2 };
    private static readonly GUIContent[] reactPopupName = new GUIContent[2] { new GUIContent("Enable"), new GUIContent("Disable") };
    private static readonly int[] reactPopupVal = new int[2] { 1, 0 };

    // Get the targeted object and initialize ReorderableLists.
    public void OnEnable()
    {
        preset = (InventoryPreset)target;

        // Wait until the Asset has been fully created if it hasn't already.
        string _hasPath = AssetDatabase.GetAssetPath(preset.GetInstanceID());
        if (_hasPath == "")
        {
            return;
        }

        // Upgrade the Preset if an older version was detected.
        if (preset.Pages.Count == 0)
            preset.Version = InventoryPresetUtility.currentVersion;
        else if (preset.Version < InventoryPresetUtility.currentVersion)
            InventoryPresetUtility.UpgradePreset(preset);

        // Setup some GUI variables.
        pagesFoldout.AddRange(new bool[preset.Pages.Count]);

        // Setup bar colors.
        barColors = new Texture2D[3] { new Texture2D(1, 1), new Texture2D(1, 1), new Texture2D(1, 1) };
        barColors[0].SetPixel(0, 0, Color.yellow * new Color(1, 1, 1, 0.9f));
        barColors[0].Apply();
        barColors[1].SetPixel(0, 0, Color.red * new Color(1, 1, 1, 0.9f));
        barColors[1].Apply();
        barColors[2].SetPixel(0, 0, Color.green * new Color(1, 1, 1, 0.9f));
        barColors[2].Apply();

        // Initialize Control Drawer.
        controlDrawer = new ControlDrawer();

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
            for (int i = 0; i <= index && i < preset.Pages.Count; i++)
            {
                if (preset.Pages[i] != item)
                    names.Add(preset.Pages[i].name);
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
            string displayedName = item.name;
            Vector2 nameSize = new GUIStyle().CalcSize(new GUIContent(displayedName + ((index == 0) ? " (Default)" : "")));
            int width = displayedName.Length;
            while (nameSize.x > rect.width - 15 && width > 1)
            {
                displayedName = displayedName.Substring(0, width) + "...";
                nameSize = new GUIStyle().CalcSize(new GUIContent(displayedName + ((index == 0) ? " (Default)" : "")));
                width--;
            }
            Rect foldoutRect = new Rect(rect.x + 8, rect.y + EditorGUIUtility.singleLineHeight / 4, 0f, 18f);
            pagesFoldout[index] = EditorGUI.Foldout(foldoutRect, pagesFoldout[index], displayedName + ((index == 0) ? " (Default)" : ""));

            // Show Context Menu
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
            {
                ShowPageContextMenu(index);
            }

            if (pagesFoldout[index] && !draggingPage)
            {
                EditorGUI.indentLevel++;
                string listKey = item.GetInstanceID().ToString();
                ReorderableList innerList;
                if (pageContentsDict.ContainsKey(listKey))
                {
                    // fetch the reorderable list in dict
                    innerList = pageContentsDict[listKey];

                    // Reassign the list if it doesn't match for some reason.
                    if (innerList.list != preset.Pages[index].Items)
                        innerList.list = preset.Pages[index].Items;
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
                        if (focused2)
                        {
                            focusedItemPage = index;
                        }

                        // The item being drawn.
                        PageItem item2 = item.Items[index2];

                        // Draw the item's name and type.
                        EditorGUI.indentLevel--;
                        string displayedName2 = item2.name;
                        Vector2 nameSize2 = new GUIStyle().CalcSize(new GUIContent(displayedName2));
                        int width2 = displayedName2.Length;
                        while (nameSize2.x > rect2.width / 2 - 15 && width2 > 1)
                        {
                            displayedName2 = displayedName2.Substring(0, width2) + "...";
                            nameSize2 = new GUIStyle().CalcSize(new GUIContent(displayedName2));
                            width2--;
                        }
                        EditorGUI.LabelField(new Rect(rect2.x, rect2.y, rect2.width / 2, rect2.height), displayedName2);
                        string typeString = "";
                        switch (item2.Type)
                        {
                            case PageItem.ItemType.Toggle:
                                typeString = "Toggle";
                                break;
                            case PageItem.ItemType.Subpage:
                                typeString = "Subpage";
                                break;
                            case PageItem.ItemType.Control:
                                typeString = "Control";
                                break;
                            case PageItem.ItemType.Button:
                                typeString = "Button";
                                break;
                        }
                        EditorGUI.LabelField(new Rect(rect2.x + (rect2.width / 2), rect2.y, rect2.width / 2, rect2.height), "Type: " + typeString);
                        Handles.color = Color.gray;
                        Handles.DrawLine(new Vector2(rect2.x + ((rect2.width - 15f) / 2), rect2.y), new Vector2(rect2.x + ((rect2.width - 15f) / 2), rect2.y + rect2.height));
                        if (index2 != 0)
                            Handles.DrawLine(new Vector2(rect2.x - 15, rect2.y), new Vector2(rect2.x + rect2.width, rect2.y));
                        EditorGUI.indentLevel++;

                        // Show Context Menu
                        if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect2.Contains(Event.current.mousePosition))
                        {
                            ShowItemContextMenu(index, index2);
                        }
                    };
                    innerList.drawNoneElementCallback += (Rect rect2) =>
                    {
                        EditorGUI.LabelField(rect2, "Page is Empty");
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
                        // Only continue if there is at least a single item on the page.
                        if (list2.list.Count > 0)
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
                        DrawButtons(innerList, item.Items.Count < 8, item.Items.Count > 0, false, "Create Item", "Remove Item", footerRect);
                    };

                    pageContentsDict[listKey] = innerList;
                }

                // Determine whether or not to display the add or remove buttons this Repaint.
                Rect innerRect = new Rect(rect.x, rect.y + 18f + EditorGUIUtility.singleLineHeight / 4, rect.width, rect.height);
                innerList.DoList(innerRect);

                EditorGUI.indentLevel--;
            }
            else if (pageContentsDict.ContainsKey(item.GetInstanceID().ToString()))
                pageContentsDict.Remove(item.GetInstanceID().ToString());

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
                propertyHeight += propertyHeight * Mathf.Clamp(preset.Pages[index].Items.Count, 1, float.MaxValue) + 36f;
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

            // Create an intial item to add on the page to avoid errors.
            PageItem item = CreateInstance<PageItem>();
            Undo.RegisterCreatedObjectUndo(item, "Add Page");

            // Configure the new item and add it to the Asset.
            item.hideFlags = HideFlags.HideInHierarchy;
            item.name = "Slot 1";
            AssetDatabase.AddObjectToAsset(item, _path);
            page.Items.Add(item);

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
                string pageName = preset.Pages[list.index].name;
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

            // Show Context Menu
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
            {
                string listKey = preset.Pages[focusedItemPage].GetInstanceID().ToString();
                ShowGroupContextMenu(focusedItemPage, pageContentsDict[listKey].index, 0);
            }
        };
        enableGroupContents.drawElementCallback += (Rect rect, int index, bool active, bool focused) =>
        {
            EditorGUI.indentLevel--;
            // Accessor string
            string listKey = preset.Pages[focusedItemPage].GetInstanceID().ToString();

            // The item being drawn.
            GroupItem item = preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].EnableGroup[index];

            // Create a list of all toggles, toggles not currently in the group, and the names of those later toggles.
            List<PageItem> allToggles = new List<PageItem>();
            List<PageItem> remainingToggles = new List<PageItem>();
            List<string> toggleNames = new List<string>() { "None" };
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
            // Display a dropdown selector for which toggle the item affects.
            EditorGUI.BeginChangeCheck();
            int itemIndex = EditorGUI.Popup(new Rect(rect.x, rect.y + (rect.height - 18f) / 2, (rect.width / 2) - 15f, rect.height), (remainingToggles.IndexOf(item.Item) != -1) ? remainingToggles.IndexOf(item.Item) + 1 : 0, toggleNames.ToArray());
            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                PageItem selected = null;
                if (itemIndex > 0)
                    selected = allToggles[allToggles.IndexOf(remainingToggles[itemIndex - 1])];

                // Mark the preset as dirty, record the item, and update it.
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(item, "Group Modified");
                item.Item = selected;
            }

            // Separator
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(rect.x + ((rect.width - 15f) / 2), rect.y), new Vector2(rect.x + ((rect.width - 15f) / 2), rect.y + rect.height));
            if (index != 0)
                Handles.DrawLine(new Vector2(rect.x - 15, rect.y - 1f), new Vector2(rect.x + rect.width, rect.y - 1f));

            // Display a dropdown selector to change how the item's toggle is affected when this group is fired.
            EditorGUI.BeginChangeCheck();
            GroupItem.GroupType itemType = (GroupItem.GroupType)EditorGUI.IntPopup(new Rect(rect.x + (rect.width / 2), rect.y + (rect.height - 18f) / 2, rect.width / 2, rect.height), (int)item.Reaction, reactPopupName, reactPopupVal);
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
            string listKey = preset.Pages[focusedItemPage].GetInstanceID().ToString();

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
                string listKey = preset.Pages[focusedItemPage].GetInstanceID().ToString();

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

            // Show Context Menu
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
            {
                string listKey = preset.Pages[focusedItemPage].GetInstanceID().ToString();
                ShowGroupContextMenu(focusedItemPage, pageContentsDict[listKey].index, 1);
            }
        };
        disableGroupContents.drawElementCallback += (Rect rect, int index, bool active, bool focused) =>
        {
            EditorGUI.indentLevel--;
            // Accessor string
            string listKey = preset.Pages[focusedItemPage].GetInstanceID().ToString();

            // The item being drawn.
            GroupItem item = preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].DisableGroup[index];

            // Create a list of all toggles, toggles not currently in the group, and the names of those later toggles.
            List<PageItem> allToggles = new List<PageItem>();
            List<PageItem> remainingToggles = new List<PageItem>();
            List<string> toggleNames = new List<string>() { "None" };
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
            // Display a dropdown selector for which toggle the item affects.
            EditorGUI.BeginChangeCheck();
            int itemIndex = EditorGUI.Popup(new Rect(rect.x, rect.y + (rect.height - 18f) / 2, (rect.width / 2) - 15f, rect.height), (remainingToggles.IndexOf(item.Item) != -1) ? remainingToggles.IndexOf(item.Item) + 1 : 0, toggleNames.ToArray());
            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                PageItem selected = null;
                if (itemIndex > 0)
                    selected = allToggles[allToggles.IndexOf(remainingToggles[itemIndex - 1])];

                // Mark the preset as dirty, record the item, and update it.
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(item, "Group Modified");
                item.Item = selected;
            }

            // Separator
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(rect.x + ((rect.width - 15f) / 2), rect.y), new Vector2(rect.x + ((rect.width - 15f) / 2), rect.y + rect.height));
            if (index != 0)
                Handles.DrawLine(new Vector2(rect.x - 15, rect.y - 1f), new Vector2(rect.x + rect.width, rect.y - 1f));

            // Display a dropdown selector to change how the item's toggle is affected when this group is fired.
            EditorGUI.BeginChangeCheck();
            GroupItem.GroupType itemType = (GroupItem.GroupType)EditorGUI.IntPopup(new Rect(rect.x + (rect.width / 2), rect.y + (rect.height - 18f) / 2, rect.width / 2, rect.height), (int)item.Reaction, reactPopupName, reactPopupVal);
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
            string listKey = preset.Pages[focusedItemPage].GetInstanceID().ToString();

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
                string listKey = preset.Pages[focusedItemPage].GetInstanceID().ToString();

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

        buttonGroupContents = new ReorderableList(null, typeof(GroupItem), true, false, false, false)
        {
            elementHeight = 20f,
            headerHeight = 0,
            footerHeight = 0,
        };
        buttonGroupContents.drawHeaderCallback += (Rect rect) =>
        {
            GUI.Label(rect, new GUIContent("When Activated...", "Modifies listed toggles when this button is used."));

            // Show Context Menu
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
            {
                string listKey = preset.Pages[focusedItemPage].GetInstanceID().ToString();
                ShowGroupContextMenu(focusedItemPage, pageContentsDict[listKey].index, 2);
            }
        };
        buttonGroupContents.drawElementCallback += (Rect rect, int index, bool active, bool focused) =>
        {
            EditorGUI.indentLevel--;
            // Accessor string
            string listKey = preset.Pages[focusedItemPage].GetInstanceID().ToString();

            // The item being drawn.
            GroupItem item = preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup[index];

            // Create a list of all toggles, toggles not currently in the group, and the names of those later toggles.
            List<PageItem> allToggles = new List<PageItem>();
            List<PageItem> remainingToggles = new List<PageItem>();
            List<string> toggleNames = new List<string>() { "None" };
            foreach (Page page in preset.Pages)
            {
                foreach (PageItem pageItem in page.Items)
                {
                    if (pageItem.Type == PageItem.ItemType.Toggle)
                    {
                        allToggles.Add(pageItem);

                        // Only add the toggle as a selectable one if it is one not currently in the group.
                        if (pageItem != preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index] && ((item.Item != null && pageItem == item.Item) || Array.IndexOf(preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].GetButtonGroupItems(), pageItem) == -1))
                        {
                            remainingToggles.Add(pageItem);
                            toggleNames.Add(page.name + ": " + pageItem.name);
                        }
                    }
                }
            }
            // Display a dropdown selector for which toggle the item affects.
            EditorGUI.BeginChangeCheck();
            int itemIndex = EditorGUI.Popup(new Rect(rect.x, rect.y + (rect.height - 18f) / 2, (rect.width / 2) - 15f, rect.height), (remainingToggles.IndexOf(item.Item) != -1) ? remainingToggles.IndexOf(item.Item) + 1 : 0, toggleNames.ToArray());
            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                PageItem selected = null;
                if (itemIndex > 0)
                    selected = allToggles[allToggles.IndexOf(remainingToggles[itemIndex - 1])];

                // Mark the preset as dirty, record the item, and update it.
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(item, "Group Modified");
                item.Item = selected;
            }

            // Separator
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(rect.x + ((rect.width - 15f) / 2), rect.y), new Vector2(rect.x + ((rect.width - 15f) / 2), rect.y + rect.height));
            if (index != 0)
                Handles.DrawLine(new Vector2(rect.x - 15, rect.y - 1f), new Vector2(rect.x + rect.width, rect.y - 1f));

            // Display a dropdown selector to change how the item's toggle is affected when this group is fired.
            EditorGUI.BeginChangeCheck();
            GroupItem.GroupType itemType = (GroupItem.GroupType)EditorGUI.IntPopup(new Rect(rect.x + (rect.width / 2), rect.y + (rect.height - 18f) / 2, rect.width / 2, rect.height), (int)item.Reaction, reactPopupName, reactPopupVal);
            if (EditorGUI.EndChangeCheck())
            {
                // Mark the preset as dirty, record the item, and update it.
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(item, "Group Modified");
                item.Reaction = itemType;
            }
            EditorGUI.indentLevel++;
        };
        buttonGroupContents.onAddCallback += (ReorderableList list) =>
        {
            // Accessor string
            string listKey = preset.Pages[focusedItemPage].GetInstanceID().ToString();

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
                GroupItem[] newArray2 = new GroupItem[preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup.Length + 1];
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup.CopyTo(newArray2, 0);
                newArray2[newArray2.GetUpperBound(0)] = item2;

                Undo.RecordObject(preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index], "Add Group Item");
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup = newArray2;
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
            if (totalUsage == list.list.Count)
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
            GroupItem[] newArray = new GroupItem[preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup.Length + 1];
            preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup.CopyTo(newArray, 0);
            newArray[newArray.GetUpperBound(0)] = item;

            // Record the selected page item and add the new item to the group.
            Undo.RecordObject(preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index], "Add Group Item");
            preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup = newArray;
            Undo.CollapseUndoOperations(group);

            // Focus the list on the new item.
            list.index = list.list.Count;
            enableScroll.y = float.MaxValue;
        };
        buttonGroupContents.onRemoveCallback += (ReorderableList list) =>
        {
            // Only continue if the list contains an element.
            if (list.list.Count > 0)
            {
                // Accessor string
                string listKey = preset.Pages[focusedItemPage].GetInstanceID().ToString();

                // Mark the preset as dirty, record the current page item, and set the item to null.
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index], "Remove Group Item");
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup[list.index] = null;

                // Copy the group into an array shortened by one element.
                GroupItem[] newArray = new GroupItem[preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup.GetUpperBound(0)];
                int index = 0;
                for (int i = 0; i < preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup.Length; i++)
                {
                    // Only copy the group item if it isn't null.
                    if (preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup[i] != null)
                    {
                        newArray[index] = preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup[i];
                        index++;
                    }
                }
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup = newArray;

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

        if (buttonGroupContents != null)
        {
            buttonGroupContents.drawHeaderCallback -= buttonGroupContents.drawHeaderCallback;
            buttonGroupContents.drawElementCallback -= buttonGroupContents.drawElementCallback;
            buttonGroupContents.onAddCallback -= buttonGroupContents.onAddCallback;
            buttonGroupContents.onRemoveCallback -= buttonGroupContents.onRemoveCallback;
        }

        if (EditorUtility.IsDirty(preset.GetInstanceID()))
            InventoryPresetUtility.SaveChanges(preset);
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
        int savedUsage = 0;

        // Check if memory is available.
        int totalToggles = 0;
        int totalMemory = (avatar != null && avatar.expressionParameters != null && avatar.expressionParameters.FindParameter("Inventory") == null) ? 8 : 0;
        totalMemory += (avatar != null && avatar.expressionParameters != null && avatar.expressionParameters.FindParameter("Inventory Loaded") == null) ? 1 : 0;

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
                    totalToggles++;
                    switch (pageItem.Sync)
                    {
                        // If the toggle is local, add one for the menu and one for each group used.
                        case PageItem.SyncMode.Off:
                            totalUsage += 1;
                            if (pageItem.EnableGroup.Length > 0)
                                totalUsage++;
                            if (pageItem.DisableGroup.Length > 0)
                                totalUsage++;
                            if (pageItem.Saved)
                            {
                                savedUsage++;
                                if (avatar != null)
                                {
                                    if (avatar.expressionParameters != null && avatar.expressionParameters.FindParameter("Inventory " + totalToggles) != null)
                                        totalMemory--;
                                    totalMemory++;
                                }
                            }
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
                            if (pageItem.Saved)
                            {
                                savedUsage++;
                                if (avatar != null)
                                {
                                    if (avatar.expressionParameters != null && avatar.expressionParameters.FindParameter("Inventory " + totalToggles) != null)
                                        totalMemory--;
                                    totalMemory++;
                                }
                            }
                            break;
                    }
                    // Check for missing items.
                    if (avatar != null && !pageItem.UseAnimations && !pageItem.ObjectReference.Equals("") && avatar.transform.Find(pageItem.ObjectReference) == null)
                    {
                        objectsMissing.Add(pageItem.ObjectReference);
                        objectsMissingPath.Add(page.name + ": " + pageItem.name);
                    }
                }
                else if (pageItem.Type == PageItem.ItemType.Button)
                    totalUsage++;
            }
        }

        // Correct lists if menus have been imported
        if (pagesFoldout.Count != preset.Pages.Count)
            pagesFoldout.AddRange(new bool[preset.Pages.Count - pagesFoldout.Count]);

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

            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("HelpBox")));
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
                    EditorGUI.LabelField(new Rect(rect.x + ((rect.width - 30f) / 2), rect.y, rect.xMax - (rect.x + ((rect.width - 30f) / 2)), rect.height), new GUIContent(objectsMissing[i].IndexOf("/") != 0 ? objectsMissing[i].Substring(objectsMissing[i].LastIndexOf("/") + 1) : objectsMissing[i], objectsMissing[i]));
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        DrawLine();

        // Usage bar
        Rect barPos = EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        EditorGUILayout.BeginHorizontal();
        float togglePercentage = Mathf.Clamp((totalUsage - 1) / 255f, 0f, 1f);
        float savedPercentage = Mathf.Clamp(savedUsage / 120f, 0f, 1f);
        GUIStyle barBackStyle = new GUIStyle(GUI.skin.FindStyle("ProgressBarBack") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("ProgressBarBack"));
        GUIStyle barFrontStyle = new GUIStyle(GUI.skin.FindStyle("ProgressBarBar") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("ProgressBarBar"));
        GUIStyle barUnderStyle = new GUIStyle(GUI.skin.FindStyle("ProgressBarBar") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("ProgressBarBar"));
        if (savedUsage > 120)
        {
            barUnderStyle.normal.background = barColors[1];
            barUnderStyle.normal.scaledBackgrounds = new Texture2D[] { barColors[1] };
        }
        else if (avatar != null && avatar.expressionParameters != null &&  avatar.expressionParameters.CalcTotalCost() + totalMemory > VRCExpressionParameters.MAX_PARAMETER_COST)
        {
            barUnderStyle.normal.background = barColors[0];
            barUnderStyle.normal.scaledBackgrounds = new Texture2D[] { barColors[0] };
        }
        else
        {
            barUnderStyle.normal.background = barColors[2];
            barUnderStyle.normal.scaledBackgrounds = new Texture2D[] { barColors[2] };
        }
        GUIStyle barTextColor = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            richText = true
        };
        string barText = "Data: " + (totalUsage - 1) + " of 255";
        if (togglePercentage >= 0.85f)
        {
            barFrontStyle.normal.background = barColors[1];
            barFrontStyle.normal.scaledBackgrounds = new Texture2D[] { barColors[1] };
            barTextColor.normal.textColor = Color.white;
            barText = "<b>Data: " + (totalUsage - 1) + " of 255</b>";
        }
        else if (togglePercentage >= 0.7f)
        {
            barFrontStyle.normal.background = barColors[0];
            barFrontStyle.normal.scaledBackgrounds = new Texture2D[] { barColors[0] };
        }
        DoCustomProgressBar(new Rect(barPos.x + 4, barPos.y + 4, barPos.width - 8, 16), togglePercentage, savedPercentage, barBackStyle, barFrontStyle, barUnderStyle);
        EditorGUI.LabelField(new Rect(barPos.x + 4, barPos.y + 4, barPos.width - 8, 16), barText, barTextColor);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(24);
        if (totalUsage > 256)
            EditorGUILayout.HelpBox("This preset exceeds the maximum amount of data usable.", MessageType.Error);
        if (savedUsage + 8 > VRCExpressionParameters.MAX_PARAMETER_COST)
            EditorGUILayout.HelpBox("This preset uses more memory than the amount possible available on an Avatar (" + (savedUsage + 8) + "/" + 128 + " bits).", MessageType.Error);
        else if (avatar != null && avatar.expressionParameters != null && avatar.expressionParameters.CalcTotalCost() + totalMemory > VRCExpressionParameters.MAX_PARAMETER_COST)
            EditorGUILayout.HelpBox("This preset uses more memory than is available on the Active Avatar (" + totalMemory + "/" + (VRCExpressionParameters.MAX_PARAMETER_COST - avatar.expressionParameters.CalcTotalCost()) + " bits).", MessageType.Warning);
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("HelpBox")));
        EditorGUI.indentLevel++;
        if (usageFoldout = EditorGUILayout.Foldout(usageFoldout, "What Uses Data and Memory?", true))
        {
            EditorGUI.indentLevel--;
            EditorGUILayout.HelpBox(
                "The usage of Data (d) and Memory (m) is listed below:\n" +
                "\n" +
                "Toggle:\n" +
                "       Sync: OFF\t= 1d\n" +
                "\tGroup\t= (# of Groups)d\n" +
                "       Sync: MANUAL\t= 3d\n" +
                "       Sync: AUTO\t= 3d\n" +
                "\tGroup\t= (# of Groups)d\n" +
                "\tSaved\t= -2d & 1m\n" +
                "\n" +
                "Button = 1d",
                MessageType.None);
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
            EditorUtility.SetDirty(preset);
            Page page = CreateInstance<Page>();
            page.name = "Page " + (pageDirectory.list.Count + 1);
            page.hideFlags = HideFlags.HideInHierarchy;
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            AssetDatabase.AddObjectToAsset(page, _path);
            PageItem item = CreateInstance<PageItem>();
            item.hideFlags = HideFlags.HideInHierarchy;
            item.name = "Slot 1";
            AssetDatabase.AddObjectToAsset(item, _path);
            page.Items.Add(item);
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
            DrawButtons(pageDirectory, true, preset.Pages.Count > 1, true, "Create Page", "Remove Page", Rect.zero);
        }
        EditorGUILayout.Space();
        DrawLine();

        // Item Settings

        // Make sure that the main indexer is within its bounds.
        if (focusedItemPage < 0)
            focusedItemPage = 0;
        else if (focusedItemPage >= preset.Pages.Count)
            focusedItemPage = preset.Pages.Count - 1;

        if (pageDirectory.HasKeyboardControl())
        {
            focusedOnItem = false;
            foreach (ReorderableList list in pageContentsDict.Values)
                list.index = -1;
        }
        else
        {
            if (pageContentsDict.ContainsKey(preset.Pages[focusedItemPage].GetInstanceID().ToString()) && pageContentsDict[preset.Pages[focusedItemPage].GetInstanceID().ToString()].HasKeyboardControl() && preset.Pages[focusedItemPage].Items.Count > 0)
            {
                focusedOnItem = true;
                pageDirectory.index = -1;
                foreach (ReorderableList list in pageContentsDict.Values)
                    if (list != pageContentsDict[preset.Pages[focusedItemPage].GetInstanceID().ToString()])
                        list.index = -1;
            }
            else if (preset.Pages[focusedItemPage].Items.Count == 0)
            {
                focusedOnItem = false;
            }      
        }
        // Check that the selected list is available, otherwise wait until it is.
        if (pageContentsDict.ContainsKey(preset.Pages[focusedItemPage].GetInstanceID().ToString()) && !draggingPage && focusedOnItem)
        {
            string listKey = preset.Pages[focusedItemPage].GetInstanceID().ToString();

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
            bool itemTransitionType = currentItem.TransitionType;
            float itemTransitionDuration = currentItem.TransitionDuration;
            bool itemTransitionOffset = currentItem.TransitionOffset;
            AnimationClip itemEnable = currentItem.EnableClip;
            AnimationClip itemDisable = currentItem.DisableClip;
            PageItem.SyncMode itemSync = currentItem.Sync;
            bool itemSaved = currentItem.Saved;
            Page itemPage = currentItem.PageReference;
            VRCExpressionsMenu.Control itemControl = currentItem.Control;

            // Item type.
            EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("HelpBox")) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(GUI.skin.box.padding.left, GUI.skin.box.padding.right, 5, 5) }, GUILayout.Height(24f));
            itemType = (PageItem.ItemType)EditorGUILayout.IntPopup(new GUIContent("Type", "The type of item."), (int)itemType, typePopupName, typePopupVal);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal(GUILayout.Height(5f));
            EditorGUILayout.EndHorizontal();

            // Separator
            rect = EditorGUILayout.BeginHorizontal();
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.width + 15, rect.y + 1));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // Draw item renamer.
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Name", "The name of the item."));
            itemName = EditorGUILayout.TextField(itemName, EditorStyles.toolbarTextField, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            // Item icon (only if the item is not of type Page).
            if (itemType != PageItem.ItemType.Subpage)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Icon", "The icon to use for the control."));
                itemIcon = (Texture2D)EditorGUILayout.ObjectField(itemIcon, typeof(Texture2D), false);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space();

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

                    EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("HelpBox")) { padding = new RectOffset(GUI.skin.box.padding.left, GUI.skin.box.padding.right, 5, 5) });
                    if (itemAnimations)
                    {
                        // Item enabled clip.
                        itemEnable = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Enable", "The Animation to play when the toggle is enabled."), itemEnable, typeof(AnimationClip), false);

                        // Item disabled clip.
                        itemDisable = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Disable", "The Animation to play when the toggle is disabled."), itemDisable, typeof(AnimationClip), false);

                        // Custom Spacer
                        GUILayout.Space(2f);
                        rect = EditorGUILayout.BeginHorizontal();
                        Handles.color = Color.gray;
                        Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.width + 25, rect.y + 1));
                        EditorGUILayout.EndHorizontal();
                        GUILayout.Space(2f);

                        // Item transition offset.
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("Loading State", "Where in the animation to display while/upon loading."));
                        itemTransitionOffset = Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(itemTransitionOffset), new string[] { "Start", "End" }));
                        EditorGUILayout.EndHorizontal();

                        // Custom Spacer
                        GUILayout.Space(2f);
                        rect = EditorGUILayout.BeginHorizontal();
                        Handles.color = Color.gray;
                        Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.width + 25, rect.y + 1));
                        EditorGUILayout.EndHorizontal();
                        GUILayout.Space(2f);

                        // Item transition timing.
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("Blend Timing", "Whether the duration is in fixed (s) or normalized (%) time."));
                        itemTransitionType = Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(itemTransitionType), new string[] { "Fixed", "Normalized" }));
                        EditorGUILayout.EndHorizontal();

                        // Item transition duration.
                        itemTransitionDuration = EditorGUILayout.FloatField(new GUIContent("Blend Duration", "How long the transition between states takes."), itemTransitionDuration);
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
                            EditorGUILayout.LabelField(currentItem.ObjectReference, EditorStyles.toolbarTextField, GUILayout.ExpandWidth(true));
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
                    itemState = !Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(!itemState), new string[] { "Enabled", "Disabled" }));
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
                    EditorGUI.BeginDisabledGroup(itemSync == PageItem.SyncMode.Manual);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(new GUIContent("Saved", "Whether to save the state of this item when unloading the avatar."));
                    itemSaved = !Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(!itemSaved), new string[] { "Enable", "Disable" }));
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.EndDisabledGroup();

                    // Like EditorGUILayout.Space(), but smaller.
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.EndVertical();
                    break;
                case PageItem.ItemType.Subpage:
                    // Check if another page besides the current one exists.
                    string[] names = new string[] { "None" };
                    Page[] pages = new Page[0];
                    if (preset.Pages.Count - 1 > 0)
                    {
                        names = new string[preset.Pages.Count];
                        names[0] = "None";
                        pages = new Page[preset.Pages.Count - 1];

                        // Store each page's name and index (excluding the currently selected page).
                        int index = 0;
                        for (int i = 0; i < preset.Pages.Count; i++)
                            if (i != focusedItemPage)
                            {
                                names[index + 1] = preset.Pages[i].name;
                                pages[index] = preset.Pages[i];
                                index++;
                            }
                    }
                    int pageIndex = EditorGUILayout.Popup(new GUIContent("Page", "The page to direct to."), itemPage != null && Array.IndexOf(pages, itemPage) != -1 ? Array.IndexOf(pages, itemPage) + 1 : 0, names);

                    // Item page reference.
                    if (pageIndex > 0)
                        itemPage = preset.Pages[preset.Pages.IndexOf(pages[pageIndex - 1])];
                    else
                        itemPage = null;
                    break;
                case PageItem.ItemType.Control:
                    // Item control.
                    controlDrawer.DrawControl(avatar, itemControl);
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
                    currentItem.ObjectReference = avatar != null && (resetReference || avatar.transform.Find(currentItem.ObjectReference) != null && (Helper.GetGameObjectPath(itemObject).IndexOf(Helper.GetGameObjectPath(avatar.transform)) != -1) || itemObject == null) ? (resetReference || itemObject == null ? "" : Helper.GetGameObjectPath(itemObject).Substring(Helper.GetGameObjectPath(itemObject).IndexOf(Helper.GetGameObjectPath(avatar.transform)) + Helper.GetGameObjectPath(avatar.transform).Length + 1)) : currentItem.ObjectReference;
                currentItem.UseAnimations = itemAnimations;
                currentItem.TransitionType = itemTransitionType;
                if (itemTransitionDuration < 0)
                    itemTransitionDuration = 0;
                currentItem.TransitionDuration = itemTransitionDuration;
                currentItem.TransitionOffset = itemTransitionOffset;
                currentItem.EnableClip = itemEnable;
                currentItem.DisableClip = itemDisable;
                currentItem.Sync = itemSync;
                currentItem.Saved = itemSaved;
                if (currentItem.Type == PageItem.ItemType.Subpage && itemPage != null && currentItem.PageReference != itemPage)
                    currentItem.name = itemPage.name;
                currentItem.PageReference = itemPage;
                if (currentItem.Type == PageItem.ItemType.Control)
                {
                    itemControl.name = currentItem.name;
                    itemControl.icon = currentItem.Icon;
                }
                currentItem.Control = itemControl;

                // Reassign the item to the list (might be redundant, haven't checked).
                preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index] = currentItem;
            }
            if (itemType != PageItem.ItemType.Button)
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

                    // Display the contents.
                    DrawCustomHeader(enableGroupContents);
                    if (enableGroupContents.list.Count != 0)
                    {
                        // Display the enableGroupContents list (with scrollbar).
                        enableScroll = EditorGUILayout.BeginScrollView(enableScroll, GUILayout.Height(Mathf.Clamp(enableGroupContents.GetHeight(), 0, enableGroupContents.elementHeight * 10 + 10)));
                        enableGroupContents.DoLayoutList();
                        EditorGUILayout.EndScrollView();
                    }
                    DrawButtons(enableGroupContents, true, true, false, "Create Member", "Remove Member", Rect.zero);

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
                    DrawButtons(disableGroupContents, true, true, false, "Create Member", "Remove Member", Rect.zero);
                    EditorGUILayout.Space();
                }
                EditorGUI.indentLevel--;
            }
            else if (itemType == PageItem.ItemType.Button)
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Height(5f));
                EditorGUILayout.EndHorizontal();

                // Update buttonGroupContents if it is using the wrong list or it has been modified.
                if (buttonGroupContents.list != preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup)
                {
                    buttonGroupContents.list = preset.Pages[focusedItemPage].Items[pageContentsDict[listKey].index].ButtonGroup;
                }

                // Display the contents.
                DrawCustomHeader(buttonGroupContents);
                if (buttonGroupContents.list.Count != 0)
                {
                    // Display the buttonGroupContents list (with scrollbar).
                    enableScroll = EditorGUILayout.BeginScrollView(enableScroll, GUILayout.Height(Mathf.Clamp(buttonGroupContents.GetHeight(), 0, buttonGroupContents.elementHeight * 10 + 10)));
                    EditorGUI.indentLevel++;
                    buttonGroupContents.DoLayoutList();
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndScrollView();
                }
                DrawButtons(buttonGroupContents, true, true, false, "Create Member", "Remove Member", Rect.zero);
                EditorGUILayout.Space();
            }

            // End item settings container.
            EditorGUILayout.EndVertical();
        }
        else if (!focusedOnItem && pageDirectory.index > -1 && pageDirectory.index < preset.Pages.Count)
        {
            // Draw page control container.
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")) { padding = new RectOffset(GUI.skin.box.padding.left, GUI.skin.box.padding.right, 5, 5) });

            // Draw and check for changes in the page name control.
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Name", "The name of the item."));
            string pageName = EditorGUILayout.TextField(preset.Pages[pageDirectory.index].name, EditorStyles.toolbarTextField, GUILayout.ExpandWidth(true));
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
        if (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying && EditorUtility.IsDirty(preset.GetInstanceID()))
        {
            InventoryPresetUtility.SaveChanges(preset);
        }
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
    private void DrawButtons(ReorderableList list, bool displayAdd, bool displayRemove, bool displayMore, string addText, string removeText, Rect given)
    {
        // Obtain the Rect for the footer.
        Rect rect = given != Rect.zero ? given : GUILayoutUtility.GetRect(4, defaultFooterHeight, GUILayout.ExpandWidth(true));

        // Button contents.
        //GUIContent iconToolbarPlus = EditorGUIUtility.TrIconContent("Toolbar Plus", "Add to list");
        GUIContent iconToolbarPlusMore = EditorGUIUtility.TrIconContent("Toolbar Plus More", "Other Options");
        //GUIContent iconToolbarMinus = EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove selection from list");

        GUIStyle preButton = "RL FooterButton";
        GUIStyle newButton = new GUIStyle(preButton);
        newButton.normal.textColor = GUI.skin.button.normal.textColor;//Color.Lerp(Color.black, Color.grey, 0.65f);
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
        Rect addRect = new Rect(leftEdge + 4, rect.y , rect.width / 2, 13);
        Rect removeRect = new Rect(rightEdge - 4 - (rect.width / 2), rect.y , rect.width / 2, 13);

        // Draw the background for the footer on Repaint Events.
        if (Event.current.type == EventType.Repaint)
        {
            footerBackground.Draw(rect, false, false, false, false);
            // Separators
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(rect.xMin + rect.width / 2, rect.yMin), new Vector2(rect.xMin + rect.width / 2, rect.yMin + defaultFooterHeight));
            Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.width + 15, rect.y));
        }

        // Makes button unable to be used while conditions are met.
        using (new EditorGUI.DisabledScope(!displayAdd ||
            (list.onCanAddCallback != null && !list.onCanAddCallback(list))))
        {
            Rect mainAddRect = addRect;
            if (displayMore)
                mainAddRect = new Rect(addRect.position, new Vector2(addRect.width - 25f, addRect.height));
            
            // Invoke the onAddCallback when the button is clicked followed by onChangedCallback.
            if (SpecialButton(mainAddRect, new GUIContent(addText), out int button, new GUIStyle(newButton)))
            {
                // Left click
                if (button == 0)
                {
                    if (list.onAddDropdownCallback != null)
                        list.onAddDropdownCallback(addRect, list);
                    else
                        list.onAddCallback?.Invoke(list);

                    list.onChangedCallback?.Invoke(list);

                    // If neither callback was provided, nothing will happen when the button is clicked.
                }
            }
            if (displayMore)
            {
                Rect plusAddRect = new Rect(new Vector2(addRect.width - 5f, addRect.position.y), new Vector2(15f, addRect.height));
                if (SpecialButton(plusAddRect, iconToolbarPlusMore, out button, new GUIStyle(newButton)))
                {
                    // Left click
                    if (button == 0)
                    {
                        // Create the menu and add items to it
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Import External Menus"), false, OnImportExternalMenus);
                        menu.AddItem(new GUIContent("Append Another Preset"), false, OnAppendAnotherPreset);
                        // Display the menu
                        menu.ShowAsContext();
                    }
                }
            }
        }

        // Exact same as above, just with the other button and removal callbacks.
        using (new EditorGUI.DisabledScope(
            list.index < 0 || list.index >= list.count || !displayRemove ||
            (list.onCanRemoveCallback != null && !list.onCanRemoveCallback(list))))
        {
            if (SpecialButton(removeRect, new GUIContent(removeText), out int button, newButton))
            {
                // Left Click
                if (button == 0)
                {
                    list.onRemoveCallback?.Invoke(list);

                    list.onChangedCallback?.Invoke(list);

                    // If neither callback was provided, nothing will happen when the button is clicked.
                }
            }
        }
    }

    /*
    // Context Menu Functions 
    */

    private void OnImportExternalMenus()
    {
        ImportExternalWindow.ImportExternalWindowInit(preset);
    }

    private void OnAppendAnotherPreset()
    {
        AppendPresetWindow.AppendPresetWindowInit(preset);
    }

    // Shows the page context menu
    private void ShowPageContextMenu(int pageIndex)
    {
        // Create the menu
        GenericMenu menu = new GenericMenu();

        // Copy the page settings to the buffer
        menu.AddItem(new GUIContent("Copy Settings"), false, InventoryPresetUtility.CopyPageSettings, new object[] { preset.Pages[pageIndex] });

        // Paste the page settings from the buffer
        menu.AddItem(new GUIContent("Paste Settings"), false, InventoryPresetUtility.PastePageSettings, new object[] { preset.Pages[pageIndex], preset });

        menu.AddSeparator("");

        // Duplicate the page
        menu.AddItem(new GUIContent("Duplicate Page"), false, InventoryPresetUtility.DuplicatePage, new object[] { preset.Pages[pageIndex], preset });

        // Delete the selected page
        if (preset.Pages.Count > 1)
            menu.AddItem(new GUIContent("Remove Page"), false, InventoryPresetUtility.RemovePage, new object[] { preset.Pages[pageIndex], pagesFoldout, preset });

        // Display the menu
        menu.ShowAsContext();
    }

    // Shows the item context menu
    private void ShowItemContextMenu(int pageIndex, int itemIndex)
    {
        // Create the menu
        GenericMenu menu = new GenericMenu();

        // Copy the item settings to the buffer
        menu.AddItem(new GUIContent("Copy Settings"), false, InventoryPresetUtility.CopyItemSettings, new object[] { preset.Pages[pageIndex].Items[itemIndex], preset });

        // Paste the item settings from the buffer
        menu.AddItem(new GUIContent("Paste Settings"), false, InventoryPresetUtility.PasteItemSettings, new object[] { preset.Pages[pageIndex].Items[itemIndex], preset.Pages[pageIndex], preset });

        menu.AddSeparator("");

        // Add pages to menu that have space
        for (int i = 0; i < preset.Pages.Count; i++)
            if (pageIndex != i && preset.Pages[i].Items.Count < 8)
                menu.AddItem(new GUIContent("Send to Page/" + preset.Pages[i].name), false, InventoryPresetUtility.SendItemToPage, new object[] { preset.Pages[pageIndex].Items[itemIndex], preset.Pages[pageIndex], preset.Pages[i], preset });

        menu.AddSeparator("");

        // Duplicate the item within the same page
        if (preset.Pages[pageIndex].Items.Count < 8)
        menu.AddItem(new GUIContent("Duplicate Item"), false, InventoryPresetUtility.DuplicateItem, new object[] { preset.Pages[pageIndex], preset.Pages[pageIndex].Items[itemIndex], preset });

        // Delete the selected item
        menu.AddItem(new GUIContent("Remove Item"), false, InventoryPresetUtility.RemoveItem, new object[] { preset.Pages[pageIndex].Items[itemIndex], preset.Pages[pageIndex], preset });

        // Display the menu
        menu.ShowAsContext();
    }

    // Shows the group context menu
    private void ShowGroupContextMenu(int pageIndex, int itemIndex, int groupType)
    {
        // Create the menu
        GenericMenu menu = new GenericMenu();

        // Copy the group settings to the buffer
        menu.AddItem(new GUIContent("Copy Settings"), false, InventoryPresetUtility.CopyGroupSettings, new object[] { preset.Pages[pageIndex].Items[itemIndex], groupType, preset });

        // Paste the group settings from the buffer
        menu.AddItem(new GUIContent("Paste Settings"), false, InventoryPresetUtility.PasteGroupSettings, new object[] { preset.Pages[pageIndex].Items[itemIndex], groupType, preset });

        menu.AddSeparator("");

        // Set all members to enable
        menu.AddItem(new GUIContent("Set All To/Enable"), false, InventoryPresetUtility.SetAllGroupMembers, new object[] { preset.Pages[pageIndex].Items[itemIndex], groupType, preset, true });

        // Set all members to disable
        menu.AddItem(new GUIContent("Set All To/Disable"), false, InventoryPresetUtility.SetAllGroupMembers, new object[] { preset.Pages[pageIndex].Items[itemIndex], groupType, preset, false });

        // Clear the group settings
        menu.AddItem(new GUIContent("Clear Group"), false, InventoryPresetUtility.ClearGroupSettings, new object[] { preset.Pages[pageIndex].Items[itemIndex], groupType, preset });

        // Display the menu
        menu.ShowAsContext();
    }

    /*
    // Special Buttons
    */

    private bool SpecialButton(Rect rect, GUIContent content, out int button, GUIStyle style = null)
    {
        if (style == null) style = GUI.skin != null ? GUI.skin.button : "Button";

        Event evt = Event.current;
        int controlId = EditorGUIUtility.GetControlID(FocusType.Passive);
        button = evt.button;
        switch (evt.type)
        {
            case EventType.MouseDown:
                {
                    if (GUIUtility.hotControl == 0 && rect.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        evt.Use();
                    }
                    break;
                }
            case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl == controlId)
                    {
                        evt.Use();
                    }
                    break;
                }
            case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl == controlId && rect.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = 0;
                        evt.Use();
                        return true;
                    }
                    break;
                }
            case EventType.Repaint:
                {
                    style.Draw(rect, content, controlId);
                    break;
                }
        }

        return false;
    }

    /*
    // Custom Progress Bar
    */

    private void DoCustomProgressBar(Rect position, float value, float value2, GUIStyle progressBarBackgroundStyle, GUIStyle progressBarStyle, GUIStyle progressBarUnderStyle)
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
                Rect memBar = new Rect(position.x, position.y + position.height + 2, position.width, 6);
                progressBarBackgroundStyle.Draw(memBar, mouseHover, false, false, false);
                if (value2 > 0.0f)
                {
                    value2 = Mathf.Clamp01(value2);
                    var barRect = new Rect(position)
                    {
                        height = 1,
                        y = position.y + position.height + 4
                    };
                    barRect.width *= value2;
                    if (barRect.width >= 1f)
                        progressBarUnderStyle.Draw(barRect, GUIContent.none, mouseHover, false, false, false);
                }
                else if (value2 == -1.0f)
                {
                    float barWidth = position.width * 0.2f;
                    float halfBarWidth = barWidth / 2.0f;
                    float cos = Mathf.Cos((float)EditorApplication.timeSinceStartup * 2f);
                    float rb = position.x + halfBarWidth;
                    float re = position.xMax - halfBarWidth;
                    float scale = (re - rb) / 2f;
                    float cursor = scale * cos;
                    var barRect = new Rect(position.x + cursor + scale, position.y, barWidth, position.height);
                    progressBarUnderStyle.Draw(barRect, GUIContent.none, mouseHover, false, false, false);
                }
                break;
        }
    }

    /*
    // These next two functions are literally just code from the Expression Menu for selecting the avatar.
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

    /*
    // Draw a Expression Menu Control 
    */

    // Coder's note: I'd like to formally apologize to VRChat for butchering their code to work with my setup.
    private class ControlDrawer
    {
        VRCAvatarDescriptor activeDescriptor = null;
        string[] parameterNames;

        public void DrawControl(VRCAvatarDescriptor avatar, VRCExpressionsMenu.Control control)
        {
            activeDescriptor = avatar;

            // Disable everything if the Avatar is missing.
            if (activeDescriptor == null)
                EditorGUILayout.HelpBox("No active avatar descriptor found in scene.", MessageType.Error);
            EditorGUILayout.Space();

            // Wait until the control is ready.
            if (control == null)
                return;

            // Disable everything if the Avatar is missing.
            EditorGUI.BeginDisabledGroup(activeDescriptor == null);

            //Init stage parameters
            if (activeDescriptor != null)
            {
                int paramCount = activeDescriptor.GetExpressionParameterCount();
                parameterNames = new string[paramCount + 1];
                parameterNames[0] = "[None]";
                for (int i = 0; i < paramCount; i++)
                {
                    var param = activeDescriptor.GetExpressionParameter(i);
                    string name2 = "[None]";
                    if (param != null && !string.IsNullOrEmpty(param.name))
                        name2 = string.Format("{0}, {1}", param.name, param.valueType.ToString(), i + 1);
                    parameterNames[i + 1] = name2;
                }
            }
            else
                parameterNames = new string[0];

            //Generic params
            control.type = (VRCExpressionsMenu.Control.ControlType)EditorGUILayout.EnumPopup("Subtype", control.type);

            //Type Info
            var controlType = control.type;
            switch (controlType)
            {
                case VRCExpressionsMenu.Control.ControlType.Button:
                    EditorGUILayout.HelpBox("Click or hold to activate. The button remains active for a minimum 0.2s.\nWhile active the (Parameter) is set to (Value).\nWhen inactive the (Parameter) is reset to zero.", MessageType.Info);
                    break;
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    EditorGUILayout.HelpBox("Click to toggle on or off.\nWhen turned on the (Parameter) is set to (Value).\nWhen turned off the (Parameter) is reset to zero.", MessageType.Info);
                    break;
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    EditorGUILayout.HelpBox("Opens another expression menu.\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.", MessageType.Info);
                    break;
                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    EditorGUILayout.HelpBox("Puppet menu that maps the joystick to two parameters (-1 to +1).\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.", MessageType.Info);
                    break;
                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    EditorGUILayout.HelpBox("Puppet menu that maps the joystick to four parameters (0 to 1).\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.", MessageType.Info);
                    break;
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    EditorGUILayout.HelpBox("Puppet menu that sets a value based on joystick rotation. (0 to 1)\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.", MessageType.Info);
                    break;
            }

            //Param
            switch (controlType)
            {
                case VRCExpressionsMenu.Control.ControlType.Button:
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    DrawParameterDropDown(ref control.parameter, "Parameter");
                    DrawParameterValue(control.parameter, ref control.value);
                    break;
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            //Style
            /*if (controlType == ExpressionsControl.ControlType.Toggle)
            {
                style.intValue = EditorGUILayout.Popup("Visual Style", style.intValue, ToggleStyles);
            }*/

            //Sub menu
            if (controlType == VRCExpressionsMenu.Control.ControlType.SubMenu)
            {
                control.subMenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField("Submenu", control.subMenu, typeof(VRCExpressionsMenu), false);
            }

            //Puppet Parameter Set
            switch (controlType)
            {
                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    if (control.subParameters.Length != 2)
                        control.subParameters = new VRCExpressionsMenu.Control.Parameter[2];
                    if (control.labels.Length != 4)
                        control.labels = new VRCExpressionsMenu.Control.Label[4];

                    DrawParameterDropDown(ref control.subParameters[0], "Parameter Horizontal", false);
                    DrawParameterDropDown(ref control.subParameters[1], "Parameter Vertical", false);

                    DrawLabel(ref control.labels[0], "Label Up");
                    DrawLabel(ref control.labels[1], "Label Right");
                    DrawLabel(ref control.labels[2], "Label Down");
                    DrawLabel(ref control.labels[3], "Label Left");
                    break;
                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    if (control.subParameters.Length != 4)
                        control.subParameters = new VRCExpressionsMenu.Control.Parameter[4];
                    if (control.labels.Length != 4)
                        control.labels = new VRCExpressionsMenu.Control.Label[4];

                    DrawParameterDropDown(ref control.subParameters[0], "Parameter Up", false);
                    DrawParameterDropDown(ref control.subParameters[1], "Parameter Right", false);
                    DrawParameterDropDown(ref control.subParameters[2], "Parameter Down", false);
                    DrawParameterDropDown(ref control.subParameters[3], "Parameter Left", false);

                    DrawLabel(ref control.labels[0], "Label Up");
                    DrawLabel(ref control.labels[1], "Label Right");
                    DrawLabel(ref control.labels[2], "Label Down");
                    DrawLabel(ref control.labels[3], "Label Left");
                    break;
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    if (control.subParameters.Length != 1)
                        control.subParameters = new VRCExpressionsMenu.Control.Parameter[1];
                    if (control.labels.Length != 0)
                        control.labels = new VRCExpressionsMenu.Control.Label[0];

                    DrawParameterDropDown(ref control.subParameters[0], "Paramater Rotation", false);
                    break;
                default:
                    if (control.subParameters == null || control.subParameters.Length != 0)
                        control.subParameters = new VRCExpressionsMenu.Control.Parameter[0];
                    if (control.labels == null || control.labels.Length != 0)
                        control.labels = new VRCExpressionsMenu.Control.Label[0];
                    break;
            }
            EditorGUI.EndDisabledGroup();
        }
        void DrawParameterDropDown(ref VRCExpressionsMenu.Control.Parameter parameter, string name, bool allowBool = true)
        {
            VRCExpressionParameters.Parameter param = null;
            string value = parameter != null ? parameter.name : "";

            bool parameterFound = false;
            EditorGUILayout.BeginHorizontal();
            {
                if (activeDescriptor != null)
                {
                    //Dropdown
                    int currentIndex;
                    if (string.IsNullOrEmpty(value))
                    {
                        currentIndex = -1;
                        parameterFound = true;
                    }
                    else
                    {
                        currentIndex = -2;
                        for (int i = 0; i < GetExpressionParametersCount(); i++)
                        {
                            var item = activeDescriptor.GetExpressionParameter(i);
                            if (item.name == value)
                            {
                                param = item;
                                parameterFound = true;
                                currentIndex = i;
                                break;
                            }
                        }
                    }

                    //Dropdown
                    EditorGUI.BeginChangeCheck();
                    currentIndex = EditorGUILayout.Popup(name, currentIndex + 1, parameterNames);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (currentIndex == 0)
                            parameter.name = "";
                        else
                            parameter.name = GetExpressionParameter(currentIndex - 1).name;
                    }
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.Popup(0, new string[0]);
                    EditorGUI.EndDisabledGroup();
                }

                //Text field
                if (parameter != null)
                    parameter.name = EditorGUILayout.TextField(parameter.name, GUILayout.MaxWidth(200));
            }
            EditorGUILayout.EndHorizontal();

            if (!parameterFound)
            {
                EditorGUILayout.HelpBox("Parameter not found on the active avatar descriptor.", MessageType.Warning);
            }

            if (!allowBool && param != null && param.valueType == VRCExpressionParameters.ValueType.Bool)
            {
                EditorGUILayout.HelpBox("Bool parameters not valid for this choice.", MessageType.Error);
            }
        }
        void DrawParameterValue(VRCExpressionsMenu.Control.Parameter parameter, ref float value)
        {
            string paramName = parameter != null ? parameter.name : "";
            if (!string.IsNullOrEmpty(paramName))
            {
                var paramDef = FindExpressionParameterDef(paramName);
                if (paramDef != null)
                {
                    if (paramDef.valueType == VRCExpressionParameters.ValueType.Int)
                    {
                        value = EditorGUILayout.IntField("Value", Mathf.Clamp((int)value, 0, 255));
                    }
                    else if (paramDef.valueType == VRCExpressionParameters.ValueType.Float)
                    {
                        value = EditorGUILayout.FloatField("Value", Mathf.Clamp(value, -1f, 1f));
                    }
                    else if (paramDef.valueType == VRCExpressionParameters.ValueType.Bool)
                    {
                        value = 1f;
                    }
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(true);
                    value = EditorGUILayout.FloatField("Value", value);
                    EditorGUI.EndDisabledGroup();
                }
            }
        }
        VRCExpressionParameters.Parameter FindExpressionParameterDef(string name)
        {
            if (activeDescriptor == null || string.IsNullOrEmpty(name))
                return null;

            //Find
            int length = GetExpressionParametersCount();
            for (int i = 0; i < length; i++)
            {
                var item = GetExpressionParameter(i);
                if (item != null && item.name == name)
                    return item;
            }
            return null;
        }
        int GetExpressionParametersCount()
        {
            if (activeDescriptor != null && activeDescriptor.expressionParameters != null && activeDescriptor.expressionParameters.parameters != null)
                return activeDescriptor.expressionParameters.parameters.Length;
            return 0;
        }
        VRCExpressionParameters.Parameter GetExpressionParameter(int i)
        {
            if (activeDescriptor != null)
                return activeDescriptor.GetExpressionParameter(i);
            return null;
        }
        void DrawLabel(ref VRCExpressionsMenu.Control.Label subControl, string name)
        {
            EditorGUILayout.LabelField(name);
            EditorGUI.indentLevel += 2;
            subControl.name = EditorGUILayout.TextField("Name", subControl.name);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Icon");
            EditorGUI.indentLevel -= 2;
            subControl.icon = (Texture2D)EditorGUILayout.ObjectField(subControl.icon, typeof(Texture2D), false);
            EditorGUILayout.EndHorizontal();
        }
    }
}
