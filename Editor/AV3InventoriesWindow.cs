using Boo.Lang;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

public class AV3InventoriesWindow : EditorWindow
{
    private readonly AV3InventoriesManager manager = new AV3InventoriesManager();

    private int windowTab;
    private bool focused;
    private readonly List<bool> pages = new List<bool>();
    private int currentPage = 0;

    [MenuItem("Window/AV3 Tools/Inventory Inventor/Manage Inventory")]
    public static void ManageInventory()
    {
        AV3InventoriesWindow window = (AV3InventoriesWindow)GetWindow(typeof(AV3InventoriesWindow), false, "Inventory Inventor");
        window.minSize = new Vector2(375f, 525f);
        window.wantsMouseMove = true;
        window.Show();
    }
    [MenuItem("Window/AV3 Tools/Inventory Inventor/Check For Updates")]
    public static void CheckForUpdates()
    {
        AV3InventoriesManager.CheckForUpdates();
    }

    private void OnFocus()
    {
        manager.UpdatePaths();
    }

    private void OnGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginArea(new Rect((position.width / 2f) - (375f / 2f), 5f, 375f, 800f));
        windowTab = GUILayout.Toolbar(windowTab, new string[] { "Create", "About" });
        DrawLine(false);
        switch (windowTab)
        {
            case 0:
                if (EditorApplication.isPlaying)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox("Inventories can only be created outside of Play Mode.", MessageType.Info);
                }
                else
                {
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
                GUILayout.BeginVertical();
                DrawAboutWindow();
                GUILayout.EndVertical();
                GUILayout.EndArea();
                GUILayout.FlexibleSpace();
                EditorGUILayout.Space();
                GUILayout.EndVertical();
                break;
        }
    }

    private void DrawSetupWindow()
    {
        EditorGUILayout.BeginVertical();
        GUILayout.BeginVertical(GUILayout.Height(54f));
        EditorGUI.BeginChangeCheck();
        GUILayout.FlexibleSpace();
        SelectAvatarDescriptor();
        if (manager.avatar == null)
        {
            EditorGUILayout.HelpBox("No Avatars found in the current Scene!", MessageType.Warning);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        DrawLine();
        GUILayout.Label("Optional Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        manager.menu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Expressions Menu", "(Optional) The Expressions Menu you want the inventory controls added to. Leave this empty if you don't want any menus to be affected.\n(Controls will be added as a submenu.)"), manager.menu, typeof(VRCExpressionsMenu), true);
        GUILayout.EndVertical();
        EditorGUILayout.Space();
        DrawLine();
        GUILayout.Label("Inventory Settings", EditorStyles.boldLabel);
        string[] pageNames = new string[pages.ToArray().Length];
        for(int i = 0; i < pageNames.Length; i++)
        {
            pageNames[i] = "Page " + (i + 1);
        }     
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")), GUILayout.MaxHeight(215f));
        currentPage = EditorGUILayout.Popup(currentPage, pageNames);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(new GUIContent("<b>Menu Name</b>", "Name used for the Expressions Menu."), new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleCenter, normal = new GUIStyleState() { background = null }, richText = true }, GUILayout.Width(355f / 2));
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(new GUIContent("<b>Animation</b>", "Animation to be toggled by the menu."), new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleCenter, normal = new GUIStyleState() { background = null }, richText = true }, GUILayout.Width(355f / 2));
        EditorGUILayout.EndHorizontal();
        int pageTotal = (manager.toggleables.ToArray().Length % 8 == 0) ? manager.toggleables.ToArray().Length / 8 : (manager.toggleables.ToArray().Length / 8) + 1;
        while (pages.ToArray().Length > pageTotal)
        {
            pages.RemoveAt(pages.ToArray().Length - 1);
            currentPage = pages.ToArray().Length - 1;
        }
        while (pages.ToArray().Length < pageTotal)
        {
            pages.Add(true);
            currentPage = pages.ToArray().Length - 1;
        }
        for (int i = 8 * currentPage; i < (8 * currentPage) + 8; i++)
        {
            if (i < manager.toggleables.ToArray().Length)
            {
                EditorGUILayout.BeginHorizontal();
                manager.aliases[i] = EditorGUILayout.TextField(manager.aliases[i]);
                manager.toggleables[i] = (AnimationClip)EditorGUILayout.ObjectField(manager.toggleables[i], typeof(AnimationClip), false);
                EditorGUILayout.EndHorizontal();
            }
            else
                break;
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginHorizontal();
        if (manager.toggleables.ToArray().Length < 64)
        {
            if (manager.toggleables.ToArray().Length == 1)
            {
                if (GUILayout.Button("Add", GUILayout.Width(355f)))
                {
                    manager.toggleables.Add(null);
                    manager.aliases.Add("Slot " + manager.toggleables.ToArray().Length);
                }
            }
            else
            {
                if (GUILayout.Button("Add", GUILayout.Width(355f / 2)))
                {
                    manager.toggleables.Add(null);
                    manager.aliases.Add("Slot " + manager.toggleables.ToArray().Length);
                }
            }
        }
        GUILayout.FlexibleSpace();
        if (manager.toggleables.ToArray().Length > 1)
        {
            if (manager.toggleables.ToArray().Length == 64)
            {
                if (GUILayout.Button("Remove", GUILayout.Width(355f)))
                {
                    if (manager.toggleables.ToArray().Length > 1)
                    {
                        manager.toggleables.Remove(manager.toggleables[manager.toggleables.ToArray().Length - 1]);
                        manager.aliases.Remove(manager.aliases[manager.aliases.ToArray().Length - 1]);
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Remove", GUILayout.Width(355f / 2)))
                {
                    if (manager.toggleables.ToArray().Length > 1)
                    {
                        manager.toggleables.Remove(manager.toggleables[manager.toggleables.ToArray().Length - 1]);
                        manager.aliases.Remove(manager.aliases[manager.aliases.ToArray().Length - 1]);
                    }
                }
            }
        }        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        DrawLine();
        GUILayout.Label("Output Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")), GUILayout.Height(50f));
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(new GUIContent("Destination", "The folder where generated files will be saved to."), new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
        GUILayout.FlexibleSpace();
        string displayPath = manager.outputPath.Replace('\\', '/');
        displayPath = ((new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, hover = GUI.skin.GetStyle("Button").active }).CalcSize(new GUIContent("<i>" + displayPath + "</i>")).x > 210) ? "..." + displayPath.Substring((displayPath.IndexOf('/', displayPath.Length - 29) != -1) ? displayPath.IndexOf('/', displayPath.Length - 29) : displayPath.Length - 29) : displayPath;
        while ((new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, hover = GUI.skin.GetStyle("Button").active }).CalcSize(new GUIContent("<i>" + displayPath + "</i>")).x > 210)
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
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Overwrite All", "Automatically overwrite existing files if needed."), GUILayout.Width(145));
        manager.autoOverwrite = Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(manager.autoOverwrite), new string[] { "No", "Yes" }));
        GUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        DrawLine();
        if (GUILayout.Button("Create"))
        {
            manager.CreateInventory();
            EditorUtility.ClearProgressBar();
        }
        EditorGUILayout.EndVertical();

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

    private void DrawAboutWindow()
    {
        string version = (AssetDatabase.FindAssets("VERSION", new string[] { manager.relativePath }).Length > 0) ? " " + File.ReadAllText(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("VERSION", new string[] { manager.relativePath })[0])) : "";
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
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<b><size=15>Troubleshooting</size></b>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true }, GUILayout.Width(200f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("If you're having issues or want to contact me, you can find more information at the Github page linked below!", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, normal = new GUIStyleState() { background = null } }, GUILayout.Width(350f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();
        DrawLine(false);
        GUILayout.Label("Github Repository", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.UpperCenter });
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
