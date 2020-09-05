using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

[CustomEditor(typeof(InventoryPreset))]
public class InventoryPresetEditor : Editor 
{
    // The Asset being edited.
    private InventoryPreset preset;

    // Reorderable Lists to be displayed.
    private ReorderableList pageContents;
    private ReorderableList pageDirectory;
    private ReorderableList enableGroupContents;
    private ReorderableList disableGroupContents;

    // Default values obtained and used for displaying ReorderableLists.
    private float defaultHeaderHeight;
    private float defaultFooterHeight;

    // Scrollbar Vectors.
    private Vector2 directoryScroll;
    private Vector2 enableScroll;
    private Vector2 disableScroll;

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

        pageDirectory = new ReorderableList(preset.Pages, typeof(Page), true, true, false, false)
        {
            elementHeight = 18f,
            headerHeight = 0,
            footerHeight = 0
        };
        pageDirectory.drawHeaderCallback += DrawDirectoryHeader;
        pageDirectory.drawElementCallback += DrawDirectoryElement;
        pageDirectory.onAddCallback += AddDirectoryItem;
        pageDirectory.onRemoveCallback += RemoveDirectoryItem;

        pageContents = new ReorderableList(null, typeof(PageItem), true, true, false, false)
        {
            elementHeight = 18f
        };
        pageContents.drawHeaderCallback += DrawPageHeader;
        pageContents.drawElementCallback += DrawPageElement;
        pageContents.onAddCallback += AddPageItem;
        pageContents.onRemoveCallback += RemovePageItem;

        defaultHeaderHeight = pageContents.headerHeight;
        defaultFooterHeight = pageContents.footerHeight;

        enableGroupContents = new ReorderableList(null, typeof(GroupItem), true, false, false, false)
        {
            elementHeight = 18f,
            headerHeight = 0,
            footerHeight = 0
        };
        enableGroupContents.drawHeaderCallback += DrawEnableGroupHeader;
        enableGroupContents.drawElementCallback += DrawEnableGroupElement;
        enableGroupContents.onAddCallback += AddEnableGroupItem;
        enableGroupContents.onRemoveCallback += RemoveEnableGroupItem;

        disableGroupContents = new ReorderableList(null, typeof(GroupItem), true, false, false, false)
        {
            elementHeight = 18f,
            headerHeight = 0,
            footerHeight = 0
        };
        disableGroupContents.drawHeaderCallback += DrawDisableGroupHeader;
        disableGroupContents.drawElementCallback += DrawDisableGroupElement;
        disableGroupContents.onAddCallback += AddDisableGroupItem;
        disableGroupContents.onRemoveCallback += RemoveDisableGroupItem;

        EditorApplication.wantsToQuit += WantsToQuit;
    }

    // Remove callbacks to prevent memory leak.
    public void OnDisable()
    {
        if (pageDirectory != null)
        {
            pageDirectory.drawHeaderCallback -= DrawDirectoryHeader;
            pageDirectory.drawElementCallback -= DrawDirectoryElement;
            pageDirectory.onAddCallback -= AddDirectoryItem;
            pageDirectory.onRemoveCallback -= RemoveDirectoryItem;
        }

        if (pageContents != null)
        {
            pageContents.drawHeaderCallback -= DrawPageHeader;
            pageContents.drawElementCallback -= DrawPageElement;
            pageContents.onAddCallback -= AddPageItem;
            pageContents.onRemoveCallback -= RemovePageItem;
        }

        if (enableGroupContents != null)
        {
            enableGroupContents.drawHeaderCallback -= DrawEnableGroupHeader;
            enableGroupContents.drawElementCallback -= DrawEnableGroupElement;
            enableGroupContents.onAddCallback -= AddEnableGroupItem;
            enableGroupContents.onRemoveCallback -= RemoveEnableGroupItem;
        }

        if (disableGroupContents != null)
        {
            disableGroupContents.drawHeaderCallback -= DrawDisableGroupHeader;
            disableGroupContents.drawElementCallback -= DrawDisableGroupElement;
            disableGroupContents.onAddCallback -= AddDisableGroupItem;
            disableGroupContents.onRemoveCallback -= RemoveDisableGroupItem;
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

        //Check if toggle limit exceeded
        int totalUsage = 1; // 0 is reserved.

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
                            totalUsage += 3;
                            if (pageItem.EnableGroup.Length > 0)
                                totalUsage++;
                            if (pageItem.DisableGroup.Length > 0)
                                totalUsage++;
                            break;
                    }
                }
            }
        }
        if (totalUsage > 256)
        {
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
            EditorGUILayout.HelpBox("This preset uses more synced data than an Integer can hold.\n(Max: 256 | Used: " + totalUsage + ")", MessageType.Warning);
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
            EditorGUILayout.HelpBox("Data usage depends on both sync mode and group usage:\n\nOff = 1 + (1 for each Toggle Group used)\nManual = 3\nAuto = 3 + (1 for each Toggle Group used)", MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        // Begin Asset modification.
        serializedObject.Update();
        EditorGUILayout.BeginVertical();

        // Begin page settings section.
        GUILayout.Label("Page Settings", EditorStyles.boldLabel);

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
        }       

        // Create a list of indicies to use in the dropdown page select.
        string[] pageNames = new string[pageDirectory.list.Count];
        for (int i = 0; i < pageDirectory.list.Count; i++)
        {
            pageNames[i] = (i + 1).ToString();
        }

        // Correct pageDirectory index if it has left the list bounds.
        if (pageDirectory.index >= pageDirectory.list.Count)
        {
            pageDirectory.index = pageDirectory.list.Count - 1;
        }
        else if (pageDirectory.index < 0)
        {
            pageDirectory.index = 0;
        }

        // Draw page control container.
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));

        // Draw page renamer.
        EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
        GUILayout.Label(new GUIContent("Name:", "Name of the selected page."), GUILayout.ExpandWidth(false));

        // Draw and check for changes in the page name control.
        EditorGUI.BeginChangeCheck();
        string pageName = EditorGUILayout.TextField(preset.Pages[pageDirectory.index].name, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
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

        // Draw left arrow.
        if (GUILayout.Button('\u25C0'.ToString(), GUILayout.Width(30f)))
        {
            // Go to the previous page if there is one.
            if (pageDirectory.index > 0)
            {
                pageDirectory.index--;
                GUI.FocusControl(null);
            }
        }

        // Draw dropdown page selector.
        EditorGUI.BeginChangeCheck();
        pageDirectory.index = EditorGUILayout.Popup(pageDirectory.index, pageNames, new GUIStyle(GUI.skin.GetStyle("Dropdown")), GUILayout.Width(30f));
        if (EditorGUI.EndChangeCheck())
        {
            GUI.FocusControl(null);
        }

        // Draw right arrow.
        if (GUILayout.Button('\u25B6'.ToString(), GUILayout.Width(30f)))
        {
            if (pageDirectory.index < pageDirectory.list.Count - 1)
            {
                pageDirectory.index++;
                GUI.FocusControl(null);
            }
        }
        EditorGUILayout.EndHorizontal();

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

        // Draw the pageDirectory list (with scrollbar).
        DrawCustomHeader(pageDirectory);
        directoryScroll = EditorGUILayout.BeginScrollView(directoryScroll, GUILayout.Height(Mathf.Clamp(pageDirectory.GetHeight(), 0, pageDirectory.elementHeight * 10 + 10)));
        if (pageDirectory != null)
            pageDirectory.DoLayoutList();
        EditorGUILayout.EndScrollView();
        DrawButtons(pageDirectory, true, preset.Pages.Count > 1);
        
        // End section.
        EditorGUILayout.Space();
        DrawLine();

        // Update the pageContents list if the a different page is selected.
        if (pageContents.list != preset.Pages[pageDirectory.index].Items)
        {
            pageContents.list = preset.Pages[pageDirectory.index].Items;
        }

        // Begin item settings section.
        GUILayout.Label("Item Settings", EditorStyles.boldLabel);
        
        // Make sure that there is at least one item in the page.
        if (pageContents.list.Count < 1)
        {
            PageItem item = CreateInstance<PageItem>();
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            item.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(item, _path);
            item.name = "Slot 1";
            preset.Pages[pageDirectory.index].Items.Add(item);
        }

        // Create a list of indicies to use in the dropdown item select.
        string[] itemNames = new string[pageContents.list.Count];
        for (int i = 0; i < itemNames.Length; i++)
        {
            itemNames[i] = (i + 1).ToString();
        }

        // Correct pageContents index if it has left the list bounds.
        if (pageContents.index >= pageContents.list.Count)
        {
            pageContents.index = pageContents.list.Count - 1;
        }
        else if (pageContents.index < 0)
        {
            pageContents.index = 0;
        }

        // Draw item control container.
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));

        // Draw item renamer.
        EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
        GUILayout.Label(new GUIContent("Name:", "The name of the item."), GUILayout.ExpandWidth(false));

        bool nameChanged = false;
        string itemName = preset.Pages[pageDirectory.index].Items[pageContents.index].name;
        if (preset.Pages[pageDirectory.index].Items[pageContents.index].Type != PageItem.ItemType.Page)
        {
            EditorGUI.BeginChangeCheck();
            itemName = EditorGUILayout.TextField(itemName, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));

            // Remember if the name was modified for later.
            if (EditorGUI.EndChangeCheck())
            {
                nameChanged = true;
            }
        }
        else
        {
            if (preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference != null)
            {
                // If this is a Page Item, allow the name field to modify the page name instead.
                EditorGUI.BeginChangeCheck();
                string refPageName = EditorGUILayout.TextField(preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference.name, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck())
                {
                    // Revert to default name if left blank.
                    if (refPageName == "")
                    {
                        refPageName = "Page " + (preset.Pages.IndexOf(preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference) + 1);
                    }

                    // Deal with pages that share the same name.
                    List<string> names = new List<string>();
                    foreach (Page page in preset.Pages)
                    {
                        if (page != preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference)
                            names.Add(page.name);
                    }
                    if (names.Contains(refPageName))
                    {
                        int occurance = 0;
                        while (names.Contains(refPageName + " " + occurance))
                        {
                            occurance++;
                        }
                        refPageName = refPageName + " " + occurance;
                    }

                    // Mark the preset as dirty, record the page, and update it.
                    EditorUtility.SetDirty(preset);
                    Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference, "Page Modified");
                    preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference.name = refPageName;
                }
            }
            else
            {
                EditorGUILayout.TextField("None", new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
            }           
        }
                    
        // Draw left arrow.
        if (GUILayout.Button('\u25C0'.ToString(), GUILayout.Width(30f)))
        {
            if (pageContents.index > 0)
            {
                pageContents.index--;
                GUI.FocusControl(null);
            }
        }

        // Draw dropdown item selector.
        EditorGUI.BeginChangeCheck();
        pageContents.index = EditorGUILayout.Popup(pageContents.index, itemNames, new GUIStyle(GUI.skin.GetStyle("Dropdown")), GUILayout.Width(30f));
        if (EditorGUI.EndChangeCheck())
        {
            GUI.FocusControl(null);
        }

        // Draw right arrow.
        if (GUILayout.Button('\u25B6'.ToString(), GUILayout.Width(30f)))
        {
            if (pageContents.index < pageContents.list.Count - 1)
            {
                pageContents.index++;
                GUI.FocusControl(null);
            }
        }
        EditorGUILayout.EndHorizontal();

        // Other item settings.
        EditorGUILayout.BeginVertical();

        EditorGUI.BeginChangeCheck();

        // Store the current state of each item to use for each control (less code overall).
        PageItem currentItem = preset.Pages[pageDirectory.index].Items[pageContents.index];

        Texture2D itemIcon = currentItem.Icon;
        PageItem.ItemType itemType = currentItem.Type;
        bool itemState = currentItem.InitialState;
        AnimationClip itemEnable = currentItem.EnableClip;
        AnimationClip itemDisable = currentItem.DisableClip;
        PageItem.SyncMode itemSync = currentItem.Sync;
        Page itemPage = currentItem.PageReference;
        VRCExpressionsMenu itemMenu = currentItem.Submenu;

        // Item icon (only if the item is not of type Page).
        if (itemType != PageItem.ItemType.Page)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent("Icon", "The icon to use for the control."));
            itemIcon = (Texture2D)EditorGUILayout.ObjectField(itemIcon, typeof(Texture2D), false);
            EditorGUILayout.EndHorizontal();
        }
            
        // Item type.
        itemType = (PageItem.ItemType)EditorGUILayout.EnumPopup(new GUIContent("Type", "The type of item."), itemType);

        // Type based settings.
        switch (itemType)
        {
            case PageItem.ItemType.Toggle:
                // Item starting state.
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Start", "What state the toggle starts in."));
                itemState = Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(itemState), new string[] { "Disable", "Enable" }));
                EditorGUILayout.EndHorizontal();

                // Item enabled clip.
                itemEnable = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Enable", "The Animation to play when the toggle is enabled."), itemEnable, typeof(AnimationClip), false);

                // Item disabled clip.
                itemDisable = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Disable", "The Animation to play when the toggle is disabled."), itemDisable, typeof(AnimationClip), false);

                // Item sync setting.
                itemSync = (PageItem.SyncMode)EditorGUILayout.EnumPopup(new GUIContent("Sync", "How the toggle should sync with others."), itemSync);

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
                        if (i != pageDirectory.index)
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
        if (EditorGUI.EndChangeCheck() || nameChanged)
        {
            EditorUtility.SetDirty(preset);
            Undo.RecordObject(currentItem, "Item Modified");

            // Correct the name if it is blank.
            if (itemName == "")
            {
                itemName = "Slot " + (pageContents.index + 1);
            }

            // Update the item's values.
            currentItem.name = itemName;
            currentItem.Icon = itemIcon;
            currentItem.Type = itemType;
            currentItem.InitialState = itemState;
            currentItem.EnableClip = itemEnable;
            currentItem.DisableClip = itemDisable;
            currentItem.Sync = itemSync;
            currentItem.PageReference = itemPage;
            currentItem.Submenu = itemMenu;

            // Reassign the item to the list (might be redundant, haven't checked).
            preset.Pages[pageDirectory.index].Items[pageContents.index] = currentItem;
        }

        // End item settings container.
        EditorGUILayout.EndVertical();

        // Determine whether or not to display the add or remove buttons this Repaint.
        pageContents.displayAdd = preset.Pages[pageDirectory.index].Items.Count < 8;
        pageContents.displayRemove = preset.Pages[pageDirectory.index].Items.Count > 1;

        // Display the pageContents list (without scrollbar).
        EditorGUILayout.BeginVertical();
        if (pageContents != null)
            pageContents.DoLayoutList();
        EditorGUILayout.EndVertical();

        // End section.
        EditorGUILayout.Space();
        DrawLine();

        // Begin toggle groups section.

        // If a toggle is selected, rename the section to use the toggle's name. Otherwise display the default name.
        if (preset.Pages[pageDirectory.index].Items[pageContents.index].Type == PageItem.ItemType.Toggle)
        {
            GUILayout.Label(preset.Pages[pageDirectory.index].Items[pageContents.index].name + "'s Groups", EditorStyles.boldLabel);
        }
        else
        {
            GUILayout.Label("Toggle Groups", EditorStyles.boldLabel);
        }
        EditorGUILayout.BeginVertical();

        // Only draw the section if the current item is of type Toggle.
        if (preset.Pages[pageDirectory.index].Items[pageContents.index].Type == PageItem.ItemType.Toggle)
        {
            // Update enableGroupContents if it is using the wrong list or it has been modified.
            if (enableGroupContents.list != preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup)
            {
                enableGroupContents.list = preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup;
            }
            
            // If the group is empty, display a button for creating it. Otherwise, display the contents.
            if (enableGroupContents.list.Count == 0)
            {
                EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
                GUILayout.Label(new GUIContent("When Enabled...", "Modifies listed toggles when this toggle is enabled."));
                if (GUILayout.Button(new GUIContent("Create Group")))
                {
                    EditorUtility.SetDirty(preset);
                    Undo.IncrementCurrentGroup();
                    int group = Undo.GetCurrentGroup();
                    GroupItem item = CreateInstance<GroupItem>();
                    Undo.RegisterCreatedObjectUndo(item, "Add Group Item");

                    // Configure and add the new item to the array.
                    item.name = "Group Item";
                    item.hideFlags = HideFlags.HideInHierarchy;
                    item.Reaction = GroupItem.GroupType.Enable;                   
                    string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                    AssetDatabase.AddObjectToAsset(item, _path);                    
                    GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.Length + 1];
                    preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.CopyTo(newArray, 0);
                    newArray[newArray.GetUpperBound(0)] = item;

                    Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index], "Add Group Item");
                    preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup = newArray;
                    Undo.CollapseUndoOperations(group);  
                }
                EditorGUILayout.BeginVertical();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
            }
            else
            {
                // Display the enableGroupContents list (with scrollbar).
                DrawCustomHeader(enableGroupContents);
                enableScroll = EditorGUILayout.BeginScrollView(enableScroll, GUILayout.Height(Mathf.Clamp(enableGroupContents.GetHeight(), 0, enableGroupContents.elementHeight * 10 + 10)));
                enableGroupContents.DoLayoutList();
                EditorGUILayout.EndScrollView();
                DrawButtons(enableGroupContents, true, true);
            }

            // Add some empty space between the two groups.
            EditorGUILayout.Space();

            // Repeat for the Disable Group.
            if (disableGroupContents.list != preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup)
            {
                disableGroupContents.list = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup;
            }

            if (disableGroupContents.list.Count == 0)
            {
                EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
                GUILayout.Label(new GUIContent("When Disabled...", "Modifies listed toggles when this toggle is disabled."));
                if (GUILayout.Button(new GUIContent("Create Group")))
                {
                    EditorUtility.SetDirty(preset);
                    Undo.IncrementCurrentGroup();
                    int group = Undo.GetCurrentGroup();
                    GroupItem item = CreateInstance<GroupItem>();
                    Undo.RegisterCreatedObjectUndo(item, "Add Group Item");

                    item.name = "Group Item";
                    item.hideFlags = HideFlags.HideInHierarchy;
                    item.Reaction = GroupItem.GroupType.Disable;                                      
                    string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                    AssetDatabase.AddObjectToAsset(item, _path);
                    GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.Length + 1];
                    preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.CopyTo(newArray, 0);
                    newArray[newArray.GetUpperBound(0)] = item;

                    Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index], "Add Group Item");
                    preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup = newArray;
                    Undo.CollapseUndoOperations(group);
                }
                EditorGUILayout.BeginVertical();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
            }
            else
            {
                DrawCustomHeader(disableGroupContents);
                disableScroll = EditorGUILayout.BeginScrollView(disableScroll, GUILayout.Height(Mathf.Clamp(disableGroupContents.GetHeight(), 0, enableGroupContents.elementHeight * 10 + 10)));
                disableGroupContents.DoLayoutList();
                EditorGUILayout.EndScrollView();
                DrawButtons(disableGroupContents, true, true);
            }
        }
        else
        {
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
            EditorGUILayout.HelpBox("Groups are only usable with Toggles.", MessageType.Info);
            EditorGUILayout.EndVertical();            
        }
        EditorGUILayout.EndVertical();

        // End section.
        EditorGUILayout.Space();
        DrawLine();

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

    //Draws a line across the GUI
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
    private void DrawButtons(ReorderableList list, bool displayAdd, bool displayRemove)
    {
        // Obtain the Rect for the footer.
        Rect rect = GUILayoutUtility.GetRect(4, defaultFooterHeight, GUILayout.ExpandWidth(true));

        // Button contents.
        GUIContent iconToolbarPlus = EditorGUIUtility.TrIconContent("Toolbar Plus", "Add to list");
        GUIContent iconToolbarPlusMore = EditorGUIUtility.TrIconContent("Toolbar Plus More", "Choose to add to list");
        GUIContent iconToolbarMinus = EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove selection from list");

        GUIStyle preButton = "RL FooterButton";
        GUIStyle footerBackground = "RL Footer";

        // Modify the footer rect for the two buttons.
        float rightEdge = rect.xMax;
        float leftEdge = rightEdge - 8f;
        if (displayAdd)
            leftEdge -= 25;
        if (displayRemove)
            leftEdge -= 25;
        rect = new Rect(leftEdge, rect.y, rightEdge - leftEdge, rect.height);

        // Get Rects for each button.
        Rect addRect = new Rect(leftEdge + 4, rect.y - 3, 25, 13);
        Rect removeRect = new Rect(rightEdge - 29, rect.y - 3, 25, 13);
        
        // Draw the background for the footer on Repaint Events.
        if (Event.current.type == EventType.Repaint)
        {
            footerBackground.Draw(rect, false, false, false, false);
        }
        
        if (displayAdd)
        {
            // Honestly no clue on what this does.
            using (new EditorGUI.DisabledScope(
                list.onCanAddCallback != null && !list.onCanAddCallback(list)))
            {
                // Invoke the onAddCallback when the button is clicked followed by onChangedCallback.
                if (GUI.Button(addRect, list.onAddDropdownCallback != null ? iconToolbarPlusMore : iconToolbarPlus, new GUIStyle(preButton)))
                {
                    if (list.onAddDropdownCallback != null)
                        list.onAddDropdownCallback(addRect, list);
                    else 
                        list.onAddCallback?.Invoke(list);

                    list.onChangedCallback?.Invoke(list);

                    // If neither callback was provided, nothing will happen when the button is clicked.
                }
            }
        }

        // Exact same as above, just with the other button and removal callbacks.
        if (displayRemove)
        {
            using (new EditorGUI.DisabledScope(
                list.index < 0 || list.index >= list.count ||
                (list.onCanRemoveCallback != null && !list.onCanRemoveCallback(list))))
            {
                if (GUI.Button(removeRect, iconToolbarMinus, preButton))
                {
                    list.onRemoveCallback?.Invoke(list);

                    list.onChangedCallback?.Invoke(list);

                    // If neither callback was provided, nothing will happen when the button is clicked.
                }
            }
        }
    }

    // pageDirectory.onDrawHeaderCallback
    private void DrawDirectoryHeader(Rect rect)
    {
        GUI.Label(rect, "Directory");
    }

    // pageDirectory.onDrawElementCallback
    private void DrawDirectoryElement(Rect rect, int index, bool active, bool focused)
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
        EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width / 2, rect.height), item.name);
        EditorGUI.LabelField(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), (index == 0) ? "Default" : "");
    }

    // pageDirectory.onAddCallback
    private void AddDirectoryItem(ReorderableList list)
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
        directoryScroll.y = float.MaxValue;
    }

    // pageDirectory.onRemoveCallback
    private void RemoveDirectoryItem(ReorderableList list)
    {
        // Only continue if there is more than a single page.
        if (preset.Pages.Count > 1)
        {
            // Mark the preset as dirty and record the preset before the page's deletion. 
            EditorUtility.SetDirty(preset);
            Undo.RecordObject(preset, "Remove Page");
            preset.Pages.RemoveAt(list.index);

            // Focus on the prior page.
            if (list.index > 0)
                list.index -= 1;
        }          
    }

    // pageContents.onDrawHeaderCallback
    private void DrawPageHeader(Rect rect)
    {
        GUI.Label(rect, preset.Pages[pageDirectory.index].name);
    }

    // pageContents.onDrawElementCallback
    private void DrawPageElement(Rect rect, int index, bool active, bool focused)
    {
        // The item being drawn.
        PageItem item = preset.Pages[pageDirectory.index].Items[index];

        // Draw the item's name and type.
        EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width / 2, rect.height), (item.Type == PageItem.ItemType.Page) ? ((item.PageReference != null) ? item.PageReference.name : "None") : item.name);
        EditorGUI.LabelField(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), (item.Type == PageItem.ItemType.Toggle) ? "Toggle" : (item.Type == PageItem.ItemType.Page) ? "Page" : "Submenu");
    }

    // pageContents.onAddCallback
    private void AddPageItem(ReorderableList list)
    {
        // Continue if there is less than eight items on the page.
        if (list.list.Count < 8)
        {
            // Mark the preset as dirty and record the creation of a new page item.
            EditorUtility.SetDirty(preset);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            PageItem item = CreateInstance<PageItem>();
            Undo.RegisterCreatedObjectUndo(item, "Add Page Item");

            // Configure the new item and add it to the Asset.
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            item.hideFlags = HideFlags.HideInHierarchy;
            item.name = "Slot " + (list.list.Count + 1);
            AssetDatabase.AddObjectToAsset(item, _path);

            // Record the state of the current page before add the item to it.
            Undo.RecordObject(preset.Pages[pageDirectory.index], "Add Page Item");
            preset.Pages[pageDirectory.index].Items.Add(item);
            Undo.CollapseUndoOperations(group);

            // Focus the list on the new item.
            list.index = list.list.Count - 1;
        }            
    }

    // pageContents.onRemoveCallback
    private void RemovePageItem(ReorderableList list)
    {
        // Only continue if there is more than a single item on the page.
        if (list.list.Count > 1)
        {
            // Mark the preset as dirty and record the affected page before removing the item.
            EditorUtility.SetDirty(preset);
            Undo.RecordObject(preset.Pages[pageDirectory.index], "Remove Page Item");
            preset.Pages[pageDirectory.index].Items.RemoveAt(list.index);

            // Focus the list on the prior item.
            if (list.index > 0)
                list.index -= 1;
        }                    
    }

    // enableGroupContents.onDrawHeaderCallback
    private void DrawEnableGroupHeader(Rect rect)
    {
        GUI.Label(rect, new GUIContent("When Enabled...", "Modifies listed toggles when this toggle is enabled."));
    }

    // enableGroupContents.onDrawElementCallback
    private void DrawEnableGroupElement(Rect rect, int index, bool active, bool focused)
    {
        // The item being drawn.
        GroupItem item = preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup[index];

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
                    if (pageItem != preset.Pages[pageDirectory.index].Items[pageContents.index] && ((item.Item != null && pageItem == item.Item) || Array.IndexOf(preset.Pages[pageDirectory.index].Items[pageContents.index].GetEnableGroupItems(), pageItem) == -1))
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
            PageItem selected = allToggles[allToggles.IndexOf(remainingToggles[EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width / 2, rect.height), (remainingToggles.IndexOf(item.Item) != -1) ? remainingToggles.IndexOf(item.Item) : 0, toggleNames.ToArray())])];
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
            EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width / 2, rect.height), 0, new string[] { "None" });
        }

        // Display a dropdown selector to change how the item's toggle is affected when this group is fired.
        EditorGUI.BeginChangeCheck();
        GroupItem.GroupType itemType = (GroupItem.GroupType)EditorGUI.EnumPopup(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), item.Reaction);
        if (EditorGUI.EndChangeCheck())
        {
            // Mark the preset as dirty, record the item, and update it.
            EditorUtility.SetDirty(preset);
            Undo.RecordObject(item, "Group Modified");
            item.Reaction = itemType;
        }
    }

    // enableGroupContents.onAddCallback
    private void AddEnableGroupItem(ReorderableList list)
    {
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
        GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.Length + 1];
        preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.CopyTo(newArray, 0);
        newArray[newArray.GetUpperBound(0)] = item;

        // Record the selected page item and add the new item to the group.
        Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index], "Add Group Item");
        preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup = newArray;
        Undo.CollapseUndoOperations(group);

        // Focus the list on the new item.
        list.index = list.list.Count;
        enableScroll.y = float.MaxValue;
    }

    // enableGroupContents.onRemoveCallback
    private void RemoveEnableGroupItem(ReorderableList list)
    {
        // Only continue if the list contains an element.
        if (list.list.Count > 0)
        {
            // Mark the preset as dirty, record the current page item, and set the item to null.
            EditorUtility.SetDirty(preset);
            Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index], "Remove Group Item");         
            preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup[list.index] = null;
            
            // Copy the group into an array shortened by one element.
            GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.GetUpperBound(0)];
            int index = 0;
            for (int i = 0; i < preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.Length; i++)
            {
                // Only copy the group item if it isn't null.
                if (preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup[i] != null)
                {
                    newArray[index] = preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup[i];
                    index++;
                }                           
            }
            preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup = newArray;

            // Focus the list on the prior item.
            if (list.index > 0) 
                list.index -= 1;
        }
    }

    // disableGroupContents.onDrawHeaderCallback
    private void DrawDisableGroupHeader(Rect rect)
    {
        GUI.Label(rect, new GUIContent("When Disabled...", "Modifies listed toggles when this toggle is disabled."));
    }

    // disableGroupContents.onDrawElementCallback
    private void DrawDisableGroupElement(Rect rect, int index, bool active, bool focused)
    {
        // The item being drawn.
        GroupItem item = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup[index];

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
                    if (pageItem != preset.Pages[pageDirectory.index].Items[pageContents.index] && ((item.Item != null && pageItem == item.Item) || Array.IndexOf(preset.Pages[pageDirectory.index].Items[pageContents.index].GetDisableGroupItems(), pageItem) == -1))
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
            PageItem selected = allToggles[allToggles.IndexOf(remainingToggles[EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width / 2, rect.height), (remainingToggles.IndexOf(item.Item) != -1) ? remainingToggles.IndexOf(item.Item) : 0, toggleNames.ToArray())])];
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
            EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width / 2, rect.height), 0, new string[] { "None" });
        }

        // Display a dropdown selector to change how the item's toggle is affected when this group is fired.
        EditorGUI.BeginChangeCheck();
        GroupItem.GroupType itemType = (GroupItem.GroupType)EditorGUI.EnumPopup(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), item.Reaction);
        if (EditorGUI.EndChangeCheck())
        {
            // Mark the preset as dirty, record the item, and update it.
            EditorUtility.SetDirty(preset);
            Undo.RecordObject(item, "Group Modified");
            item.Reaction = itemType;
        }
    }

    // disableGroupContents.onAddCallback
    private void AddDisableGroupItem(ReorderableList list)
    {
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
        GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.Length + 1];
        preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.CopyTo(newArray, 0);
        newArray[newArray.GetUpperBound(0)] = item;

        // Record the selected page item and add the new item to the group.
        Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index], "Add Group Item");
        preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup = newArray;
        Undo.CollapseUndoOperations(group);

        // Focus the list on the new item.
        list.index = list.list.Count;
        enableScroll.y = float.MaxValue;
    }

    // disableGroupContents.onRemoveCallback
    private void RemoveDisableGroupItem(ReorderableList list)
    {
        // Only continue if the list contains an element.
        if (list.list.Count > 0)
        {
            // Mark the preset as dirty, record the current page item, and set the item to null.
            EditorUtility.SetDirty(preset);
            Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index], "Remove Group Item");
            preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup[list.index] = null;

            // Copy the group into an array shortened by one element.
            GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.GetUpperBound(0)];
            int index = 0;
            for (int i = 0; i < preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.Length; i++)
            {
                // Only copy the group item if it isn't null.
                if (preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup[i] != null)
                {
                    newArray[index] = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup[i];
                    index++;
                }
            }
            preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup = newArray;

            // Focus the list on the prior item.
            if (list.index > 0)
                list.index -= 1;
        }
    }
}