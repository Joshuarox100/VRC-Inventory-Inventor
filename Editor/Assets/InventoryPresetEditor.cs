using Boo.Lang;
using System.Text.RegularExpressions;
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
    public ReorderableList groupContents;

    public int pageList = 0;

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

        groupContents = new ReorderableList(null, typeof(GroupItem), true, false, true, true)
        {
            elementHeight = 18f
        };
        groupContents.drawHeaderCallback += DrawGroupHeader;
        groupContents.drawElementCallback += DrawGroupElement;
        groupContents.onAddCallback += AddGroupItem;
        groupContents.onRemoveCallback += RemoveGroupItem;
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

        if (groupContents != null)
        {
            groupContents.drawHeaderCallback -= DrawGroupHeader;
            groupContents.drawElementCallback -= DrawGroupElement;
            groupContents.onAddCallback -= AddGroupItem;
            groupContents.onRemoveCallback -= RemoveGroupItem;
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
                    if (pageItem.Type == PageItem.ItemType.Toggle && pageItem.Sync != 0)
                    {
                        totalUsage += 1;
                    }
                }
            }
        }
        foreach (Page page in preset.ExtraPages)
        {
            if (page.Type == Page.PageType.Inventory)
            {
                foreach (PageItem pageItem in page.Items)
                {
                    if (pageItem.Type == PageItem.ItemType.Toggle && pageItem.Sync != 0)
                    {
                        totalUsage += 1;
                    }
                }
            }
        }
        if (totalUsage > 85)
        {
            EditorGUILayout.HelpBox("This preset exceeds the max number of synced toggles.\n(Max: 85 | Used: " + totalUsage + ")", MessageType.Warning);
        }
        serializedObject.Update();
        EditorGUILayout.BeginVertical();
        //List Inventory Settings
        GUILayout.Label("Inventory Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
        GUILayout.Label("Name:", GUILayout.ExpandWidth(false));
        preset.MenuName = EditorGUILayout.TextField(preset.MenuName, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Icon");
        preset.Icon = (Texture2D)EditorGUILayout.ObjectField(preset.Icon, typeof(Texture2D), false);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        DrawLine();

        GUILayout.Label("Page Settings", EditorStyles.boldLabel);
        pageDirectory.list = ((pageList = GUILayout.Toolbar(pageList, new string[] { "Main", "Extra" })) == 1) ? preset.ExtraPages : preset.Pages;
        //Make sure at least one page exists
        if (pageDirectory.list.Count == 0)
        {
            Page page = ScriptableObject.CreateInstance<Page>();
            page.name = ((pageList == 1) ? "Extra " : "Page ") + (pageDirectory.list.Count + 1);
            page.hideFlags = HideFlags.HideInHierarchy;
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            AssetDatabase.AddObjectToAsset(page, _path);
            switch (pageList)
            {
                case 0:
                    preset.Pages.Add(page);
                    pageContents.list = preset.Pages[0].Items;
                    break;
                case 1:
                    preset.ExtraPages.Add(page);
                    pageContents.list = preset.ExtraPages[0].Items;
                    break;
            }            
            AssetDatabase.SaveAssets();
        }       
        else if (pageContents.list == null)
        {
            pageContents.list = (pageList == 1) ? preset.ExtraPages[0].Items : preset.Pages[0].Items;
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
        switch (pageList)
        {
            case 0:
                preset.Pages[pageDirectory.index].name = EditorGUILayout.TextField(preset.Pages[pageDirectory.index].name, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
                if (preset.Pages[pageDirectory.index].name == "")
                {
                    preset.Pages[pageDirectory.index].name = "Page " + (pageDirectory.index + 1);
                }
                break;
            case 1:
                preset.ExtraPages[pageDirectory.index].name = EditorGUILayout.TextField(preset.ExtraPages[pageDirectory.index].name, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
                if (preset.ExtraPages[pageDirectory.index].name == "")
                {
                    preset.ExtraPages[pageDirectory.index].name = "Page " + (pageDirectory.index + 1);
                }
                break;
        }     
        //Draw page navigator
        if (GUILayout.Button('\u25C0'.ToString(), GUILayout.Width(30f)))
        {
            if (pageDirectory.index > 0)
            {
                pageDirectory.index--;
                pageContents.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items : preset.Pages[pageDirectory.index].Items;
                GUI.FocusControl(null);
            }
        }
        EditorGUI.BeginChangeCheck();
        pageDirectory.index = EditorGUILayout.Popup(pageDirectory.index, pageNames, new GUIStyle(GUI.skin.GetStyle("Dropdown")), GUILayout.Width(30f));
        if (EditorGUI.EndChangeCheck())
        {
            pageContents.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items : preset.Pages[pageDirectory.index].Items;
            GUI.FocusControl(null);
        }
        if (GUILayout.Button('\u25B6'.ToString(), GUILayout.Width(30f)))
        {
            if (pageDirectory.index < pageDirectory.list.Count - 1)
            {
                pageDirectory.index++;
                pageContents.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items : preset.Pages[pageDirectory.index].Items;
                GUI.FocusControl(null);
            }
        }
        EditorGUILayout.EndHorizontal();
        switch (pageList)
        {
            case 0:
                EditorGUILayout.BeginVertical();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Icon");
                preset.Pages[pageDirectory.index].Icon = (Texture2D)EditorGUILayout.ObjectField(preset.Pages[pageDirectory.index].Icon, typeof(Texture2D), false);
                EditorGUILayout.EndHorizontal();
                preset.Pages[pageDirectory.index].Type = (Page.PageType)EditorGUILayout.EnumPopup(new GUIContent("Type"), preset.Pages[pageDirectory.index].Type);
                if (preset.Pages[pageDirectory.index].Type == Page.PageType.Submenu)
                    preset.Pages[pageDirectory.index].Submenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Submenu"), preset.Pages[pageDirectory.index].Submenu, typeof(VRCExpressionsMenu), false);
                EditorGUILayout.EndVertical();
                break;
            case 1:
                EditorGUILayout.BeginVertical();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Icon");
                preset.ExtraPages[pageDirectory.index].Icon = (Texture2D)EditorGUILayout.ObjectField(preset.ExtraPages[pageDirectory.index].Icon, typeof(Texture2D), false);
                EditorGUILayout.EndHorizontal();
                preset.ExtraPages[pageDirectory.index].Type = (Page.PageType)EditorGUILayout.EnumPopup(new GUIContent("Type"), preset.ExtraPages[pageDirectory.index].Type);
                if (preset.ExtraPages[pageDirectory.index].Type == Page.PageType.Submenu)
                    preset.ExtraPages[pageDirectory.index].Submenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Submenu"), preset.ExtraPages[pageDirectory.index].Submenu, typeof(VRCExpressionsMenu), false);
                EditorGUILayout.EndVertical();
                break;
        }
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
            pageContents.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items : preset.Pages[pageDirectory.index].Items;

        //Draw currently selected page contents
        GUILayout.Label("Item Settings", EditorStyles.boldLabel);
        
        if (((pageList == 1) ? preset.ExtraPages[pageDirectory.index] : preset.Pages[pageDirectory.index]).Type == Page.PageType.Inventory)
        {
            if (pageContents.list.Count < 1)
            {
                PageItem item = ScriptableObject.CreateInstance<PageItem>();
                string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                item.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(item, _path);
                item.name = "Slot 1";
                switch (pageList)
                {
                    case 0:
                        preset.Pages[pageDirectory.index].Items.Add(item);
                        break;
                    case 1:
                        preset.ExtraPages[pageDirectory.index].Items.Add(item);
                        break;
                }
            }

            string[] itemNames = new string[pageContents.list.Count];
            for (int i = 0; i < itemNames.Length; i++)
            {
                itemNames[i] = (i + 1).ToString();
            }
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
            EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
            GUILayout.Label("Name:", GUILayout.ExpandWidth(false));

            pageContents.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items : preset.Pages[pageDirectory.index].Items;
            if (pageContents.index >= pageContents.list.Count)
            {
                pageContents.index = pageContents.list.Count - 1;
            }
            else if (pageContents.index < 0)
            {
                pageContents.index = 0;
            }

            switch (pageList)
            {
                case 0:
                    preset.Pages[pageDirectory.index].Items[pageContents.index].name = EditorGUILayout.TextField(preset.Pages[pageDirectory.index].Items[pageContents.index].name, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
                    if (preset.Pages[pageDirectory.index].Items[pageContents.index].name == "")
                    {
                        preset.Pages[pageDirectory.index].Items[pageContents.index].name = "Slot " + (pageContents.index + 1);
                    }
                    else if (preset.Pages[pageDirectory.index].Items[pageContents.index].Type == PageItem.ItemType.Inventory)
                    {
                        preset.Pages[pageDirectory.index].Items[pageContents.index].name = (preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference != null) ? preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference.name : "None";
                    }
                    break;
                case 1:
                    preset.ExtraPages[pageDirectory.index].Items[pageContents.index].name = EditorGUILayout.TextField(preset.ExtraPages[pageDirectory.index].Items[pageContents.index].name, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
                    if (preset.ExtraPages[pageDirectory.index].Items[pageContents.index].name == "")
                    {
                        preset.ExtraPages[pageDirectory.index].Items[pageContents.index].name = "Slot " + (pageContents.index + 1);
                    }
                    else if (preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Type == PageItem.ItemType.Inventory)
                    {
                        preset.ExtraPages[pageDirectory.index].Items[pageContents.index].name = (preset.ExtraPages[pageDirectory.index].Items[pageContents.index].PageReference != null) ? preset.ExtraPages[pageDirectory.index].Items[pageContents.index].PageReference.name : "None";
                    }
                    break;
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

            switch (pageList)
            {
                case 0:
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Icon");
                    preset.Pages[pageDirectory.index].Items[pageContents.index].Icon = (Texture2D)EditorGUILayout.ObjectField(preset.Pages[pageDirectory.index].Items[pageContents.index].Icon, typeof(Texture2D), false);
                    EditorGUILayout.EndHorizontal();
                    preset.Pages[pageDirectory.index].Items[pageContents.index].Type = (PageItem.ItemType)EditorGUILayout.EnumPopup(new GUIContent("Type"), preset.Pages[pageDirectory.index].Items[pageContents.index].Type);
                    switch (preset.Pages[pageDirectory.index].Items[pageContents.index].Type)
                    {
                        case PageItem.ItemType.Toggle:
                            preset.Pages[pageDirectory.index].Items[pageContents.index].Clip = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Toggle"), preset.Pages[pageDirectory.index].Items[pageContents.index].Clip, typeof(AnimationClip), false);
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.PrefixLabel(new GUIContent("Sync"));
                            preset.Pages[pageDirectory.index].Items[pageContents.index].Sync = GUILayout.Toolbar(preset.Pages[pageDirectory.index].Items[pageContents.index].Sync, new string[] { "Off", "Manual", "Auto" });
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginVertical();
                            EditorGUILayout.EndVertical();
                            break;
                        case PageItem.ItemType.Inventory:
                            string[] names = new string[preset.ExtraPages.Count];
                            for (int i = 0; i < names.Length; i++)
                            {
                                names[i] = preset.ExtraPages[i].name;
                            }
                            if (preset.ExtraPages.Count > 0)
                            {
                                preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference = preset.ExtraPages[EditorGUILayout.Popup(new GUIContent("Inventory"), preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference != null ? preset.ExtraPages.IndexOf(preset.Pages[pageDirectory.index].Items[pageContents.index].PageReference) : 0, names)];
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
                    break;
                case 1:
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Icon");
                    preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Icon = (Texture2D)EditorGUILayout.ObjectField(preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Icon, typeof(Texture2D), false);
                    EditorGUILayout.EndHorizontal();
                    preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Type = (PageItem.ItemType)EditorGUILayout.EnumPopup(new GUIContent("Type"), preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Type);
                    switch (preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Type)
                    {
                        case PageItem.ItemType.Toggle:
                            preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Clip = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Toggle"), preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Clip, typeof(AnimationClip), false);
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.PrefixLabel(new GUIContent("Sync"));
                            preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Sync = GUILayout.Toolbar(preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Sync, new string[] { "Off", "Manual", "Auto" });
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginVertical();
                            EditorGUILayout.EndVertical();
                            break;
                        case PageItem.ItemType.Inventory:
                            string[] names = new string[preset.ExtraPages.Count - 1];
                            for (int i = 0; i < names.Length; i++)
                            {
                                if (i == pageDirectory.index + 1)
                                    continue;
                                names[i] = preset.ExtraPages[i].name;
                            }
                            preset.ExtraPages[pageDirectory.index].Items[pageContents.index].PageReference = preset.ExtraPages[EditorGUILayout.Popup(new GUIContent("Inventory"), preset.ExtraPages[pageDirectory.index].Items[pageContents.index].PageReference != null ? preset.ExtraPages.IndexOf(preset.ExtraPages[pageDirectory.index].Items[pageContents.index].PageReference) : 0, names)];
                            break;
                        case PageItem.ItemType.Submenu:
                            preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Submenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Submenu"), preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Submenu, typeof(VRCExpressionsMenu), false);
                            break;
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndVertical();
                    break;
            }

            EditorGUILayout.BeginVertical();
            if (pageContents != null)
                pageContents.DoLayoutList();
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox("Only Inventory pages can be modified.", MessageType.Info);
        }
        EditorGUILayout.Space();
        DrawLine();

        GUILayout.Label("Group Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical();
        if (((pageList == 1) ? preset.ExtraPages[pageDirectory.index] : preset.Pages[pageDirectory.index]).Type == Page.PageType.Submenu)
        {
            EditorGUILayout.HelpBox("Only Inventory pages can be modified.", MessageType.Info);
        }
        else if ((pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Type == PageItem.ItemType.Toggle : preset.Pages[pageDirectory.index].Items[pageContents.index].Type == PageItem.ItemType.Toggle)
        {
            groupContents.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group : preset.Pages[pageDirectory.index].Items[pageContents.index].Group;
            if (groupContents.list.Count == 0)
            {
                if (GUILayout.Button(new GUIContent("Create Group")))
                {
                    GroupItem item = ScriptableObject.CreateInstance<GroupItem>();
                    item.hideFlags = HideFlags.HideInHierarchy;
                    item.Item = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items[pageContents.index] : preset.Pages[pageDirectory.index].Items[pageContents.index];
                    item.Reaction = GroupItem.GroupType.Toggle;
                    item.name = ((pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items[pageContents.index].name : preset.Pages[pageDirectory.index].Items[pageContents.index].name) + ": Group Item " + (groupContents.list.Count + 1);
                    string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
                    AssetDatabase.AddObjectToAsset(item, _path);
                    switch (pageList)
                    {
                        case 0:
                            GroupItem[] newArray0 = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].Group.Length + 1];
                            preset.Pages[pageDirectory.index].Items[pageContents.index].Group.CopyTo(newArray0, 0);
                            newArray0[newArray0.GetUpperBound(0)] = item;
                            preset.Pages[pageDirectory.index].Items[pageContents.index].Group = newArray0;
                            groupContents.list = preset.Pages[pageDirectory.index].Items[pageContents.index].Group;
                            break;
                        case 1:
                            GroupItem[] newArray1 = new GroupItem[preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group.Length + 1];
                            preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group.CopyTo(newArray1, 0);
                            newArray1[newArray1.GetUpperBound(0)] = item;
                            preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group = newArray1;
                            groupContents.list = preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group;
                            break;
                    }
                    AssetDatabase.SaveAssets();
                }
            }
            else
            {
                groupContents.DoLayoutList();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Groups are only usable with Toggles.", MessageType.Info);
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
        Page item = (pageList == 1) ? preset.ExtraPages[index] : preset.Pages[index];

        EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width / 2, rect.height), item.name);
        EditorGUI.LabelField(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), (item.Type == Page.PageType.Inventory) ? "Inventory" : "Submenu");

        if (item.name == "")
        {
            item.name = "Page " + (index + 1);
        }
    }

    //Adds a page when the add button is pressed, if room is available
    private void AddDirectoryItem(ReorderableList list)
    {
        if (pageList == 1 || list.list.Count < 8)
        {
            Page page = ScriptableObject.CreateInstance<Page>();
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            page.hideFlags = HideFlags.HideInHierarchy;
            page.name = ((pageList == 1) ? "Extra " : "Page ") + (pageDirectory.list.Count + 1);
            AssetDatabase.AddObjectToAsset(page, _path);
            switch (pageList)
            {
                case 0:
                    preset.Pages.Add(page);
                    pageContents.list = preset.Pages[0].Items;
                    break;
                case 1:
                    preset.ExtraPages.Add(page);
                    pageContents.list = preset.ExtraPages[0].Items;
                    break;
            }
        }
        list.list = (pageList == 1) ? preset.ExtraPages : preset.Pages;
        list.index = list.list.Count - 1;        
        pageContents.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items : preset.Pages[pageDirectory.index].Items;
        AssetDatabase.SaveAssets();
    }

    //Removes the currently selected item from the list, if at least two are present
    private void RemoveDirectoryItem(ReorderableList list)
    {
        if (list.list.Count > 1)
        {
            switch (pageList)
            {
                case 0:
                    foreach (PageItem item in preset.Pages[list.index].Items)
                    {
                        AssetDatabase.RemoveObjectFromAsset(item);
                    }
                    AssetDatabase.RemoveObjectFromAsset(preset.Pages[list.index]);
                    preset.Pages.RemoveAt(list.index);
                    break;
                case 1:
                    foreach (PageItem item in preset.ExtraPages[list.index].Items)
                    {
                        AssetDatabase.RemoveObjectFromAsset(item);
                    }
                    AssetDatabase.RemoveObjectFromAsset(preset.ExtraPages[list.index]);
                    preset.ExtraPages.RemoveAt(list.index);
                    break;
            }            
        }          
        list.list = (pageList == 1) ? preset.ExtraPages : preset.Pages;
        if (list.index == list.list.Count)
            list.index -= 1;
        pageContents.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items : preset.Pages[pageDirectory.index].Items;
        AssetDatabase.SaveAssets();
    }

    //Page Contents

    //Draws the list header
    private void DrawPageHeader(Rect rect)
    {
        if (pageDirectory.index >= 0 && pageDirectory.index < pageDirectory.list.Count)
            GUI.Label(rect, (pageList == 1) ? preset.ExtraPages[pageDirectory.index].name : preset.Pages[pageDirectory.index].name);
    }

    //Draws each element
    private void DrawPageElement(Rect rect, int index, bool active, bool focused)
    {
        pageContents.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items : preset.Pages[pageDirectory.index].Items;
        if (index < pageContents.list.Count && index >= 0 && pageDirectory.index >= 0 && pageDirectory.index < pageDirectory.list.Count)
        {
            PageItem item = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items[index] : preset.Pages[pageDirectory.index].Items[index];

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
            switch (pageList)
            {
                case 0:
                    preset.Pages[pageDirectory.index].Items.Add(item);
                    break;
                case 1:
                    preset.ExtraPages[pageDirectory.index].Items.Add(item);
                    break;
            }
            
        }            
        list.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items : preset.Pages[pageDirectory.index].Items;
        list.index = list.list.Count - 1;
        AssetDatabase.SaveAssets();
    }

    //Removes the currently selected item from the list, if at least two are present
    private void RemovePageItem(ReorderableList list)
    {
        if (list.list.Count > 1)
        {           
            switch (pageList)
            {
                case 0:
                    AssetDatabase.RemoveObjectFromAsset(preset.Pages[pageDirectory.index].Items[list.index]);
                    preset.Pages[pageDirectory.index].Items.RemoveAt(list.index);
                    break;
                case 1:
                    AssetDatabase.RemoveObjectFromAsset(preset.ExtraPages[pageDirectory.index].Items[list.index]);
                    preset.ExtraPages[pageDirectory.index].Items.RemoveAt(list.index);
                    break;
            }           
        }           
        list.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items : preset.Pages[pageDirectory.index].Items;
        if (list.index == list.list.Count)
            list.index -= 1;
        AssetDatabase.SaveAssets();
    }

    //Group Contents

    //Draws the list header
    private void DrawGroupHeader(Rect rect)
    {
        GUI.Label(rect, (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items[pageContents.index].name + " Group" : preset.Pages[pageDirectory.index].Items[pageContents.index].name + " Group");
    }

    //Draws each element
    private void DrawGroupElement(Rect rect, int index, bool active, bool focused)
    {
        groupContents.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group : preset.Pages[pageDirectory.index].Items[pageContents.index].Group;
        if (index < groupContents.list.Count && index >= 0 && pageContents.index < pageContents.list.Count && pageContents.index >= 0 && pageDirectory.index >= 0 && pageDirectory.index < pageDirectory.list.Count)
        {
            GroupItem item = (pageList == 1) ? (GroupItem)preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group[index] : (GroupItem)preset.Pages[pageDirectory.index].Items[pageContents.index].Group[index];

            List<PageItem> toggles = new List<PageItem>();
            List<string> toggleNames = new List<string>();
            foreach (Page page in preset.Pages)
            {
                if (page.Type == Page.PageType.Inventory)
                {
                    foreach (PageItem pageItem in page.Items)
                    {
                        if (pageItem.Type == PageItem.ItemType.Toggle && (pageItem == item.Item || ((pageList == 1) && System.Array.IndexOf(preset.ExtraPages[pageDirectory.index].Items[pageContents.index].GetGroupItems(), pageItem) == -1) || ((pageList == 0) && System.Array.IndexOf(preset.Pages[pageDirectory.index].Items[pageContents.index].GetGroupItems(), pageItem) == -1)))
                        {
                            toggles.Add(pageItem);
                            toggleNames.Add(page.name + ": " + pageItem.name);
                        }
                    }
                }                
            }
            foreach (Page page in preset.ExtraPages)
            {
                if (page.Type == Page.PageType.Inventory)
                {
                    foreach (PageItem pageItem in page.Items)
                    {
                        if (pageItem.Type == PageItem.ItemType.Toggle && (pageItem == item.Item || ((pageList == 1) && System.Array.IndexOf(preset.ExtraPages[pageDirectory.index].Items[pageContents.index].GetGroupItems(), pageItem) == -1) || ((pageList == 0) && System.Array.IndexOf(preset.Pages[pageDirectory.index].Items[pageContents.index].GetGroupItems(), pageItem) == -1)))
                        {
                            toggles.Add(pageItem);
                            toggleNames.Add(page.name + ": " + pageItem.name);
                        }
                    }
                }                    
            }

            item.Item = toggles[EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width / 2, rect.height), (toggles.IndexOf(item.Item) != -1) ? toggles.IndexOf(item.Item) : 0, toggleNames.ToArray())];
            item.Reaction = (GroupItem.GroupType)EditorGUI.EnumPopup(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), item.Reaction);
        }
    }

    //Adds a slot when the add button is pressed, if room is available
    private void AddGroupItem(ReorderableList list)
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
        foreach (Page page in preset.ExtraPages)
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
        if (totalUsage == list.list.Count)
        {
            return;
        }

        GroupItem item = ScriptableObject.CreateInstance<GroupItem>();
        string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
        item.hideFlags = HideFlags.HideInHierarchy;
        item.name = ((pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items[pageContents.index].name : preset.Pages[pageDirectory.index].Items[pageContents.index].name) + ": Group Item " + (list.list.Count + 1);
        AssetDatabase.AddObjectToAsset(item, _path);
        switch (pageList)
        {
            case 0:
                GroupItem[] newArray0 = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].Group.Length + 1];
                preset.Pages[pageDirectory.index].Items[pageContents.index].Group.CopyTo(newArray0, 0);
                newArray0[newArray0.GetUpperBound(0)] = item;
                preset.Pages[pageDirectory.index].Items[pageContents.index].Group = newArray0;
                break;
            case 1:
                GroupItem[] newArray1 = new GroupItem[preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group.Length + 1];
                preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group.CopyTo(newArray1, 0);
                newArray1[newArray1.GetUpperBound(0)] = item;
                preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group = newArray1;
                break;
        }
        list.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group : preset.Pages[pageDirectory.index].Items[pageContents.index].Group;
        AssetDatabase.SaveAssets();
    }

    //Removes the currently selected item from the list, if at least two are present
    private void RemoveGroupItem(ReorderableList list)
    {
        if (list.list.Count > 0)
        {
            switch (pageList)
            {
                case 0:
                    AssetDatabase.RemoveObjectFromAsset(preset.Pages[pageDirectory.index].Items[pageContents.index].Group[list.index]);
                    preset.Pages[pageDirectory.index].Items[pageContents.index].Group[list.index] = null;
                    GroupItem[] newArray0 = new GroupItem[preset.Pages[pageDirectory.index].Items[pageContents.index].Group.Length - 1];
                    for (int i = 0; i < newArray0.Length; i++)
                    {
                        newArray0[i] = preset.Pages[pageDirectory.index].Items[pageContents.index].Group[i];
                    }
                    preset.Pages[pageDirectory.index].Items[pageContents.index].Group = newArray0;
                    break;
                case 1:
                    AssetDatabase.RemoveObjectFromAsset(preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group[list.index]);
                    preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group[list.index] = null;
                    GroupItem[] newArray1 = new GroupItem[preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group.Length - 1];
                    for (int i = 0; i < newArray1.Length; i++)
                    {
                        newArray1[i] = preset.Pages[pageDirectory.index].Items[pageContents.index].Group[i];
                    }
                    preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group = newArray1;
                    break;
            }
        }
        list.list = (pageList == 1) ? preset.ExtraPages[pageDirectory.index].Items[pageContents.index].Group : preset.Pages[pageDirectory.index].Items[pageContents.index].Group;
        if (list.index == list.list.Count)
            list.index -= 1;
        AssetDatabase.SaveAssets();
    }
}