#if UNITY_EDITOR
using InventoryInventor.Preset;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace InventoryInventor.Settings
{
    public class InventorSettings : ScriptableObject
    {
        [SerializeField]
        [HideInInspector]
        private bool m_AllowInvalid;

        [SerializeField]
        [HideInInspector]
        private string m_DefaultPath;

        [SerializeField]
        [HideInInspector]
        private string m_LastPath;

        [SerializeField]
        [HideInInspector]
        private bool m_Updating;

        internal static string GetSettingsPath()
        {
            return "Packages/com.joshuarox100.inventoryinventor/Editor/SETTINGS.asset";
        }

        internal static InventorSettings GetOrCreateSettings()
        {
            string settingsPath = GetSettingsPath();
            var settings = AssetDatabase.LoadAssetAtPath<InventorSettings>(settingsPath);
            if (settings == null)
            {
                settings = CreateInstance<InventorSettings>();
                settings.m_AllowInvalid = false;
                settings.m_DefaultPath = "Assets";
                settings.m_LastPath = settings.m_DefaultPath;
                settings.m_Updating = false;
                // Throwaway line to stop compiler warning for unused variable. (Short circuits on first condition).
                if (settings.m_AllowInvalid && settings.m_Updating && settings.m_LastPath == "") { }
                AssetDatabase.CreateAsset(settings, settingsPath);
                AssetDatabase.SaveAssets();
            }
            else if (!settings.m_DefaultPath.StartsWith("Assets"))
            {
                EditorUtility.SetDirty(settings);
                settings.m_DefaultPath = settingsPath.Substring(0, settingsPath.LastIndexOf("Editor") - 1) + Path.DirectorySeparatorChar + "Output";
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        public static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
    }

    //Create MyCustomSettingsProvider by deriving from SettingsProvider:
    class InventorSettingsProvider : SettingsProvider
    {
        private SerializedObject m_InventorSettings;

        class Styles
        {
            public static GUIContent allowInvalid = new GUIContent("Allow Invalid Animations", "Skip the check for invalid Animation properties when applying Presets.");
            public static GUIContent defaultPath = new GUIContent("Default Output Destination", "The default output location the Manager will fallback on when it is unable to use the provided location.");
            public static GUIContent upgradeButton = new GUIContent("Upgrade All Old Presets");
        }

        static string k_InventorSettingsPath { get { return InventorSettings.GetSettingsPath(); } }
        public InventorSettingsProvider(string path, SettingsScope scope = SettingsScope.User)
            : base(path, scope) { }

        public static bool IsSettingsAvailable()
        {
            return File.Exists(k_InventorSettingsPath);
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
           //This function is called when the user clicks on the MyCustom element in the Settings window.
           m_InventorSettings = InventorSettings.GetSerializedSettings();
        }

        public override void OnGUI(string searchContext)
        {
            m_InventorSettings.Update();

            // Auto Update
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.allowInvalid, new GUILayoutOption[] { GUILayout.MinWidth(160f) });
            m_InventorSettings.FindProperty("m_AllowInvalid").boolValue = !Convert.ToBoolean(GUILayout.Toolbar(Convert.ToInt32(!m_InventorSettings.FindProperty("m_AllowInvalid").boolValue), new string[] { "Yes", "No" }, new GUILayoutOption[] { GUILayout.MinWidth(128f), GUILayout.MaxWidth(384f) }));
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);

            // Default Path
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.defaultPath, new GUILayoutOption[] { GUILayout.MinWidth(160f) });
            EditorGUI.BeginChangeCheck();
            m_InventorSettings.FindProperty("m_DefaultPath").stringValue = EditorGUILayout.TextField(m_InventorSettings.FindProperty("m_DefaultPath").stringValue, new GUILayoutOption[] { GUILayout.MinWidth(128f), GUILayout.MaxWidth(384f) });
            if (EditorGUI.EndChangeCheck() && !m_InventorSettings.FindProperty("m_DefaultPath").stringValue.StartsWith("Assets"))
                m_InventorSettings.FindProperty("m_DefaultPath").stringValue = k_InventorSettingsPath.Substring(0, k_InventorSettingsPath.LastIndexOf("Editor") - 1) + Path.DirectorySeparatorChar + "Output";
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

            m_InventorSettings.ApplyModifiedProperties();
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

        //Register the SettingsProvider
        [SettingsProvider]
        public static SettingsProvider CreateInventorSettingsProvider()
        {
            if (IsSettingsAvailable())
            {
                var provider = new InventorSettingsProvider("Project/Inventory Inventor", SettingsScope.Project)
                {
                    //Automatically extract all keywords from the Styles.
                    keywords = GetSearchKeywordsFromGUIContentProperties<Styles>()
                };
                return provider;
            }

            //Settings Asset doesn't exist yet; no need to display anything in the Settings window.
            return null;
        }
    } 
}
#endif