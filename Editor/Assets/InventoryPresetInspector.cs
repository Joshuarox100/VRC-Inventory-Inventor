using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

[CustomEditor(typeof(InventoryPreset))]
public class InventoryPresetInspector : Editor 
{
    private InventoryPreset preset;

    public ReorderableList pageContents;
    public ReorderableList pageDirectory;

    public int currentPage = 0;

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

        pageContents = new ReorderableList(null, typeof(ListItem), true, true, true, true)
        {
            elementHeight = 18f
        };
        pageContents.drawHeaderCallback += DrawPageHeader;
        pageContents.drawElementCallback += DrawPageElement;
        pageContents.onAddCallback += AddPageItem;
        pageContents.onRemoveCallback += RemovePageItem;
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

        AssetDatabase.SaveAssets();
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        string _hasPath = AssetDatabase.GetAssetPath(preset.GetInstanceID());
        if (_hasPath == "")
        {
            return;
        }       
        serializedObject.Update();
        EditorGUILayout.BeginVertical();
        //List Inventory Settings
        GUILayout.Label("Inventory Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        DrawLine();

        GUILayout.Label("Page Settings", EditorStyles.boldLabel);
        //Make sure at least one page exists
        if (pageDirectory.list.Count == 0)
        {
            Page page = ScriptableObject.CreateInstance<Page>();
            page.name = "Page " + (preset.Pages.Count + 1);
            page.hideFlags = HideFlags.HideInHierarchy;
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            AssetDatabase.AddObjectToAsset(page, _path);
            preset.Pages.Add(page);
            pageContents.list = preset.Pages[0].Items;
            AssetDatabase.SaveAssets();
        }       
        else if (pageContents.list == null)
        {
            pageContents.list = ((Page)pageDirectory.list[0]).Items;
        }
        string[] pageNames = new string[preset.Pages.ToArray().Length];
        for (int i = 0; i < preset.Pages.ToArray().Length; i++)
        {
            pageNames[i] = (i + 1).ToString();
        }
        //Draw page renamer
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
        GUILayout.Label("Name:", GUILayout.ExpandWidth(false));
        if (currentPage >= preset.Pages.ToArray().Length)
        {
            currentPage = preset.Pages.ToArray().Length - 1;
            pageDirectory.index = currentPage;
        }
        else if (currentPage < 0)
        {
            currentPage = 0;
            pageDirectory.index = currentPage;
        }
        preset.Pages[currentPage].name = EditorGUILayout.TextField(preset.Pages[currentPage].name, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
        if (preset.Pages[currentPage].name == "")
        {
            preset.Pages[currentPage].name = "Page " + (currentPage + 1);
        }
        //Draw page navigator
        if (GUILayout.Button('\u25C0'.ToString(), GUILayout.Width(30f)))
        {
            if (currentPage != 0)
            {
                currentPage--;
                pageDirectory.index = currentPage;
                pageContents.list = preset.Pages[currentPage].Items;
                GUI.FocusControl(null);
            }
        }
        EditorGUI.BeginChangeCheck();
        currentPage = EditorGUILayout.Popup(currentPage, pageNames, new GUIStyle(GUI.skin.GetStyle("Dropdown")), GUILayout.Width(30f));
        if (EditorGUI.EndChangeCheck())
        {
            pageDirectory.index = currentPage;
            pageContents.list = preset.Pages[currentPage].Items;
            GUI.FocusControl(null);
        }
        if (GUILayout.Button('\u25B6'.ToString(), GUILayout.Width(30f)))
        {
            if (currentPage != preset.Pages.ToArray().Length - 1)
            {
                currentPage++;
                pageDirectory.index = currentPage;
                pageContents.list = preset.Pages[currentPage].Items;
                GUI.FocusControl(null);
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Icon");
        preset.Pages[currentPage].Icon = (Texture2D)EditorGUILayout.ObjectField(preset.Pages[currentPage].Icon, typeof(Texture2D), false);
        EditorGUILayout.EndHorizontal();
        preset.Pages[currentPage].Type = (Page.PageType)EditorGUILayout.EnumPopup(new GUIContent("Type"), preset.Pages[currentPage].Type);
        if (preset.Pages[currentPage].Type == Page.PageType.Submenu)
            preset.Pages[currentPage].Submenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Submenu"), preset.Pages[currentPage].Submenu, typeof(VRCExpressionsMenu), false);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
        EditorGUILayout.BeginVertical(GUILayout.Height(185f));
        if (pageDirectory != null)
            pageDirectory.DoLayoutList();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        DrawLine();

        int temp = currentPage;
        currentPage = (pageDirectory.index >= 0 && pageDirectory.index < preset.Pages.Count) ? pageDirectory.index : currentPage;
        if (temp != currentPage) 
            pageContents.list = preset.Pages[pageDirectory.index].Items;

        //Draw currently selected page contents
        GUILayout.Label("Item Settings", EditorStyles.boldLabel);

        if (preset.Pages[currentPage].Items.Count < 1)
        {            
            ListItem item = ScriptableObject.CreateInstance<ListItem>();
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            item.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(item, _path);            
            item.name = "Slot 1";
            preset.Pages[currentPage].Items.Add(item);
        }

        string[] itemNames = new string[preset.Pages[currentPage].Items.Count];
        for (int i = 0; i < itemNames.Length; i++)
        {
            itemNames[i] = (i + 1).ToString();
        }
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        EditorGUILayout.BeginHorizontal(new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
        GUILayout.Label("Name:", GUILayout.ExpandWidth(false));
        if (pageContents.index >= preset.Pages[currentPage].Items.Count)
        {
            pageContents.index = preset.Pages[currentPage].Items.Count - 1;
        }
        else if (pageContents.index < 0)
        {
            pageContents.index = 0;
        }
        preset.Pages[currentPage].Items[pageContents.index].name = EditorGUILayout.TextField(preset.Pages[currentPage].Items[pageContents.index].name, new GUIStyle(GUI.skin.GetStyle("Box")) { font = EditorStyles.toolbarTextField.font, alignment = TextAnchor.MiddleLeft, normal = EditorStyles.toolbarTextField.normal }, GUILayout.ExpandWidth(true));
        if (preset.Pages[currentPage].Items[pageContents.index].name == "")
        {
            preset.Pages[currentPage].Items[pageContents.index].name = "Slot " + (pageContents.index + 1);
        }
        //Draw page navigator
        if (GUILayout.Button('\u25C0'.ToString(), GUILayout.Width(30f)))
        {
            if (pageContents.index != 0)
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
            if (pageContents.index != preset.Pages[currentPage].Items.Count - 1)
            {
                pageContents.index++;
                GUI.FocusControl(null);
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Icon");
        preset.Pages[currentPage].Items[pageContents.index].Icon = (Texture2D)EditorGUILayout.ObjectField(preset.Pages[currentPage].Items[pageContents.index].Icon, typeof(Texture2D), false);
        EditorGUILayout.EndHorizontal();
        preset.Pages[currentPage].Items[pageContents.index].Type = (ListItem.ItemType)EditorGUILayout.EnumPopup(new GUIContent("Type"), preset.Pages[currentPage].Items[pageContents.index].Type);
        switch (preset.Pages[currentPage].Items[pageContents.index].Type)
        {
            case ListItem.ItemType.Toggle:
                preset.Pages[currentPage].Items[pageContents.index].Clip = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Toggle"), preset.Pages[currentPage].Items[pageContents.index].Clip, typeof(AnimationClip), false);
                break;
            case ListItem.ItemType.Inventory:
                string[] names = new string[preset.Pages.Count];
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = preset.Pages[i].name;
                }
                preset.Pages[currentPage].Items[pageContents.index].PageReference = preset.Pages[EditorGUILayout.Popup(new GUIContent("Inventory"), preset.Pages[currentPage].Items[pageContents.index].PageReference != null ? preset.Pages.IndexOf(preset.Pages[currentPage].Items[pageContents.index].PageReference) : 0, names)];
                break;
            case ListItem.ItemType.Submenu:
                preset.Pages[currentPage].Items[pageContents.index].Submenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Submenu"), preset.Pages[currentPage].Items[pageContents.index].Submenu, typeof(VRCExpressionsMenu), false);
                break;
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
        EditorGUILayout.BeginVertical(GUILayout.Height(185f));
        if (pageContents != null)
            pageContents.DoLayoutList();
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
        if (preset.Pages.ToArray().Length < 8)
        {
            Page page = ScriptableObject.CreateInstance<Page>();
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            page.hideFlags = HideFlags.HideInHierarchy;
            page.name = "Page " + (preset.Pages.Count + 1);
            AssetDatabase.AddObjectToAsset(page, _path);                        
            preset.Pages.Add(page);
        }
        list.list = preset.Pages;
        list.index = list.list.Count - 1;        
        currentPage = list.index;
        pageContents.list = preset.Pages[currentPage].Items;
        AssetDatabase.SaveAssets();
    }

    //Removes the currently selected item from the list, if at least two are present
    private void RemoveDirectoryItem(ReorderableList list)
    {
        if (preset.Pages.ToArray().Length > 1)
        {
            foreach (ListItem item in preset.Pages[list.index].Items)
            {
                AssetDatabase.RemoveObjectFromAsset(item);
            }
            AssetDatabase.RemoveObjectFromAsset(preset.Pages[list.index]);
            preset.Pages.RemoveAt(list.index);
        }          
        list.list = preset.Pages;
        if (list.index == list.list.Count)
            list.index -= 1;
        currentPage = list.index;
        pageContents.list = preset.Pages[currentPage].Items;
        AssetDatabase.SaveAssets();
    }

    //Page Contents

    //Draws the list header
    private void DrawPageHeader(Rect rect)
    {
        if (currentPage >= 0 && currentPage < preset.Pages.Count)
            GUI.Label(rect, preset.Pages[currentPage].name);
    }

    //Draws each element
    private void DrawPageElement(Rect rect, int index, bool active, bool focused)
    {       
        if (index < preset.Pages[currentPage].Items.Count && currentPage >= 0 && currentPage < preset.Pages.Count)
        {
            ListItem item = preset.Pages[currentPage].Items[index];

            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width / 2, rect.height), item.name);
            EditorGUI.LabelField(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), (item.Type == ListItem.ItemType.Toggle) ? "Toggle" : (item.Type == ListItem.ItemType.Inventory) ? "Inventory" : "Submenu");
        }            
    }

    //Adds a slot when the add button is pressed, if room is available
    private void AddPageItem(ReorderableList list)
    {
        if (preset.Pages[currentPage].Items.ToArray().Length < 8)
        {
            ListItem item = ScriptableObject.CreateInstance<ListItem>();
            string _path = AssetDatabase.GetAssetPath(preset.GetInstanceID());
            item.hideFlags = HideFlags.HideInHierarchy;
            item.name = "Slot " + (preset.Pages[currentPage].Items.Count + 1);
            AssetDatabase.AddObjectToAsset(item, _path);                       
            preset.Pages[currentPage].Items.Add(item);
        }            
        list.list = preset.Pages[currentPage].Items;
        list.index = list.list.Count - 1;
        AssetDatabase.SaveAssets();
    }

    //Removes the currently selected item from the list, if at least two are present
    private void RemovePageItem(ReorderableList list)
    {
        if (preset.Pages[currentPage].Items.ToArray().Length > 1)
        {           
            AssetDatabase.RemoveObjectFromAsset(preset.Pages[currentPage].Items[list.index]);
            preset.Pages[currentPage].Items.RemoveAt(list.index);
        }           
        list.list = preset.Pages[currentPage].Items;
        if (list.index == list.list.Count)
            list.index -= 1;
        AssetDatabase.SaveAssets();
    }
}