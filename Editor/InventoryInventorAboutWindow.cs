using UnityEditor;
using UnityEngine;

public class InventoryInventorAboutWindow : EditorWindow
{
    // Window Size.
    private Rect windowSize = new Rect(0, 0, 375f, 450f);

    [MenuItem("Tools/Avatars 3.0/Inventory Inventor/About", priority = 12)]
    public static void AboutWindow()
    {
        InventoryInventorAboutWindow window = (InventoryInventorAboutWindow)GetWindow(typeof(InventoryInventorAboutWindow), false, "Inventory Inventor");
        window.minSize = new Vector2(375f, 440f);
        window.Show();
    }

    private void OnGUI()
    {
        // Define main window area.
        EditorGUILayout.BeginVertical();
        windowSize.x = (position.width / 2f) - (375f / 2f);
        windowSize.y = 5f;
        GUILayout.BeginArea(windowSize);

        DrawAboutWindow();

        GUILayout.EndArea();
        GUILayout.FlexibleSpace();
        EditorGUILayout.Space();
        EditorGUILayout.EndVertical();

        // Unfocus item in window when empty space is clicked.
        if (GUI.Button(windowSize, "", GUIStyle.none))
        {            
            GUI.FocusControl(null);
        }
    }

    // Draw about window GUI.
    private void DrawAboutWindow()
    {
        // Load version number.
        string version = InventoryInventor.GetVersion();

        // Display top header.
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<b><size=18>Inventory Inventor" + (version != "" ? " " : "") + version + "</size></b>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true }, GUILayout.Width(300f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<size=13>Author: Joshuarox100</size>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, normal = new GUIStyleState() { background = null } });
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        DrawLine();

        // Display summary.
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<b><size=15>Summary</size></b>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true }, GUILayout.Width(200f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("Make inventories fast with Inventory Inventor! With it, you can quickly create inventories with plenty of toggles, all managed by a single Expression Parameter!", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, normal = new GUIStyleState() { background = null } }, GUILayout.Width(350f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        DrawLine();

        // Display special thanks.
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<b><size=15>Special Thanks</size></b>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true }, GUILayout.Width(200f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<i><size=12>zachgregoire & ValliereMagic</size></i>\nFor helping with development, giving advice, and improving my coding knowledge overall.", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, normal = new GUIStyleState() { background = null } }, GUILayout.Width(350f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<i><size=12>Ambiguous & FlowerBunny</size></i>\nFor being my guinea pigs to test things on.", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, normal = new GUIStyleState() { background = null } }, GUILayout.Width(350f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        DrawLine();

        // Display troubleshooting.
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("<b><size=15>Troubleshooting</size></b>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true }, GUILayout.Width(200f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box("If you're having issues or want to contact me, you can find more information at the GitHub page linked below!", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, normal = new GUIStyleState() { background = null } }, GUILayout.Width(350f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        DrawLine();

        // Display GitHub link.
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("GitHub Repository", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.UpperCenter });
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Open in Browser", GUILayout.Width(250)))
        {
            Application.OpenURL("https://github.com/Joshuarox100/VRC-Inventory-Inventor");
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
    }

    // Draws a line across the GUI.
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
