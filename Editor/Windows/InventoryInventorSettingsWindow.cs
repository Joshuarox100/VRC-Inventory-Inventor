using InventoryInventor.Preset;
using InventoryInventor.Settings;
using System;
using UnityEditor;
using UnityEngine;

namespace InventoryInventor
{
    public class InventoryInventorSettingsWindow : EditorWindow
    {
        // Window Size.
        private Rect windowSize = new Rect(0, 0, 375f, 450f);

        // Settings Asset.
        private InventorSettings settings;

        [MenuItem("Tools/Joshuarox100/Inventory Inventor/Settings", priority = 11)]
        public static void SettingsWindow()
        {
            InventoryInventorSettingsWindow window = (InventoryInventorSettingsWindow)GetWindow(typeof(InventoryInventorSettingsWindow), false, "Inventory Inventor");
            window.settings = InventorSettings.Create();
            window.minSize = new Vector2(375f, 70f);
            window.Show();
        }

        private void OnGUI()
        {
            // Define main window area.
            EditorGUILayout.BeginVertical();
            windowSize.x = (position.width / 2f) - (375f / 2f);
            windowSize.y = 5f;
            GUILayout.BeginArea(windowSize);

            DrawSettingsWindow();

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

        private void OnFocus()
        {
            if (settings != null)
                settings.Load();
        }

        // Draw settings window GUI.
        private void DrawSettingsWindow()
        {
            EditorGUI.BeginChangeCheck();

            // Allow Invalid
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.allowInvalid, new GUILayoutOption[] { GUILayout.MinWidth(160f) });
            settings.allowInvalid = !Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(!settings.allowInvalid), new string[] { "Yes", "No" }, new GUILayoutOption[] { GUILayout.MinWidth(128f), GUILayout.MaxWidth(384f) }));
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);

            // Default Path
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.defaultPath, new GUILayoutOption[] { GUILayout.MinWidth(160f) });
            EditorGUI.BeginChangeCheck();
            settings.defaultPath = EditorGUILayout.TextField(settings.defaultPath, new GUILayoutOption[] { GUILayout.MinWidth(128f), GUILayout.MaxWidth(384f) });
            if (EditorGUI.EndChangeCheck() && !settings.defaultPath.StartsWith("Assets"))
                settings.defaultPath = "Assets";
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // Upgrade All
            DrawLine(false);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Space(4f);
            if (GUILayout.Button(Styles.upgradeButton, GUILayout.MaxWidth(512f)))
                InventoryPresetUtility.UpgradeAll(true);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
                settings.Save();
        }

        class Styles
        {
            public static GUIContent allowInvalid = new GUIContent("Allow Invalid Animations", "Skip the check for invalid Animation properties when applying Presets.");
            public static GUIContent defaultPath = new GUIContent("Default Output Destination", "The default output location the Manager will fallback on when it is unable to use the provided location.");
            public static GUIContent upgradeButton = new GUIContent("Upgrade All Old Presets");
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
}