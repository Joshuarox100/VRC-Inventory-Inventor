using Boo.Lang;
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

[CustomEditor(typeof(InventoryPreset))]
public class InventoryPresetEditor : Editor 
{
    private InventoryPreset preset;

    public ReorderableList pageContents;
    public ReorderableList pageDirectory;
    public ReorderableList enableGroupContents;
    public ReorderableList disableGroupContents;

    public void OnEnable()
    {
        preset = (InventoryPreset)target;
        EditorUtility.SetDirty(preset);

        string _hasPath = AssetDatabase.GetAssetPath(preset.GetInstanceID());
        if (_hasPath == "")
        {
            return;
        }

        pageDirectory = new ReorderableList(preset.Pages, typeof(Page), true, true, true, true)
        {
            elementHeight = 18f
        };
        pageDirectory.drawHeaderCallback += DrawDirectoryHeader;
        pageDirectory.drawElementCallback += DrawDirectoryElement;
        pageDirectory.onAddCallback += AddDirectoryItem;
        pageDirectory.onRemoveCallback += RemoveDirectoryItem;

        pageContents = new ReorderableList(null, typeof(PageItem), true, true, true, true)
        {
            elementHeight = 18f
        };
        pageContents.drawHeaderCallback += DrawPageHeader;
        pageContents.drawElementCallback += DrawPageElement;
        pageContents.onAddCallback += AddPageItem;
        pageContents.onRemoveCallback += RemovePageItem;

        enableGroupContents = new ReorderableList(null, typeof(GroupItem), true, false, true, true)
        {
            elementHeight = 18f
        };
        enableGroupContents.drawHeaderCallback += DrawEnableGroupHeader;
        enableGroupContents.drawElementCallback += DrawEnableGroupElement;
        enableGroupContents.onAddCallback += AddEnableGroupItem;
        enableGroupContents.onRemoveCallback += RemoveEnableGroupItem;

        disableGroupContents = new ReorderableList(null, typeof(GroupItem), true, false, true, true)
        {
            elementHeight = 18f
        };
        disableGroupContents.drawHeaderCallback += DrawDisableGroupHeader;
        disableGroupContents.drawElementCallback += DrawDisableGroupElement;
        disableGroupContents.onAddCallback += AddDisableGroupItem;
        disableGroupContents.onRemoveCallback += RemoveDisableGroupItem;
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

        AssetDatabase.SaveAssets();
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
        int totalUsage = 0;
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
        if (totalUsage > 255)
        {
            EditorGUILayout.HelpBox("This preset uses more synced data than an Integer can hold.\n(Max: 255 | Used: " + totalUsage + ")", MessageType.Warning);
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
            Page page = ScriptableObject.CreateInstance<Page>();
            page.name = "Page " + (pageDirectory.list.Count + 1);
            page.hideFlags = HideFlags.HideInHierarchy;
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            AssetDatabase.AddObjectToAsset(page, _path);
            preset.Pages.Add(page);
            pageContents.list = preset.Pages[0].Items;
            AssetDatabase.SaveAssets();  
        }       
        else if (pageContents.list == null)
        {
            pageContents.list = preset.Pages[0].Items;
        }
        string[] pageNames = new string[pageDirectory.list.Count];
        for (int i = 0; i < pageDirectory.list.Count; i++)
        {
            pageNames[i] = (i + 1).ToString();
        }

        //Draw page renamer
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
        GUILayout.Label("Name:", GUILayout.ExpandWidth(false));
        if (pageDirectory.index >= pageDirectory.list.Count)
        {
            pageDirectory.index = pageDirectory.list.Count - 1;
        }
        else if (pageDirectory.index < 0)
        {
            pageDirectory.index = 0;
        }
        preset.Pages[pageDirectory.index].name = EditorGUILayout.TextField(preset.Pages[pageDirectory.index].name, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
        if (preset.Pages[pageDirectory.index].name == "")
        {
            preset.Pages[pageDirectory.index].name = "Page " + (pageDirectory.index + 1);
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
        EditorGUILayout.PrefixLabel("Icon");
        preset.Pages[pageDirectory.index].Icon = (Texture2D)EditorGUILayout.ObjectField(preset.Pages[pageDirectory.index].Icon, typeof(Texture2D), false);
        EditorGUILayout.EndHorizontal();
        if (pageDirectory.index != 0)
            preset.Pages[pageDirectory.index].Type = (Page.PageType)EditorGUILayout.EnumPopup(new GUIContent("Type"), preset.Pages[pageDirectory.index].Type);
        if (preset.Pages[pageDirectory.index].Type == Page.PageType.Submenu)
            preset.Pages[pageDirectory.index].Submenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Submenu"), preset.Pages[pageDirectory.index].Submenu, typeof(VRCExpressionsMenu), false);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
        EditorGUILayout.BeginVertical();
        if (pageDirectory != null)
            pageDirectory.DoLayoutList();
        EditorGUILayout.EndVertical();      
        EditorGUILayout.Space();
        DrawLine();

        int temp = pageDirectory.index;
        pageDirectory.index = (pageDirectory.index >= 0 && pageDirectory.index < pageDirectory.list.Count) ? pageDirectory.index : pageDirectory.index;
        if (temp != pageDirectory.index) 
            pageContents.list = preset.Pages[pageDirectory.index].Items;

        //Draw currently selected page contents
        GUILayout.Label("Item Settings", EditorStyles.boldLabel);
        
        if (preset.Pages[pageDirectory.index].Type == Page.PageType.Inventory)
        {
            if (pageContents.list.Count < 1)
            {
                PageItem item = ScriptableObject.CreateInstance<PageItem>();
                string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                item.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(item, _path);
                item.name = "Slot 1";
                preset.Pages[pageDirectory.index].Items.Add(item);
            }

            string[] itemNames = new string[pageContents.list.Count];
            for (int i = 0; i < itemNames.Length; i++)
            {
                itemNames[i] = (i + 1).ToString();
            }
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
            EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
            GUILayout.Label("Name:", GUILayout.ExpandWidth(false));

            pageContents.list = preset.Pages[pageDirectory.index].Items;
            if (pageContents.index >= pageContents.list.Count)
            {
                pageContents.index = pageContents.list.Count - 1;
            }
            else if (pageContents.index < 0)
            {
                pageContents.index = 0;
            }

            preset.Pages[pageDirectory.index].Items[pageContents.index].name = EditorGUILayout.TextField(preset.Pages[pageDirectory.index].Items[pageContents.index].name, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
            if (preset.Pages[pageDirectory.index].Items[pageContents.index].name == "")
            {
                preset.Pages[pageDirectory.index].Items[pageContents.index].name = "Slot " + (pageContents.index + 1);
            }
            else if (preset.Pages[pageDirectory.index].Items[pageContents.index].Type == PageItem.ItemType.Inventory && preset.Pages[pageDirectory.index].Items[pageContents.index].name != ((preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference != null) ? preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference.name : "None"))
            {
                preset.Pages[pageDirectory.index].Items[pageContents.index].name = (preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference != null) ? preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference.name : "None";
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
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Icon");
            preset.Pages[pageDirectory.index].Items[pageContents.index].Icon = (Texture2D)EditorGUILayout.ObjectField(preset.Pages[pageDirectory.index].Items[pageContents.index].Icon, typeof(Texture2D), false);
            EditorGUILayout.EndHorizontal();
            preset.Pages[pageDirectory.index].Items[pageContents.index].Type = (PageItem.ItemType)EditorGUILayout.EnumPopup(new GUIContent("Type"), preset.Pages[pageDirectory.index].Items[pageContents.index].Type);
            switch (preset.Pages[pageDirectory.index].Items[pageContents.index].Type)
            {
                case PageItem.ItemType.Toggle:
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(new GUIContent("Start"));
                    preset.Pages[pageDirectory.index].Items[pageContents.index].InitialState = Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(preset.Pages[pageDirectory.index].Items[pageContents.index].InitialState), new string[] { "Off", "On" }));
                    EditorGUILayout.EndHorizontal();
                    preset.Pages[pageDirectory.index].Items[pageContents.index].EnableClip = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Enable"), preset.Pages[pageDirectory.index].Items[pageContents.index].EnableClip, typeof(AnimationClip), false);
                    preset.Pages[pageDirectory.index].Items[pageContents.index].DisableClip = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Disable"), preset.Pages[pageDirectory.index].Items[pageContents.index].DisableClip, typeof(AnimationClip), false);
                    preset.Pages[pageDirectory.index].Items[pageContents.index].Sync = (PageItem.SyncMode)EditorGUILayout.EnumPopup(new GUIContent("Sync"), preset.Pages[pageDirectory.index].Items[pageContents.index].Sync);
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.EndVertical();
                    break;
                case PageItem.ItemType.Inventory:
                    string[] names = new string[preset.Pages.Count - 1];
                    int index = 0;
                    for (int i = 0; i < preset.Pages.Count; i++)
                    {
                        if (i != pageDirectory.index)
                        {
                            names[index] = preset.Pages[i].name;
                            index++;
                        }                            
                    }
                    if (preset.Pages.Count - 1 > 0)
                    {
                        preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference = preset.Pages[EditorGUILayout.Popup(new GUIContent("Inventory"), preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference != null ? preset.Pages.IndexOf(preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference) : 0, names)];
                    }
                    else
                    {
                        EditorGUILayout.Popup(new GUIContent("Inventory"), 0, new string[] { "None" });
                    }
                    break;
                case PageItem.ItemType.Submenu:
                    preset.Pages[pageDirectory.index].Items[pageContents.index].Submenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Submenu"), preset.Pages[pageDirectory.index].Items[pageContents.index].Submenu, typeof(VRCExpressionsMenu), false);
                    break;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
                
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
                GUILayout.Label("When Enabled...");
                if (GUILayout.Button(new GUIContent("Create Group")))
                {
                    GroupItem item = ScriptableObject.CreateInstance<GroupItem>();
                    item.hideFlags = HideFlags.HideInHierarchy;
                    item.Reaction = GroupItem.GroupType.Enable;
                    item.name = preset.Pages[pageDirectory.index].Items[pageContents.index].name + ": Enable Group Item " + (enableGroupContents.list.Count + 1);
                    item.Reaction = GroupItem.GroupType.Enable;
                    string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                    AssetDatabase.AddObjectToAsset(item, _path);                    
                    GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.Length + 1];
                    preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.CopyTo(newArray, 0);
                    newArray[newArray.GetUpperBound(0)] = item;
                    preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup = newArray;
                    enableGroupContents.list = preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup;     
                    AssetDatabase.SaveAssets();
                }
                EditorGUILayout.BeginVertical();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
            }
            else
            {
                enableGroupContents.DoLayoutList();
            }

            EditorGUILayout.Space();

            disableGroupContents.list = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup;
            if (disableGroupContents.list.Count == 0)
            {
                EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
                GUILayout.Label("When Disabled...");
                if (GUILayout.Button(new GUIContent("Create Group")))
                {
                    GroupItem item = ScriptableObject.CreateInstance<GroupItem>();
                    item.hideFlags = HideFlags.HideInHierarchy;
                    item.Reaction = GroupItem.GroupType.Enable;
                    item.name = preset.Pages[pageDirectory.index].Items[pageContents.index].name + ": Disable Group Item " + (disableGroupContents.list.Count + 1);
                    item.Reaction = GroupItem.GroupType.Disable;
                    string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                    AssetDatabase.AddObjectToAsset(item, _path);
                    GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.Length + 1];
                    preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.CopyTo(newArray, 0);
                    newArray[newArray.GetUpperBound(0)] = item;
                    preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup = newArray;
                    disableGroupContents.list = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup;
                    AssetDatabase.SaveAssets();
                }
                EditorGUILayout.BeginVertical();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
            }
            else
            {
                disableGroupContents.DoLayoutList();
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

    //Page Directory

    //Draws the list header
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

        if (item.name == "")
        {
            item.name = "Page " + (index + 1);
        }
    }

    //Adds a page when the add button is pressed, if room is available
    private void AddDirectoryItem(ReorderableList list)
    {
        Page page = ScriptableObject.CreateInstance<Page>();
        string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
        page.hideFlags = HideFlags.HideInHierarchy;
        page.name = "Page " + (pageDirectory.list.Count + 1);
        AssetDatabase.AddObjectToAsset(page, _path);
        preset.Pages.Add(page);
        pageContents.list = preset.Pages[0].Items;
        list.list = preset.Pages;
        list.index = list.list.Count - 1;        
        pageContents.list = preset.Pages[pageDirectory.index].Items;
        AssetDatabase.SaveAssets();
    }

    //Removes the currently selected item from the list, if at least two are present
    private void RemoveDirectoryItem(ReorderableList list)
    {
        if (preset.Pages.Count > 1)
        {           
            foreach (PageItem item in preset.Pages[list.index].Items)
            {
                foreach (GroupItem groupItem in item.EnableGroup)
                {
                    AssetDatabase.RemoveObjectFromAsset(groupItem);
                }
                foreach (GroupItem groupItem in item.DisableGroup)
                {
                    AssetDatabase.RemoveObjectFromAsset(groupItem);
                }
                AssetDatabase.RemoveObjectFromAsset(item);
            }
            AssetDatabase.RemoveObjectFromAsset(preset.Pages[list.index]);
            preset.Pages.RemoveAt(list.index);                    
        }          
        list.list = preset.Pages;
        if (list.index == list.list.Count && list.list.Count != 0)
            list.index -= 1;
        if (list.list.Count != 0)
            pageContents.list = preset.Pages[pageDirectory.index].Items;
        AssetDatabase.SaveAssets();
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
            EditorGUI.LabelField(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), (item.Type == PageItem.ItemType.Toggle) ? "Toggle" : (item.Type == PageItem.ItemType.Inventory) ? "Inventory" : "Submenu");
        }            
    }

    //Adds a slot when the add button is pressed, if room is available
    private void AddPageItem(ReorderableList list)
    {
        if (list.list.Count < 8)
        {
            PageItem item = ScriptableObject.CreateInstance<PageItem>();
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            item.hideFlags = HideFlags.HideInHierarchy;
            item.name = "Slot " + (list.list.Count + 1);
            AssetDatabase.AddObjectToAsset(item, _path);       
            preset.Pages[pageDirectory.index].Items.Add(item);            
        }            
        list.list = preset.Pages[pageDirectory.index].Items;
        list.index = list.list.Count - 1;
        AssetDatabase.SaveAssets();
    }

    //Removes the currently selected item from the list, if at least two are present
    private void RemovePageItem(ReorderableList list)
    {
        if (list.list.Count > 1)
        {           
            foreach (GroupItem groupItem in preset.Pages[pageDirectory.index].Items[list.index].EnableGroup)
            {
                AssetDatabase.RemoveObjectFromAsset(groupItem);
            }
            foreach (GroupItem groupItem in preset.Pages[pageDirectory.index].Items[list.index].DisableGroup)
            {
                AssetDatabase.RemoveObjectFromAsset(groupItem);
            }
            AssetDatabase.RemoveObjectFromAsset(preset.Pages[pageDirectory.index].Items[list.index]);
            preset.Pages[pageDirectory.index].Items.RemoveAt(list.index);                       
        }           
        list.list = preset.Pages[pageDirectory.index].Items;
        if (list.index == list.list.Count)
            list.index -= 1;
        AssetDatabase.SaveAssets();
    }

    //Group Contents

    //Enable Group

    //Draws the list header
    private void DrawEnableGroupHeader(Rect rect)
    {
        GUI.Label(rect, "When Enabled...");
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
                    item.Item = allToggles[0];
                }
                item.Item = allToggles[allToggles.IndexOf(remainingToggles[EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width / 2, rect.height), (remainingToggles.IndexOf(item.Item) != -1) ? remainingToggles.IndexOf(item.Item) : 0, toggleNames.ToArray())])];
            }
            else
            {
                EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width / 2, rect.height), 0, new string[] { "None" });
            }

            item.Reaction = (GroupItem.GroupType)EditorGUI.EnumPopup(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), item.Reaction);
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

        GroupItem item = ScriptableObject.CreateInstance<GroupItem>();
        string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
        item.hideFlags = HideFlags.HideInHierarchy;
        item.name = preset.Pages[pageDirectory.index].Items[pageContents.index].name + ": Group Item " + (list.list.Count + 1);
        item.Reaction = GroupItem.GroupType.Enable;
        AssetDatabase.AddObjectToAsset(item, _path);
        GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.Length + 1];
        preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.CopyTo(newArray, 0);
        newArray[newArray.GetUpperBound(0)] = item;
        preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup = newArray;
        list.list = preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup;
        AssetDatabase.SaveAssets();
    }

    //Removes the currently selected item from the list, if at least one is present
    private void RemoveEnableGroupItem(ReorderableList list)
    {
        if (list.list.Count > 0)
        {
            AssetDatabase.RemoveObjectFromAsset(preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup[list.index]);
            preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup[list.index] = null;
            GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].EnableGroup.Length - 1];
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
        AssetDatabase.SaveAssets();
    }

    //Disable Group

    //Draws the list header
    private void DrawDisableGroupHeader(Rect rect)
    {
        GUI.Label(rect, "When Disabled...");
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
                    item.Item = allToggles[0];
                }
                item.Item = allToggles[allToggles.IndexOf(remainingToggles[EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width / 2, rect.height), (remainingToggles.IndexOf(item.Item) != -1) ? remainingToggles.IndexOf(item.Item) : 0, toggleNames.ToArray())])];
            }
            else
            {
                EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width / 2, rect.height), 0, new string[] { "None" });
            }

            item.Reaction = (GroupItem.GroupType)EditorGUI.EnumPopup(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), item.Reaction);
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

        GroupItem item = ScriptableObject.CreateInstance<GroupItem>();
        string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
        item.hideFlags = HideFlags.HideInHierarchy;
        item.name = preset.Pages[pageDirectory.index].Items[pageContents.index].name + ": Group Item " + (list.list.Count + 1);
        item.Reaction = GroupItem.GroupType.Disable;
        AssetDatabase.AddObjectToAsset(item, _path);
        GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.Length + 1];
        preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.CopyTo(newArray, 0);
        newArray[newArray.GetUpperBound(0)] = item;
        preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup = newArray;
        list.list = preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup;
        AssetDatabase.SaveAssets();
    }

    //Removes the currently selected item from the list, if at least one is present
    private void RemoveDisableGroupItem(ReorderableList list)
    {
        if (list.list.Count > 0)
        {
            AssetDatabase.RemoveObjectFromAsset(preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup[list.index]);
            preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup[list.index] = null;
            GroupItem[] newArray = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].DisableGroup.Length - 1];
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
        AssetDatabase.SaveAssets();
    }
}