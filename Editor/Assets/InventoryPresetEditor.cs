using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

[CustomEditor(typeof(InventoryPreset))]
public class InventoryPresetEditor : Editor 
{
    private InventoryPreset preset;

    private ReorderableList pageContents;
    private ReorderableList pageDirectory;
    private ReorderableList enableGroupContents;
    private ReorderableList disableGroupContents;

    private float defaultHeaderHeight;
    private float defaultFooterHeight;

    private Vector2 directoryScroll;
    private Vector2 enableScroll;
    private Vector2 disableScroll;

    public void OnEnable()
    {
        preset = (InventoryPreset)target;

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
        //base.OnInspectorGUI();
        string _hasPath = AssetDatabase.GetAssetPath(preset.GetInstanceID());
        if (_hasPath == "")
        {
            return;
        }
        //Check if toggle limit exceeded
        int totalUsage = 1;
        foreach (Page page in preset.Pages)
        {
            if (page.Type == Page.PageType.Inventory)
            {
                foreach (PageItem pageItem in page.Items)
                {
                    if (pageItem.Type == PageItem.ItemType.Toggle)
                    {
                        switch (pageItem.Sync)
                        {
                            case PageItem.SyncMode.Off:
                                totalUsage += 1;
                                if (pageItem.EnableGroup.Length > 0)
                                    totalUsage++;
                                if (pageItem.DisableGroup.Length > 0)
                                    totalUsage++;
                                break;
                            case PageItem.SyncMode.Manual:
                                totalUsage += 3;
                                break;
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
        }
        if (totalUsage > 256)
        {
            EditorGUILayout.HelpBox("This preset uses more synced data than an Integer can hold.\n(Max: 256 | Used: " + totalUsage + ")", MessageType.Warning);
            EditorGUILayout.HelpBox("Data usage depends on both sync mode and group usage:\n\nOff = 1 + (1 for each Toggle Group used)\nManual = 3\nAuto = 3 + (1 for each Toggle Group used)", MessageType.Info);
        }

        serializedObject.Update();
        EditorGUILayout.BeginVertical();

        //List Page Settings
        GUILayout.Label("Page Settings", EditorStyles.boldLabel);
        pageDirectory.list = preset.Pages;
        //Make sure at least one page exists
        if (pageDirectory.list.Count == 0)
        {
            Page page = CreateInstance<Page>();
            page.name = "Page " + (pageDirectory.list.Count + 1);
            page.hideFlags = HideFlags.HideInHierarchy;
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            AssetDatabase.AddObjectToAsset(page, _path);
            preset.Pages.Add(page);
            pageContents.list = preset.Pages[0].Items;
            //AssetDatabase.SaveAssets();  
        }       

        string[] pageNames = new string[pageDirectory.list.Count];
        for (int i = 0; i < pageDirectory.list.Count; i++)
        {
            pageNames[i] = (i + 1).ToString();
        }

        //Draw page renamer
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
        GUILayout.Label(new GUIContent("Name:", "Name of the selected page."), GUILayout.ExpandWidth(false));
        if (pageDirectory.index >= pageDirectory.list.Count)
        {
            pageDirectory.index = pageDirectory.list.Count - 1;
        }
        else if (pageDirectory.index < 0)
        {
            pageDirectory.index = 0;
        }
        EditorGUI.BeginChangeCheck();
        string pageName = EditorGUILayout.TextField(preset.Pages[pageDirectory.index].name, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(preset);
            Undo.RecordObject(preset.Pages[pageDirectory.index], "Page Modified");
            preset.Pages[pageDirectory.index].name = pageName;
            if (preset.Pages[pageDirectory.index].name == "")
            {
                preset.Pages[pageDirectory.index].name = "Page " + (pageDirectory.index + 1);
            }
        }        
        //Draw page navigator
        if (GUILayout.Button('\u25C0'.ToString(), GUILayout.Width(30f)))
        {
            if (pageDirectory.index > 0)
            {
                pageDirectory.index--;
                pageContents.list = preset.Pages[pageDirectory.index].Items;
                GUI.FocusControl(null);
            }
        }
        EditorGUI.BeginChangeCheck();
        pageDirectory.index = EditorGUILayout.Popup(pageDirectory.index, pageNames, new GUIStyle(GUI.skin.GetStyle("Dropdown")), GUILayout.Width(30f));
        if (EditorGUI.EndChangeCheck())
        {
            pageContents.list = preset.Pages[pageDirectory.index].Items;
            GUI.FocusControl(null);
        }
        if (GUILayout.Button('\u25B6'.ToString(), GUILayout.Width(30f)))
        {
            if (pageDirectory.index < pageDirectory.list.Count - 1)
            {
                pageDirectory.index++;
                pageContents.list = preset.Pages[pageDirectory.index].Items;
                GUI.FocusControl(null);
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(new GUIContent("Icon", "Icon to use for the control."));
        EditorGUI.BeginChangeCheck();
        Texture2D pageIcon = (Texture2D)EditorGUILayout.ObjectField(preset.Pages[pageDirectory.index].Icon, typeof(Texture2D), false);
        EditorGUILayout.EndHorizontal();
        Page.PageType pageType = preset.Pages[pageDirectory.index].Type;
        if (pageDirectory.index != 0)
            pageType = (Page.PageType)EditorGUILayout.EnumPopup(new GUIContent("Type", "The type of page."), preset.Pages[pageDirectory.index].Type);
        VRCExpressionsMenu pageMenu = preset.Pages[pageDirectory.index].Submenu;
        if (preset.Pages[pageDirectory.index].Type == Page.PageType.Submenu)
            pageMenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Submenu", "The menu to use."), preset.Pages[pageDirectory.index].Submenu, typeof(VRCExpressionsMenu), false);
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(preset);
            Undo.RecordObject(preset.Pages[pageDirectory.index], "Page Modified");
            preset.Pages[pageDirectory.index].Icon = pageIcon;
            preset.Pages[pageDirectory.index].Type = pageType;
            preset.Pages[pageDirectory.index].Submenu = pageMenu;
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();

        DrawCustomHeader(pageDirectory);
        directoryScroll = EditorGUILayout.BeginScrollView(directoryScroll, GUILayout.Height(Mathf.Clamp(pageDirectory.GetHeight(), 0, pageDirectory.elementHeight * 10 + 10)));
        if (pageDirectory != null)
            pageDirectory.DoLayoutList();
        EditorGUILayout.EndScrollView();
        DrawButtons(pageDirectory, true, preset.Pages.Count > 1);
        
        EditorGUILayout.Space();
        DrawLine();

        //Will be null when the preset is first selected and a page already exists.
        if (pageContents.list == null)
        {
            pageContents.list = preset.Pages[pageDirectory.index].Items;
        }

        //Draw currently selected page contents
        GUILayout.Label("Item Settings", EditorStyles.boldLabel);
        
        if (preset.Pages[pageDirectory.index].Type == Page.PageType.Inventory)
        {
            if (pageContents.list.Count < 1)
            {
                PageItem item = CreateInstance<PageItem>();
                string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                item.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(item, _path);
                item.name = "Slot 1";
                preset.Pages[pageDirectory.index].Items.Add(item);
                //AssetDatabase.SaveAssets();
            }

            string[] itemNames = new string[pageContents.list.Count];
            for (int i = 0; i < itemNames.Length; i++)
            {
                itemNames[i] = (i + 1).ToString();
            }
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
            EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
            GUILayout.Label(new GUIContent("Name:", "The name of the item."), GUILayout.ExpandWidth(false));

            pageContents.list = preset.Pages[pageDirectory.index].Items;
            if (pageContents.index >= pageContents.list.Count)
            {
                pageContents.index = pageContents.list.Count - 1;
            }
            else if (pageContents.index < 0)
            {
                pageContents.index = 0;
            }

            bool nameChanged = false;
            EditorGUI.BeginChangeCheck();
            string itemName = EditorGUILayout.TextField(preset.Pages[pageDirectory.index].Items[pageContents.index].name, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
            {
                nameChanged = true;
            }
                    
            //Draw page navigator
            if (GUILayout.Button('\u25C0'.ToString(), GUILayout.Width(30f)))
            {
                if (pageContents.index > 0)
                {
                    pageContents.index--;
                    GUI.FocusControl(null);
                }
            }
            EditorGUI.BeginChangeCheck();
            pageContents.index = EditorGUILayout.Popup(pageContents.index, itemNames, new GUIStyle(GUI.skin.GetStyle("Dropdown")), GUILayout.Width(30f));
            if (EditorGUI.EndChangeCheck())
            {
                GUI.FocusControl(null);
            }
            if (GUILayout.Button('\u25B6'.ToString(), GUILayout.Width(30f)))
            {
                if (pageContents.index < pageContents.list.Count - 1)
                {
                    pageContents.index++;
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();

            Texture2D itemIcon = preset.Pages[pageDirectory.index].Items[pageContents.index].Icon;
            PageItem.ItemType itemType = preset.Pages[pageDirectory.index].Items[pageContents.index].Type;
            bool itemState = preset.Pages[pageDirectory.index].Items[pageContents.index].InitialState;
            AnimationClip itemEnable = preset.Pages[pageDirectory.index].Items[pageContents.index].EnableClip;
            AnimationClip itemDisable = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableClip;
            PageItem.SyncMode itemSync = preset.Pages[pageDirectory.index].Items[pageContents.index].Sync;
            Page itemPage = preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference;
            VRCExpressionsMenu itemMenu = preset.Pages[pageDirectory.index].Items[pageContents.index].Submenu;

            if (itemType != PageItem.ItemType.Page)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Icon", "The icon to use for the control."));
                itemIcon = (Texture2D)EditorGUILayout.ObjectField(itemIcon, typeof(Texture2D), false);
                EditorGUILayout.EndHorizontal();
            }
            itemType = (PageItem.ItemType)EditorGUILayout.EnumPopup(new GUIContent("Type", "The type of item."), itemType);
            switch (itemType)
            {
                case PageItem.ItemType.Toggle:
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(new GUIContent("Start", "What state the toggle starts in."));
                    itemState = Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(itemState), new string[] { "Disable", "Enable" }));
                    EditorGUILayout.EndHorizontal();
                    itemEnable = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Enable", "The Animation to play when the toggle is enabled."), itemEnable, typeof(AnimationClip), false);
                    itemDisable = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Disable", "The Animation to play when the toggle is disabled."), itemDisable, typeof(AnimationClip), false);
                    itemSync = (PageItem.SyncMode)EditorGUILayout.EnumPopup(new GUIContent("Sync", "How the toggle should sync with others."), itemSync);
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.EndVertical();
                    break;
                case PageItem.ItemType.Page:
                    string[] names = new string[preset.Pages.Count - 1];
                    Page[] pages = new Page[preset.Pages.Count - 1];
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
                    if (preset.Pages.Count - 1 > 0)
                    {
                        itemPage = preset.Pages[preset.Pages.IndexOf(pages[EditorGUILayout.Popup(new GUIContent("Page", "The page to direct to."), itemPage != null ? Array.IndexOf(pages, itemPage) : 0, names)])];
                    }
                    else
                    {
                        EditorGUILayout.Popup(new GUIContent("Page", "The page to direct to."), 0, new string[] { "None" });
                    }
                    break;
                case PageItem.ItemType.Submenu:
                    itemMenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Submenu", "The menu to use."), itemMenu, typeof(VRCExpressionsMenu), false);
                    break;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            if (EditorGUI.EndChangeCheck() || nameChanged)
            {
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index], "Item Modified");

                if (itemType == PageItem.ItemType.Page && itemName != ((itemPage != null) ? itemPage.name : "None"))
                {
                    itemName = (itemPage != null) ? itemPage.name : "None";
                    itemIcon = (itemPage != null) ? itemPage.Icon : itemIcon;
                }
                else if (itemName == "")
                {
                    itemName = "Slot " + (pageContents.index + 1);
                }

                preset.Pages[pageDirectory.index].Items[pageContents.index].name = itemName;
                preset.Pages[pageDirectory.index].Items[pageContents.index].Icon = itemIcon;
                preset.Pages[pageDirectory.index].Items[pageContents.index].Type = itemType;
                preset.Pages[pageDirectory.index].Items[pageContents.index].InitialState = itemState;
                preset.Pages[pageDirectory.index].Items[pageContents.index].EnableClip = itemEnable;
                preset.Pages[pageDirectory.index].Items[pageContents.index].DisableClip = itemDisable;
                preset.Pages[pageDirectory.index].Items[pageContents.index].Sync = itemSync;
                preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference = itemPage;
                preset.Pages[pageDirectory.index].Items[pageContents.index].Submenu = itemMenu;
            }

            pageContents.displayAdd = preset.Pages[pageDirectory.index].Items.Count < 8;
            pageContents.displayRemove = preset.Pages[pageDirectory.index].Items.Count > 1;

            EditorGUILayout.BeginVertical();
            if (pageContents != null)
                pageContents.DoLayoutList();
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
            EditorGUILayout.HelpBox("Only Inventory pages can be modified.", MessageType.Info);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.Space();
        DrawLine();

        //Toggle Groups
        if (preset.Pages[pageDirectory.index].Type == Page.PageType.Inventory && preset.Pages[pageDirectory.index].Items[pageContents.index].Type == PageItem.ItemType.Toggle)
        {
            GUILayout.Label(preset.Pages[pageDirectory.index].Items[pageContents.index].name + "'s Groups", EditorStyles.boldLabel);
        }
        else
        {
            GUILayout.Label("Toggle Groups", EditorStyles.boldLabel);
        }
        EditorGUILayout.BeginVertical();
        if (preset.Pages[pageDirectory.index].Type == Page.PageType.Submenu)
        {
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
            EditorGUILayout.HelpBox("Only Inventory pages can be modified.", MessageType.Info);
            EditorGUILayout.EndVertical();
        }
        else if (preset.Pages[pageDirectory.index].Items[pageContents.index].Type == PageItem.ItemType.Toggle)
        {
            enableGroupContents.list = preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup;
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

                    item.hideFlags = HideFlags.HideInHierarchy;
                    item.Reaction = GroupItem.GroupType.Enable;
                    item.name = preset.Pages[pageDirectory.index].Items[pageContents.index].name + ": Enable Group Item " + (enableGroupContents.list.Count + 1);
                    item.Reaction = GroupItem.GroupType.Enable;
                    string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                    AssetDatabase.AddObjectToAsset(item, _path);                    
                    GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.Length + 1];
                    preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.CopyTo(newArray, 0);
                    newArray[newArray.GetUpperBound(0)] = item;

                    Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index], "Add Group Item");
                    preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup = newArray;
                    Undo.CollapseUndoOperations(group);

                    enableGroupContents.list = preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup;     
                    //AssetDatabase.SaveAssets();
                }
                EditorGUILayout.BeginVertical();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
            }
            else
            {
                DrawCustomHeader(enableGroupContents);
                enableScroll = EditorGUILayout.BeginScrollView(enableScroll, GUILayout.Height(Mathf.Clamp(enableGroupContents.GetHeight(), 0, enableGroupContents.elementHeight * 10 + 10)));
                enableGroupContents.DoLayoutList();
                EditorGUILayout.EndScrollView();
                DrawButtons(enableGroupContents, true, true);
            }

            EditorGUILayout.Space();

            disableGroupContents.list = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup;
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

                    item.hideFlags = HideFlags.HideInHierarchy;
                    item.Reaction = GroupItem.GroupType.Enable;
                    item.name = preset.Pages[pageDirectory.index].Items[pageContents.index].name + ": Disable Group Item " + (disableGroupContents.list.Count + 1);
                    item.Reaction = GroupItem.GroupType.Disable;
                    string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                    AssetDatabase.AddObjectToAsset(item, _path);
                    GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.Length + 1];
                    preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.CopyTo(newArray, 0);
                    newArray[newArray.GetUpperBound(0)] = item;

                    Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index], "Add Group Item");
                    preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup = newArray;
                    Undo.CollapseUndoOperations(group);

                    disableGroupContents.list = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup;
                    //AssetDatabase.SaveAssets();
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
        EditorGUILayout.Space();
        DrawLine();

        EditorGUILayout.EndVertical();
        serializedObject.ApplyModifiedProperties();

        // Save Changes before entering Play Mode
        if (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
        {
            SaveChanges();
        }
    }

    //Removes unused SubAssets
    private void SaveChanges()
    {
        AssetDatabase.SaveAssets();

        UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(preset.GetInstanceID()));

        bool[] used = new bool[objects.Length];

        for (int i = 0; i < objects.Length; i++)
        {
            //switch can't be used here because typeof doesn't return a constant.
            if (objects[i].GetType() == typeof(InventoryPreset))
            {
                if ((InventoryPreset)objects[i] == preset)
                    used[i] = true;
                else
                    continue;
            }
            else if (objects[i].GetType() == typeof(Page))
            {
                if (preset.Pages.Contains((Page)objects[i]))
                    used[i] = true;
                else
                    continue;
            }
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

        for (int i = 0; i < objects.Length; i++)
            if (!used[i])
                AssetDatabase.RemoveObjectFromAsset(objects[i]);

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
    //Reorderable List Code
    */

    // Modified version of Unity's Header Drawing code for ReorderableList

    private void DrawCustomHeader(ReorderableList list)
    {
        Rect rect = GUILayoutUtility.GetRect(0, defaultHeaderHeight, GUILayout.ExpandWidth(true));

        GUIStyle headerBackground = "RL Header";

        if (Event.current.type == EventType.Repaint)
            headerBackground.Draw(rect, false, false, false, false);

        rect.xMin += ReorderableList.Defaults.padding;
        rect.xMax -= ReorderableList.Defaults.padding;
        rect.height -= 2;
        rect.y += 1;

        list.drawHeaderCallback?.Invoke(rect);
        // No access to default header.
    }

    // Modified version of Unity's Button Drawing code for ReorderableList.

    private void DrawButtons(ReorderableList list, bool displayAdd, bool displayRemove)
    {
        Rect rect = GUILayoutUtility.GetRect(4, defaultFooterHeight, GUILayout.ExpandWidth(true));

        GUIContent iconToolbarPlus = EditorGUIUtility.TrIconContent("Toolbar Plus", "Add to list");
        GUIContent iconToolbarPlusMore = EditorGUIUtility.TrIconContent("Toolbar Plus More", "Choose to add to list");
        GUIContent iconToolbarMinus = EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove selection from list");

        GUIStyle preButton = "RL FooterButton";
        GUIStyle footerBackground = "RL Footer";

        float rightEdge = rect.xMax;
        float leftEdge = rightEdge - 8f;
        if (displayAdd)
            leftEdge -= 25;
        if (displayRemove)
            leftEdge -= 25;
        rect = new Rect(leftEdge, rect.y, rightEdge - leftEdge, rect.height);
        Rect addRect = new Rect(leftEdge + 4, rect.y - 3, 25, 13);
        Rect removeRect = new Rect(rightEdge - 29, rect.y - 3, 25, 13);
        
        if (Event.current.type == EventType.Repaint)
        {
            footerBackground.Draw(rect, false, false, false, false);
        }
        
        if (displayAdd)
        {
            using (new EditorGUI.DisabledScope(
                list.onCanAddCallback != null && !list.onCanAddCallback(list)))
            {
                if (GUI.Button(addRect, list.onAddDropdownCallback != null ? iconToolbarPlusMore : iconToolbarPlus, new GUIStyle(preButton)))
                {
                    if (list.onAddDropdownCallback != null)
                        list.onAddDropdownCallback(addRect, list);
                    else 
                        list.onAddCallback?.Invoke(list);

                    //Default add method unaccessable.

                    list.onChangedCallback?.Invoke(list);
                }
            }
        }
        if (displayRemove)
        {
            using (new EditorGUI.DisabledScope(
                list.index < 0 || list.index >= list.count ||
                (list.onCanRemoveCallback != null && !list.onCanRemoveCallback(list))))
            {
                if (GUI.Button(removeRect, iconToolbarMinus, preButton))
                {
                    //Default removal method unaccessable

                    list.onRemoveCallback?.Invoke(list);

                    list.onChangedCallback?.Invoke(list);
                }
            }
        }
    }

    //Page Directory

    private void DrawDirectoryHeader(Rect rect)
    {
        GUI.Label(rect, "Directory");
    }

    //Draws each element
    private void DrawDirectoryElement(Rect rect, int index, bool active, bool focused)
    {
        Page item = preset.Pages[index];
        if (index == 0 && item.Type != Page.PageType.Inventory)
            item.Type = Page.PageType.Inventory;

        EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width / 2, rect.height), item.name);
        EditorGUI.LabelField(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), (item.Type == Page.PageType.Inventory) ? ((index == 0) ? "Default" : "Inventory") : "Submenu");
    }

    //Adds a page when the add button is pressed, if room is available
    private void AddDirectoryItem(ReorderableList list)
    {
        EditorUtility.SetDirty(preset);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Page page = CreateInstance<Page>();
        Undo.RegisterCreatedObjectUndo(page, "Add Page");

        string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
        page.hideFlags = HideFlags.HideInHierarchy;
        page.name = "Page " + (pageDirectory.list.Count + 1);
        AssetDatabase.AddObjectToAsset(page, _path);

        Undo.RecordObject(preset, "Add Page");
        preset.Pages.Add(page);
        Undo.CollapseUndoOperations(group);

        pageContents.list = preset.Pages[0].Items;
        list.list = preset.Pages;
        list.index = list.list.Count - 1;        
        pageContents.list = preset.Pages[pageDirectory.index].Items;

        directoryScroll.y = float.MaxValue;
        //AssetDatabase.SaveAssets();
    }

    //Removes the currently selected item from the list, if at least two are present
    private void RemoveDirectoryItem(ReorderableList list)
    {
        EditorUtility.SetDirty(preset);
        if (preset.Pages.Count > 1)
        {
            Undo.RecordObject(preset, "Remove Page");
            preset.Pages.RemoveAt(list.index);                    
        }          
        list.list = preset.Pages;
        if (list.index == list.list.Count && list.list.Count != 0)
            list.index -= 1;
        if (list.list.Count != 0)
            pageContents.list = preset.Pages[pageDirectory.index].Items;
    }

    //Page Contents

    //Draws the list header
    private void DrawPageHeader(Rect rect)
    {
        if (pageDirectory.index >= 0 && pageDirectory.index < pageDirectory.list.Count)
            GUI.Label(rect, preset.Pages[pageDirectory.index].name);
    }

    //Draws each element
    private void DrawPageElement(Rect rect, int index, bool active, bool focused)
    {
        pageContents.list = preset.Pages[pageDirectory.index].Items;
        if (index < pageContents.list.Count && index >= 0 && pageDirectory.index >= 0 && pageDirectory.index < pageDirectory.list.Count)
        {
            PageItem item = preset.Pages[pageDirectory.index].Items[index];

            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width / 2, rect.height), item.name);
            EditorGUI.LabelField(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), (item.Type == PageItem.ItemType.Toggle) ? "Toggle" : (item.Type == PageItem.ItemType.Page) ? "Page" : "Submenu");
        }            
    }

    //Adds a slot when the add button is pressed, if room is available
    private void AddPageItem(ReorderableList list)
    {
        if (list.list.Count < 8)
        {
            EditorUtility.SetDirty(preset);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            PageItem item = CreateInstance<PageItem>();
            Undo.RegisterCreatedObjectUndo(item, "Add Page Item");

            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            item.hideFlags = HideFlags.HideInHierarchy;
            item.name = "Slot " + (list.list.Count + 1);
            AssetDatabase.AddObjectToAsset(item, _path);

            Undo.RecordObject(preset.Pages[pageDirectory.index], "Add Page Item");
            preset.Pages[pageDirectory.index].Items.Add(item);

            Undo.CollapseUndoOperations(group);
        }            
        list.list = preset.Pages[pageDirectory.index].Items;
        list.index = list.list.Count - 1;
        //AssetDatabase.SaveAssets();
    }

    //Removes the currently selected item from the list, if at least two are present
    private void RemovePageItem(ReorderableList list)
    {
        if (list.list.Count > 1)
        {
            EditorUtility.SetDirty(preset);
            Undo.RecordObject(preset.Pages[pageDirectory.index], "Remove Page Item");
            preset.Pages[pageDirectory.index].Items.RemoveAt(list.index);                       
        }           
        list.list = preset.Pages[pageDirectory.index].Items;
        if (list.index == list.list.Count)
            list.index -= 1;
    }

    //Group Contents

    //Enable Group

    //Draws the list header
    private void DrawEnableGroupHeader(Rect rect)
    {
        GUI.Label(rect, new GUIContent("When Enabled...", "Modifies listed toggles when this toggle is enabled."));
    }

    //Draws each element
    private void DrawEnableGroupElement(Rect rect, int index, bool active, bool focused)
    {
        enableGroupContents.list = preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup;
        if (index < enableGroupContents.list.Count && index >= 0 && pageContents.index < pageContents.list.Count && pageContents.index >= 0 && pageDirectory.index >= 0 && pageDirectory.index < pageDirectory.list.Count)
        {
            GroupItem item = preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup[index];

            List<PageItem> allToggles = new List<PageItem>();
            List<PageItem> remainingToggles = new List<PageItem>();
            List<string> toggleNames = new List<string>();
            foreach (Page page in preset.Pages)
            {
                if (page.Type == Page.PageType.Inventory)
                {
                    foreach (PageItem pageItem in page.Items)
                    {
                        if (pageItem.Type == PageItem.ItemType.Toggle)
                        {
                            allToggles.Add(pageItem);
                            if (pageItem != preset.Pages[pageDirectory.index].Items[pageContents.index] && ((item.Item != null && pageItem == item.Item) || Array.IndexOf(preset.Pages[pageDirectory.index].Items[pageContents.index].GetEnableGroupItems(), pageItem) == -1))
                            {
                                remainingToggles.Add(pageItem);
                                toggleNames.Add(page.name + ": " + pageItem.name);
                            }
                        }
                    }
                }                
            }

            if (allToggles.Count > 0 && remainingToggles.Count > 0)
            {
                if (item.Item == null)
                {
                    item.Item = remainingToggles[0];
                }
                EditorGUI.BeginChangeCheck();
                PageItem selected = allToggles[allToggles.IndexOf(remainingToggles[EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width / 2, rect.height), (remainingToggles.IndexOf(item.Item) != -1) ? remainingToggles.IndexOf(item.Item) : 0, toggleNames.ToArray())])];
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(preset);
                    Undo.RecordObject(item, "Group Modified");
                    item.Item = selected;
                }
            }
            else
            {
                EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width / 2, rect.height), 0, new string[] { "None" });
            }

            EditorGUI.BeginChangeCheck();
            GroupItem.GroupType itemType = (GroupItem.GroupType)EditorGUI.EnumPopup(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), item.Reaction);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(item, "Group Modified");
                item.Reaction = itemType;
            }
        }
    }

    //Adds a slot when the add button is pressed, if room is available
    private void AddEnableGroupItem(ReorderableList list)
    {
        int totalUsage = 0;
        foreach (Page page in preset.Pages)
        {
            if (page.Type == Page.PageType.Inventory)
            {
                foreach (PageItem pageItem in page.Items)
                {
                    if (pageItem.Type == PageItem.ItemType.Toggle)
                    {
                        totalUsage += 1;
                    }
                }
            }            
        }
        if (totalUsage - 1 == list.list.Count)
        {
            return;
        }

        EditorUtility.SetDirty(preset);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        GroupItem item = CreateInstance<GroupItem>();
        Undo.RegisterCreatedObjectUndo(item, "Add Group Item");

        string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
        item.hideFlags = HideFlags.HideInHierarchy;
        item.name = preset.Pages[pageDirectory.index].Items[pageContents.index].name + ": Group Item " + (list.list.Count + 1);
        item.Reaction = GroupItem.GroupType.Enable;
        AssetDatabase.AddObjectToAsset(item, _path);
        GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.Length + 1];
        preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.CopyTo(newArray, 0);
        newArray[newArray.GetUpperBound(0)] = item;

        Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index], "Add Group Item");
        preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup = newArray;
        Undo.CollapseUndoOperations(group);

        list.list = preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup;

        enableScroll.y = float.MaxValue;
        //AssetDatabase.SaveAssets();
    }

    //Removes the currently selected item from the list, if at least one is present
    private void RemoveEnableGroupItem(ReorderableList list)
    {
        if (list.list.Count > 0)
        {
            EditorUtility.SetDirty(preset);
            Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index], "Remove Group Item");
            preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup[list.index] = null;
            GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.GetUpperBound(0)];
            int index = 0;
            for (int i = 0; i < preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.Length; i++)
            {
                if (preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup[i] != null)
                {
                    newArray[index] = preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup[i];
                    index++;
                }                           
            }
            preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup = newArray;                   
        }
        list.list = preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup;
        if (list.index == list.list.Count)
            list.index -= 1;
    }

    //Disable Group

    //Draws the list header
    private void DrawDisableGroupHeader(Rect rect)
    {
        GUI.Label(rect, new GUIContent("When Disabled...", "Modifies listed toggles when this toggle is disabled."));
    }

    //Draws each element
    private void DrawDisableGroupElement(Rect rect, int index, bool active, bool focused)
    {
        disableGroupContents.list = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup;
        if (index < disableGroupContents.list.Count && index >= 0 && pageContents.index < pageContents.list.Count && pageContents.index >= 0 && pageDirectory.index >= 0 && pageDirectory.index < pageDirectory.list.Count)
        {
            GroupItem item = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup[index];

            List<PageItem> allToggles = new List<PageItem>();
            List<PageItem> remainingToggles = new List<PageItem>();
            List<string> toggleNames = new List<string>();
            foreach (Page page in preset.Pages)
            {
                if (page.Type == Page.PageType.Inventory)
                {
                    foreach (PageItem pageItem in page.Items)
                    {
                        if (pageItem.Type == PageItem.ItemType.Toggle)
                        {
                            allToggles.Add(pageItem);
                            if (pageItem != preset.Pages[pageDirectory.index].Items[pageContents.index] && ((item.Item != null && pageItem == item.Item) || Array.IndexOf(preset.Pages[pageDirectory.index].Items[pageContents.index].GetDisableGroupItems(), pageItem) == -1))
                            {
                                remainingToggles.Add(pageItem);
                                toggleNames.Add(page.name + ": " + pageItem.name);
                            }
                        }
                    }
                }
            }

            if (allToggles.Count > 0 && remainingToggles.Count > 0)
            {
                if (item.Item == null)
                {
                    item.Item = remainingToggles[0];
                }
                EditorGUI.BeginChangeCheck();
                PageItem selected = allToggles[allToggles.IndexOf(remainingToggles[EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width / 2, rect.height), (remainingToggles.IndexOf(item.Item) != -1) ? remainingToggles.IndexOf(item.Item) : 0, toggleNames.ToArray())])];
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(preset);
                    Undo.RecordObject(item, "Group Modified");
                    item.Item = selected;
                }
            }
            else
            {
                EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width / 2, rect.height), 0, new string[] { "None" });
            }

            EditorGUI.BeginChangeCheck();
            GroupItem.GroupType itemType = (GroupItem.GroupType)EditorGUI.EnumPopup(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), item.Reaction);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(preset);
                Undo.RecordObject(item, "Group Modified");
                item.Reaction = itemType;
            }
        }
    }

    //Adds a slot when the add button is pressed, if room is available
    private void AddDisableGroupItem(ReorderableList list)
    {
        int totalUsage = 0;
        foreach (Page page in preset.Pages)
        {
            if (page.Type == Page.PageType.Inventory)
            {
                foreach (PageItem pageItem in page.Items)
                {
                    if (pageItem.Type == PageItem.ItemType.Toggle)
                    {
                        totalUsage += 1;
                    }
                }
            }
        }
        if (totalUsage - 1 == list.list.Count)
        {
            return;
        }

        EditorUtility.SetDirty(preset);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup(); 
        GroupItem item = CreateInstance<GroupItem>();
        Undo.RegisterCreatedObjectUndo(item, "Add Group Item");

        string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
        item.hideFlags = HideFlags.HideInHierarchy;
        item.name = preset.Pages[pageDirectory.index].Items[pageContents.index].name + ": Group Item " + (list.list.Count + 1);
        item.Reaction = GroupItem.GroupType.Disable;
        AssetDatabase.AddObjectToAsset(item, _path);
        GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.Length + 1];
        preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.CopyTo(newArray, 0);
        newArray[newArray.GetUpperBound(0)] = item;

        Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index], "Add Group Item");
        preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup = newArray;
        Undo.CollapseUndoOperations(group);

        list.list = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup;

        disableScroll.y = float.MaxValue;
        //AssetDatabase.SaveAssets();
    }

    //Removes the currently selected item from the list, if at least one is present
    private void RemoveDisableGroupItem(ReorderableList list)
    {
        if (list.list.Count > 0)
        {
            EditorUtility.SetDirty(preset);
            Undo.RecordObject(preset.Pages[pageDirectory.index].Items[pageContents.index], "Remove Group Item");
            preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup[list.index] = null;
            GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.GetUpperBound(0)];
            int index = 0;
            for (int i = 0; i < preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.Length; i++)
            {
                if (preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup[i] != null)
                {
                    newArray[index] = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup[i];
                    index++;
                }
            }            
            preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup = newArray;                    
        }
        list.list = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup;
        if (list.index == list.list.Count)
            list.index -= 1;
    }
}