using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
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

    [MenuItem("Tools/Avatars 3.0/Inventory Inventor/Manage Inventory")]
    public static void ManageInventory()
    {
        InventoryInventor window = (InventoryInventor)GetWindow(typeof(InventoryInventor), false, "Inventory Inventor");
        window.minSize = new Vector2(375f, 600f);
        window.wantsMouseMove = true;
        window.Show();
    }
    [MenuItem("Tools/Avatars 3.0/Inventory Inventor/Check For Updates")]
    public static void CheckForUpdates()
    {
        InventoryInventorManager.CheckForUpdates();
    }

    //Relocate file directory path
    private void OnFocus()
    {
        manager.UpdatePaths();
    }

    //Draw GUI
    private void OnGUI()
    {
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
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        manager.menu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Expressions Menu", "The Expressions Menu you want the inventory controls added to. Leave this empty if you don't want any menus to be affected.\n(Controls will be added as a submenu.)"), manager.menu, typeof(VRCExpressionsMenu), true);
        if (manager.avatar != null && manager.controller == null)
        {
            manager.controller = (manager.avatar.baseAnimationLayers[4].animatorController != null) ? (AnimatorController)manager.avatar.baseAnimationLayers[4].animatorController : null;
        }
        manager.controller = (AnimatorController)EditorGUILayout.ObjectField(new GUIContent("Animator", "The Animator Controller to modify.\n(If left empty, a new Animator will be created and used.)"), manager.controller, typeof(AnimatorController), false);
        manager.refreshRate = EditorGUILayout.FloatField(new GUIContent("Refresh Rate", "How long each synced toggle is given to synchronize with late joiners (seconds per item)."), manager.refreshRate);
        if (manager.refreshRate < 0)
            manager.refreshRate = 0.05f;
        GUILayout.EndVertical();
        EditorGUILayout.Space();
        DrawLine();

        //List Inventory Settings
        GUILayout.Label("Inventory Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
        manager.preset = (InventoryPreset)EditorGUILayout.ObjectField(new GUIContent("Preset"), manager.preset, typeof(InventoryPreset), false);
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
        if (GUILayout.Button(new GUIContent("<i>" + displayPath + "</i>", manager.outputPath.Replace('\\', '/')), new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, hover = GUI.skin.GetStyle("Button").active }, GUILayout.MinWidth(210)))
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
        GUILayout.Box("Make inventories fast with Inventory Inventor! With it, you can create inventories with up to 64 synced toggles, all contained within a single Expression Parameter!", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, normal = new GUIStyleState() { background = null } }, GUILayout.Width(350f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();
        DrawLine();

        //Display special thanks
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<b><size=15>Special Thanks</size></b>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true }, GUILayout.Width(200f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<i><size=12>zachgregoire & ValliereMagic</size></i>\nFor helping with development, giving advice, and improving my coding knowledge overall.", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, normal = new GUIStyleState() { background = null } }, GUILayout.Width(350f));
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
            Application.OpenURL("https://github.com/Joshuarox100/VRC-Inventory-Inventor");
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
            var nextIndex = EditorGUILayout.Popup(new GUIContent("Active Avatar", "The Avatar you want to create an inventory for."), currentIndex, names);
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
}
