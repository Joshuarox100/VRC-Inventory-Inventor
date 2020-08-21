using Boo.Lang;
using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

public class InventoryInventor : EditorWindow
{
    //Model Object
    private readonly InventoryInventorManager manager = new InventoryInventorManager();

    //Window Size
    private Rect windowSize = new Rect(0, 0, 375f, 800f);

    //Various Trackers
    private int windowTab;
    private bool focused;
    private ReorderableList itemList;
    private readonly List<Page> pages = new List<Page>();
    private int currentPage = 0;

    //Class used for representing an Inventory Page
    //string name           = name of the page
    //List<ListItem> items  = list of items held in the page
    private class Page
    {
        public string name;
        public readonly List<ListItem> items;

        //Constructor
        public Page(string name, ref ReorderableList itemList)
        {
            this.name = name;
            items = new List<ListItem>() { new ListItem("Slot 1") };
            itemList.list = items;
        }

        //Returns array of item names
        public string[] GetNames()
        {
            List<string> names = new List<string>();
            foreach (ListItem item in items)
            {
                names.Add(item.name);
            }
            return names.ToArray();
        }

        //Returns array of item clips
        public AnimationClip[] GetClips()
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            foreach (ListItem item in items)
            {
                clips.Add(item.clip);
            }
            return clips.ToArray();
        }
    }

    //Class used for storing page items
    //string name   = name of the item
    //AnimationClip = animation to be toggled
    [Serializable]
    private class ListItem
    {
        public string name;
        public AnimationClip clip;

        //Default Constructor
        public ListItem()
        {
            name = "Slot 1";
            clip = null;
        }

        //Additional Constructor
        public ListItem(string name)
        {
            this.name = name;
            clip = null;
        }
    }

    [MenuItem("Window/AV3 Tools/Inventory Inventor/Manage Inventory")]
    public static void ManageInventory()
    {
        InventoryInventor window = (InventoryInventor)GetWindow(typeof(InventoryInventor), false, "Inventory Inventor");
        window.minSize = new Vector2(375f, 585f);
        window.wantsMouseMove = true;
        window.Show();
    }
    [MenuItem("Window/AV3 Tools/Inventory Inventor/Check For Updates")]
    public static void CheckForUpdates()
    {
        InventoryInventorManager.CheckForUpdates();
    }

    //Remove listeners when window is closed to prevent memory leaks
    private void OnDestroy()
    {
        if (itemList != null)
        {
            itemList.drawHeaderCallback -= DrawHeader;
            itemList.drawElementCallback -= DrawElement;
            itemList.onAddCallback -= AddItem;
            itemList.onRemoveCallback -= RemoveItem;
        }       
    }

    //Relocate file directory path
    private void OnFocus()
    {
        manager.UpdatePaths();
    }

    //Assign listeners to itemList
    private void AssignListeners()
    {
        itemList.drawHeaderCallback += DrawHeader;
        itemList.drawElementCallback += DrawElement;
        itemList.onAddCallback += AddItem;
        itemList.onRemoveCallback += RemoveItem;
    }

    //Draw GUI
    private void OnGUI()
    {
        //Make sure itemList isn't null and has listeners assigned
        if (itemList == null)
        {
            itemList = new ReorderableList(null, typeof(ListItem), true, true, true, true);
            AssignListeners();
        }

        //Define main window area
        GUILayout.BeginVertical();
        windowSize.x = (position.width / 2f) - (375f / 2f);
        windowSize.y = 5f;
        GUILayout.BeginArea(windowSize);

        //Create toolbar
        windowTab = GUILayout.Toolbar(windowTab, new string[] { "Create", "About" });
        DrawLine(false);

        //Respond to current toolbar selection
        switch (windowTab)
        {
            case 0:
                if (EditorApplication.isPlaying)
                {
                    //Stop usage within play mode
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox("Inventories can only be created outside of Play Mode.", MessageType.Info);
                }
                else
                {
                    //Draw setup window
                    GUILayout.BeginVertical();
                    DrawSetupWindow();
                    GUILayout.EndVertical();
                    GUILayout.EndArea();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.Space();
                    GUILayout.EndVertical();
                }
                break;
            case 1:
                //Draw about window
                GUILayout.BeginVertical();
                DrawAboutWindow();
                GUILayout.EndVertical();
                GUILayout.EndArea();
                GUILayout.FlexibleSpace();
                EditorGUILayout.Space();
                GUILayout.EndVertical();
                break;
        }
        if (GUI.Button(windowSize, "", GUIStyle.none))
        {
            //Unfocus item in window when empty space is clicked
            GUI.FocusControl(null);
            itemList.index = 8;
        }
    }

    //Draws the setup GUI
    private void DrawSetupWindow()
    {
        EditorGUILayout.BeginVertical();
        
        //Check for avatars in the scene
        GUILayout.BeginVertical(GUILayout.Height(54f));
        GUILayout.FlexibleSpace();
        SelectAvatarDescriptor();
        if (manager.avatar == null)
        {
            EditorGUILayout.HelpBox("No Avatars found in the current Scene!", MessageType.Warning);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        DrawLine();

        //List optional settings
        GUILayout.Label("Optional Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")), GUILayout.Height(65f));
        manager.menu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Expressions Menu", "(Optional) The Expressions Menu you want the inventory controls added to. Leave this empty if you don't want any menus to be affected.\n(Controls will be added as a submenu.)"), manager.menu, typeof(VRCExpressionsMenu), true);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(new GUIContent("Auto Sync", "Off: Items will not auto-sync with late joiners.\nOn: Items will auto-sync with late joiners."), new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } }, GUILayout.ExpandWidth(false));
        manager.syncMode = GUILayout.Toolbar(manager.syncMode, new string[] { "Off", "On" }, GUILayout.Width(210f));
        EditorGUILayout.EndHorizontal();
        if (manager.syncMode == 1)
            manager.refreshRate = EditorGUILayout.FloatField(new GUIContent("Refresh Rate", "How long each toggle is given to synchronize with late joiners (seconds per item)."), manager.refreshRate);
        if (manager.refreshRate < 0)
            manager.refreshRate = 0.05f;
        GUILayout.EndVertical();
        EditorGUILayout.Space();
        DrawLine();

        //List Inventory Settings
        GUILayout.Label("Inventory Settings", EditorStyles.boldLabel);
        //Make sure at least one page exists
        if (pages.ToArray().Length == 0)
        {
            pages.Add(new Page("Page 1", ref itemList));
        }
        string[] pageNames = new string[pages.ToArray().Length];
        for(int i = 0; i < pages.ToArray().Length; i++)
        {
            pageNames[i] = (i + 1).ToString();
        }     
        //Draw page renamer
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Name:", GUILayout.ExpandWidth(false));
        if (currentPage >= pages.ToArray().Length)
        {
            currentPage = pages.ToArray().Length - 1;
        }
        else if (currentPage < 0)
        {
            currentPage = 0;
        }
        pages[currentPage].name = EditorGUILayout.TextField(pages[currentPage].name);
        //Draw page navigator
        if (GUILayout.Button('\u25C0'.ToString()))
        {
            if (currentPage != 0)
            {
                currentPage--;
                itemList.list = pages[currentPage].items;
            }
        }
        EditorGUI.BeginChangeCheck();
        currentPage = EditorGUILayout.Popup(currentPage, pageNames, new GUIStyle(GUI.skin.GetStyle("Dropdown")), GUILayout.Width(30f));
        if (EditorGUI.EndChangeCheck())
        {
            itemList.list = pages[currentPage].items;
        }
        if (GUILayout.Button('\u25B6'.ToString()))
        {
            if (currentPage != pages.ToArray().Length - 1)
            {              
                currentPage++;
                itemList.list = pages[currentPage].items;
            }
        }       
        EditorGUILayout.EndHorizontal();      
        //Draw page creator/remover
        EditorGUILayout.BeginHorizontal();
        if (pages.ToArray().Length < 8)
        {
            if (pages.ToArray().Length == 1)
            {
                if (GUILayout.Button("Add Page", GUILayout.Width(356f)))
                {
                    pages.Add(new Page("Page " + (pages.ToArray().Length + 1), ref itemList));
                    currentPage = pages.ToArray().Length - 1;
                    itemList.list = pages[currentPage].items;
                }               
            }
            else if (GUILayout.Button("Add Page", GUILayout.Width(356f / 2)))
            {              
                pages.Add(new Page("Page " + (pages.ToArray().Length + 1), ref itemList));
                currentPage = pages.ToArray().Length - 1;
                itemList.list = pages[currentPage].items;
            }
        }
        if (pages.ToArray().Length > 1)
        {
            if (pages.ToArray().Length == 8)
            {
                if (GUILayout.Button("Remove Page", GUILayout.Width(356f)))
                {
                    pages.RemoveAt(currentPage);
                    currentPage--;
                    itemList.list = pages[currentPage].items;
                }
            }
            else if (GUILayout.Button("Remove Page", GUILayout.Width(356f / 2)))
            {
                pages.RemoveAt(currentPage);
                currentPage--;
                itemList.list = pages[currentPage].items;
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        DrawLine();
        //Draw currently selected page contents
        EditorGUILayout.BeginVertical(GUILayout.Height(210f));
        if (itemList != null)
            itemList.DoLayoutList();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        DrawLine();

        //List Output Settings
        GUILayout.Label("Output Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")), GUILayout.Height(50f));
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(new GUIContent("Destination", "The folder where generated files will be saved to."), new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
        GUILayout.FlexibleSpace();      
        //Format file path to fit in a button
        string displayPath = manager.outputPath.Replace('\\', '/');
        displayPath = (new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, hover = GUI.skin.GetStyle("Button").active }.CalcSize(new GUIContent("<i>" + displayPath + "</i>")).x > 210) ? "..." + displayPath.Substring((displayPath.IndexOf('/', displayPath.Length - 29) != -1) ? displayPath.IndexOf('/', displayPath.Length - 29) : displayPath.Length - 29) : displayPath;
        while (new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, hover = GUI.skin.GetStyle("Button").active }.CalcSize(new GUIContent("<i>" + displayPath + "</i>")).x > 210)
        {
            displayPath = "..." + displayPath.Substring(4);
        }
        if (GUILayout.Button("<i>" + displayPath + "</i>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, hover = GUI.skin.GetStyle("Button").active }, GUILayout.MinWidth(210)))
        {
            string absPath = EditorUtility.OpenFolderPanel("Destination Folder", "", "");
            if (absPath.StartsWith(Application.dataPath))
            {
                manager.outputPath = "Assets" + absPath.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();
        //Auto-overwrite toggle
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Overwrite All", "Automatically overwrite existing files if needed."), GUILayout.Width(145));
        manager.autoOverwrite = Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(manager.autoOverwrite), new string[] { "No", "Yes" }));
        GUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        DrawLine();

        //Draw create button
        if (GUILayout.Button("Create"))
        {
            //Convert GUI data into data for the model to use
            List<AnimationClip> allClips = new List<AnimationClip>();
            List<string> allNames = new List<string>();
            List<int> allPageSizes = new List<int>();
            List<string> allPageNames = new List<string>();
            foreach (Page page in pages)
            {
                allClips.AddRange(page.GetClips());
                allNames.AddRange(page.GetNames());
                allPageSizes.Add(page.GetClips().Length);
                allPageNames.Add(page.name);
            }
            manager.toggleables = allClips.ToArray();
            manager.aliases = allNames.ToArray();
            manager.pageLength = allPageSizes.ToArray();
            manager.pageNames = allPageNames.ToArray();
            manager.CreateInventory();
            EditorUtility.ClearProgressBar();
        }
        EditorGUILayout.EndVertical();

        //Repaint when hovering over window (needed for directory button)
        if (mouseOverWindow != null && mouseOverWindow == this)
        {
            Repaint();
            focused = true;
        }
        else if (focused && mouseOverWindow == null)
        {
            Repaint();
            focused = false;
        }
    }

    //Draw about window
    private void DrawAboutWindow()
    {
        //Load version number
        string version = (AssetDatabase.FindAssets("VERSION", new string[] { manager.relativePath }).Length > 0) ? " " + File.ReadAllText(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("VERSION", new string[] { manager.relativePath })[0])) : "";

        //Display top header
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<b><size=18>Inventory Inventor" + version + "</size></b>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true }, GUILayout.Width(300f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<size=13>Author: Joshuarox100</size>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, normal = new GUIStyleState() { background = null } });
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();
        DrawLine();

        //Display summary
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<b><size=15>Summary</size></b>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true }, GUILayout.Width(200f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("Make upgrading to 3.0 a breeze with AV3 Overrides!", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, normal = new GUIStyleState() { background = null } }, GUILayout.Width(350f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();
        DrawLine();

        //Display troubleshooting
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<b><size=15>Troubleshooting</size></b>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true }, GUILayout.Width(200f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("If you're having issues or want to contact me, you can find more information at the GitHub page linked below!", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, normal = new GUIStyleState() { background = null } }, GUILayout.Width(350f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();
        DrawLine(false);

        //Display GitHub link
        GUILayout.Label("GitHub Repository", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.UpperCenter });
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Open in Browser", GUILayout.Width(250)))
        {
            Application.OpenURL("https://github.com/Joshuarox100/VRC-AV3-Inventories");
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
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
            var currentIndex = Array.IndexOf(descriptors, manager.avatar);
            var nextIndex = EditorGUILayout.Popup(new GUIContent("Active Avatar", "The Avatar you want to setup scaling for."), currentIndex, names);
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
        if (desc == manager.avatar)
            return;

        manager.avatar = desc;
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
    
    //Draws the list header
    private void DrawHeader(Rect rect)
    {
        GUI.Label(rect, pages[currentPage].name);
    }
    
    //Draws each element
    private void DrawElement(Rect rect, int index, bool active, bool focused)
    {
        ListItem item = pages[currentPage].items[index];

        item.name = EditorGUI.TextField(new Rect(rect.x, rect.y, rect.width / 2, rect.height), item.name);
        item.clip = (AnimationClip)EditorGUI.ObjectField(new Rect(rect.x + (rect.width / 2), rect.y, rect.width / 2, rect.height), item.clip, typeof(AnimationClip), false);
    }

    //Adds a slot when the add button is pressed, if room is available
    private void AddItem(ReorderableList list)
    {
        if (pages[currentPage].items.ToArray().Length < 8)
            pages[currentPage].items.Add(new ListItem("Slot " + (pages[currentPage].GetClips().Length + 1)));
        list.list = pages[currentPage].items;
        list.index = list.list.Count - 1;
    }

    //Removes the currently selected item from the list, if at least two are present
    private void RemoveItem(ReorderableList list)
    {
        if (pages[currentPage].items.ToArray().Length > 1)
            pages[currentPage].items.RemoveAt(list.index);
        list.list = pages[currentPage].items;
        if (list.index == list.list.Count)
            list.index -= 1;
    }
}
