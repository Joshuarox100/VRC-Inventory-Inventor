using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using InventoryInventor.Preset;
using InventoryInventor.Version;
using InventoryInventor.Settings;

namespace InventoryInventor
{
    public class InventoryInventorWindow : EditorWindow
    {
        // Model Object.
        private readonly Manager manager = new Manager();

        // Window Size.
        private Rect windowSize = new Rect(0, 0, 375f, 350f);

        // Various Trackers.
        private int windowTab;
        private bool focused;
        private Vector2 scroll;

        // Removal Preview.
        private List<AnimatorControllerLayer> layers;
        private List<AnimatorControllerParameter> parameters;
        private List<bool> expression;

        // Colors
        private readonly static Color32 navy = new Color32(143, 0, 254, 255);   

        [MenuItem("Tools/Joshuarox100/Inventory Inventor/Manage Inventory", priority = 0)]
        public static void ManageInventory()
        {
            InventoryInventorWindow window = (InventoryInventorWindow)GetWindow(typeof(InventoryInventorWindow), false, "Inventory Inventor");
            window.manager.outputPath = InventorSettings.GetSerializedSettings().FindProperty("m_LastPath").stringValue;
            window.minSize = new Vector2(375f, 355f);
            window.wantsMouseMove = true;
            window.Show();
        }

        [MenuItem("Tools/Joshuarox100/Inventory Inventor/Check For Updates", priority = 11)]
        public static void CheckForUpdates()
        {
            Updater.CheckForUpdates();
        }

        // Relocate file directory path.
        private void OnFocus()
        {
            manager.UpdatePaths();
        }

        // Draw GUI.
        private void OnGUI()
        {
            // Define main window area.
            EditorGUILayout.BeginVertical();
            windowSize.x = (position.width / 2f) - (375f / 2f);
            windowSize.y = 5f;
            GUILayout.BeginArea(windowSize);

            // Create toolbar.
            windowTab = GUILayout.Toolbar(windowTab, new string[] { "Create", "Remove" });
            DrawLine(false);

            // Respond to current toolbar selection.
            EditorGUILayout.BeginVertical();
            if (EditorApplication.isPlaying)
            {
                // Stop usage within play mode.
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Inventories can only be managed outside of Play Mode.", MessageType.Info);
            }
            else
            {
                switch (windowTab)
                {
                    case 0:
                        DrawCreateWindow();
                        break;
                    case 1:
                        DrawRemoveWindow();
                        break;
                }
            }
            EditorGUILayout.EndVertical();
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

        // Draws the creation window GUI.
        private void DrawCreateWindow()
        {
            EditorGUILayout.BeginVertical();

            // Check for avatars in the scene.
            EditorGUILayout.BeginVertical(GUILayout.Height(54f));
            GUILayout.FlexibleSpace();
            SelectAvatarDescriptor();
            if (manager.avatar == null)
            {
                EditorGUILayout.HelpBox("No Avatars found in the current Scene!", MessageType.Warning);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            DrawLine();

            // List optional settings.
            EditorGUILayout.LabelField("Optional Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
            // Expressions Menu
            manager.menu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(new GUIContent("Expressions Menu", "The Expressions Menu you want the inventory controls added to. Leave this empty if you don't want any menus to be affected.\n(Controls will be added as a submenu.)"), manager.menu, typeof(VRCExpressionsMenu), true);
            EditorGUI.BeginChangeCheck();
            // Animator Controller
            manager.controller = (AnimatorController)EditorGUILayout.ObjectField(new GUIContent("Animator Controller", "The Animator Controller to modify.\n(If left empty, a new Animator Controller will be created and used.)"), manager.controller, typeof(AnimatorController), false);
            if (EditorGUI.EndChangeCheck())
            {
                manager.PreviewRemoval(out layers, out parameters, out expression);
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(3);
            DrawLine();

            // List Inventory Settings.
            EditorGUILayout.LabelField("Inventory Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
            // Preset
            EditorGUI.BeginChangeCheck();
            manager.preset = (InventoryPreset)EditorGUILayout.ObjectField(new GUIContent("Preset", "The preset to apply to the Animator."), manager.preset, typeof(InventoryPreset), false);
            if (EditorGUI.EndChangeCheck() && manager.preset != null && manager.preset.LastPath != null && manager.preset.LastPath != "")
                manager.outputPath = manager.preset.LastPath;
            // Refresh Rate
            manager.refreshRate = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Refresh Rate", "How long each synced (or unsaved) toggle is given to synchronize with late joiners (seconds per item)."), manager.refreshRate), 0f, float.MaxValue);
            EditorGUILayout.EndVertical();
            GUILayout.Space(3);
            DrawLine();

            // List Output Settings.
            EditorGUILayout.LabelField("Output Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")), GUILayout.Height(50f));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Destination", "The folder where generated files will be saved to."), new GUIStyle(GUI.skin.GetStyle("Box")) { alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { textColor = GUI.skin.label.normal.textColor, background = null } });
            GUILayout.FlexibleSpace();
            // Format file path to fit in a button.
            string displayPath = (manager.outputPath != null) ? manager.outputPath.Replace('\\', '/') : "";
            displayPath = (new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, hover = GUI.skin.GetStyle("Button").active }.CalcSize(new GUIContent("<i>" + displayPath + "</i>")).x > 210) ? "..." + displayPath.Substring((displayPath.IndexOf('/', displayPath.Length - 29) != -1) ? displayPath.IndexOf('/', displayPath.Length - 29) : displayPath.Length - 29) : displayPath;
            while (new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, normal = GUI.skin.GetStyle("Button").normal, hover = GUI.skin.GetStyle("Button").active }.CalcSize(new GUIContent("<i>" + displayPath + "</i>")).x > 210)
            {
                displayPath = "..." + displayPath.Substring(4);
            }
            // Destination.
            if (GUILayout.Button(new GUIContent("<i>" + displayPath + "</i>", (manager.outputPath != null) ? manager.outputPath.Replace('\\', '/') : ""), new GUIStyle(GUI.skin.GetStyle("Button")) { richText = true, hover = GUI.skin.GetStyle("Button").active }, GUILayout.MinWidth(210)))
            {
                string absPath = EditorUtility.OpenFolderPanel("Destination Folder", "", "");
                if (absPath.StartsWith(Application.dataPath))
                {
                    manager.outputPath = "Assets" + absPath.Substring(Application.dataPath.Length);
                    SerializedObject settings = InventorSettings.GetSerializedSettings();
                    settings.FindProperty("m_LastPath").stringValue = manager.outputPath;
                    settings.ApplyModifiedProperties();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            // Overwrite All.
            EditorGUILayout.PrefixLabel(new GUIContent("Overwrite All", "Automatically overwrite existing files if needed."));
            manager.autoOverwrite = !Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(!manager.autoOverwrite), new string[] { "Yes", "No" }));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            DrawLine();

            // Create Button.
            if (GUILayout.Button("Create"))
            {
                manager.CreateInventory();
                EditorUtility.ClearProgressBar();
                manager.PreviewRemoval(out layers, out parameters, out expression);
            }
            EditorGUILayout.EndVertical();

            // Repaint when hovering over window (needed for Destination button).
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

        // Draw removal window GUI.
        private void DrawRemoveWindow()
        {
            EditorGUILayout.BeginVertical();

            // Check for avatars in the scene.
            EditorGUILayout.BeginVertical(GUILayout.Height(54f));
            GUILayout.FlexibleSpace();
            SelectAvatarDescriptor();
            if (manager.avatar == null)
            {
                EditorGUILayout.HelpBox("No Avatars found in the current Scene!", MessageType.Warning);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            DrawLine();

            // List optional settings.
            EditorGUILayout.LabelField("Optional Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
            EditorGUILayout.BeginHorizontal(GUILayout.Height(5f));
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            // Animator Controller.
            manager.controller = (AnimatorController)EditorGUILayout.ObjectField(new GUIContent("Animator Controller", "The Animator Controller to modify."), manager.controller, typeof(AnimatorController), false);
            EditorGUILayout.BeginHorizontal();
            // Remove Parameters.
            EditorGUILayout.PrefixLabel(new GUIContent("Remove Parameters", "Remove generated parameters from the Animator Controller."));
            manager.removeParameters = !Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(!manager.removeParameters), new string[] { "Yes", "No" }));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal(GUILayout.Height(5f));
            EditorGUILayout.EndHorizontal();
            // Remove Expressions
            if (manager.removeParameters)
            {
                // Separator
                var rect = EditorGUILayout.BeginHorizontal();
                Handles.color = Color.gray;
                Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.width + 5f, rect.y + 1));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal(GUILayout.Height(5f));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                var old = EditorStyles.label.normal.textColor;
                EditorStyles.label.normal.textColor = navy;
                EditorGUILayout.PrefixLabel(new GUIContent("Include Expression", "Also remove any related Expression Parameters."));
                manager.removeExpParams = !Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(!manager.removeExpParams), new string[] { "Yes", "No" }));
                EditorStyles.label.normal.textColor = old;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal(GUILayout.Height(5f));
                EditorGUILayout.EndHorizontal();
            }
            if (EditorGUI.EndChangeCheck() || layers == null || parameters == null || expression == null)
            {
                manager.PreviewRemoval(out layers, out parameters, out expression);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            DrawLine();

            // Preview of items that will be removed.
            EditorGUILayout.LabelField("Will Be Removed", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(new GUIStyle(GUI.skin.GetStyle("Box")));
            if (layers.Count > 0 || parameters.Count > 0)
            {
                var topR = EditorGUILayout.BeginHorizontal();
                GUILayout.Space(4);
                if (layers.Count > 0)
                    EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel, GUILayout.Width(155f));
                if (parameters.Count > 0)
                {
                    Handles.color = Color.gray;
                    Handles.DrawLine(new Vector2(topR.x + (topR.width / 2) - 17, topR.y), new Vector2(topR.x + (topR.width / 2) - 17, topR.y + topR.height));
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel, GUILayout.Width(155f));
                }
                EditorGUILayout.EndHorizontal();
                // Separator
                var r = EditorGUILayout.BeginHorizontal();
                Handles.color = Color.gray;
                Handles.DrawLine(new Vector2(r.x, r.y + 1), new Vector2(r.x + r.width + 5f, r.y + 1));
                EditorGUILayout.EndHorizontal();

                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical(GUILayout.Width(155f));
                if (layers.Count > 0)
                {
                    for (int i = 0; i < layers.Count; i++)
                    {
                        // Separator
                        if (i != 0)
                        {
                            var rect = EditorGUILayout.BeginHorizontal();
                            Handles.color = Color.gray;
                            Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.x + rect.width + 5f, rect.y + 1));
                            EditorGUILayout.EndHorizontal();
                        }

                        EditorGUILayout.LabelField(layers[i].name, GUILayout.Width(155f));
                    }
                    EditorGUILayout.Space();
                }
                EditorGUILayout.EndVertical();

                // Separator
                var midRect = EditorGUILayout.BeginVertical(GUILayout.Width(155f));
                Handles.color = Color.gray;
                Handles.DrawLine(new Vector2(midRect.x, midRect.y), new Vector2(midRect.x, midRect.y + midRect.height));

                if (parameters.Count > 0)
                {
                    for (int i = 0; i < parameters.Count; i++)
                    {
                        // Separator
                        if (i != 0)
                        {
                            var rect = EditorGUILayout.BeginHorizontal();
                            Handles.color = Color.gray;
                            Handles.DrawLine(new Vector2(rect.x, rect.y + 1), new Vector2(rect.x + rect.width + 5f, rect.y + 1));
                            EditorGUILayout.EndHorizontal();
                        }

                        if (manager.removeExpParams && expression[i])
                            EditorGUILayout.LabelField(parameters[i].name, new GUIStyle(GUI.skin.label) { normal = new GUIStyleState() { textColor = navy } }, GUILayout.Width(155f));
                        else
                            EditorGUILayout.LabelField(parameters[i].name, GUILayout.Width(155f));
                    }
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndScrollView();
            }
            else
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("<i>No Inventory Detected</i>", new GUIStyle(GUI.skin.GetStyle("Box")) { richText = true, alignment = TextAnchor.MiddleLeft, normal = new GUIStyleState() { background = null } });
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            DrawLine();

            // Remove Button.
            if (GUILayout.Button("Remove"))
            {
                if (layers.Count == 0 && parameters.Count == 0)
                {
                    EditorUtility.DisplayDialog("Inventory Inventory", "ERROR: No Inventory was found on the provided Animator Controller.", "Close");
                }
                else if (EditorUtility.DisplayDialog("Inventory Inventory", "WARNING: This action is unreversable.\nContinue?", "Yes", "Cancel"))
                {
                    manager.RemoveInventory();
                    EditorUtility.ClearProgressBar();
                    manager.PreviewRemoval(out layers, out parameters, out expression);
                }
            }
            EditorGUILayout.EndVertical();
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
                var nextIndex = EditorGUILayout.Popup(new GUIContent("Active Avatar", "The Avatar you want to manage an inventory for."), currentIndex, names);
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
            if (manager.avatar != null)
            {
                if (manager.controller == null)
                    manager.controller = manager.avatar.baseAnimationLayers.Length == 5 ? ((manager.avatar.baseAnimationLayers[4].animatorController != null) ? (AnimatorController)manager.avatar.baseAnimationLayers[4].animatorController : null) : ((manager.avatar.baseAnimationLayers[2].animatorController != null) ? (AnimatorController)manager.avatar.baseAnimationLayers[2].animatorController : null);

                if (manager.controller != null)
                    manager.PreviewRemoval(out layers, out parameters, out expression);

                if (manager.menu == null)
                    manager.menu = manager.avatar.expressionsMenu != null ? manager.avatar.expressionsMenu : null;

            }
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
