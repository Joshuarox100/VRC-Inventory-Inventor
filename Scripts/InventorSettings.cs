#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace InventoryInventor.Settings
{
    public class InventorSettings : ScriptableObject
    {
        [SerializeField]
        [HideInInspector]
        private bool m_AutoUpdate;

        [SerializeField]
        [HideInInspector]
        private string m_DefaultPath;

        [SerializeField]
        [HideInInspector]
        private string m_LastPath;

        internal static string GetSettingsPath()
        {
            string filter = "InventoryInventor";
            string[] guids = AssetDatabase.FindAssets(filter);
            string relativePath = "";
            foreach (string guid in guids)
            {
                string tempPath = AssetDatabase.GUIDToAssetPath(guid);
                if (tempPath.LastIndexOf(filter) == tempPath.Length - filter.Length - 3)
                {
                    relativePath = tempPath.Substring(0, tempPath.LastIndexOf("Editor") - 1);
                    break;
                }
            }
            return relativePath + Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar + "SETTINGS.asset";
        }

        internal static InventorSettings GetOrCreateSettings()
        {
            string settingsPath = GetSettingsPath();
            var settings = AssetDatabase.LoadAssetAtPath<InventorSettings>(settingsPath);
            if (settings == null)
            {
                settings = CreateInstance<InventorSettings>();
                settings.hideFlags = HideFlags.HideInHierarchy;
                settings.m_AutoUpdate = true;
                settings.m_DefaultPath = settingsPath.Substring(0, settingsPath.LastIndexOf("Editor") - 1) + Path.DirectorySeparatorChar + "Output";
                settings.m_LastPath = settings.m_DefaultPath;
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

    // Register a SettingsProvider using IMGUI for the drawing framework:
    static class InventorSettingsIMGUIRegister
    {
        [SettingsProvider]
        public static SettingsProvider CreateInventorSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            var provider = new SettingsProvider("Project/Inventory Inventor", SettingsScope.Project)
            {
                // By default the last token of the path is used as display name if no label is provided.
                label = "Inventory Inventor",
                // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
                guiHandler = (searchContext) =>
                {
                    var settings = InventorSettings.GetSerializedSettings();
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(settings.FindProperty("m_AutoUpdate"), new GUIContent("Startup Update Prompts"));
                    EditorGUILayout.PropertyField(settings.FindProperty("m_DefaultPath"), new GUIContent("Default File Destination"));
                    if (EditorGUI.EndChangeCheck() && !settings.FindProperty("m_DefaultPath").stringValue.StartsWith("Assets"))
                    {
                        settings.FindProperty("m_DefaultPath").stringValue = InventorSettings.GetSettingsPath().Substring(0, InventorSettings.GetSettingsPath().LastIndexOf("Editor") - 1) + Path.DirectorySeparatorChar + "Output";
                        settings.ApplyModifiedProperties();
                    }
                },

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[] { "Startup Update Prompts", "Default File Destination" })
            };

            return provider;
        }
    }
}
#endif