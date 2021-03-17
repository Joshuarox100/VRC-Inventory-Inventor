#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections;
using UnityEngine.Networking;
using System;
using System.IO;
using InventoryInventor.Settings;
using InventoryInventor.Preset;

namespace InventoryInventor.Version
{
    [InitializeOnLoad]
    public class AutoUpdater : ScriptableObject
    {
        static AutoUpdater m_Instance = null;
        static AutoUpdater()
        {
            EditorApplication.update += OnInit;
        }
        static void OnInit()
        {
            if (!EditorApplication.isUpdating && !EditorApplication.isCompiling)
            {
                SerializedObject settings = InventorSettings.GetSerializedSettings();
                if (settings != null)
                {
                    EditorApplication.update -= OnInit;
                    m_Instance = FindObjectOfType<AutoUpdater>();
                    if (m_Instance == null)
                    {
                        m_Instance = CreateInstance<AutoUpdater>();
                        if (settings.FindProperty("m_AutoUpdate").boolValue)
                            Updater.CheckForUpdates(true);
                    }
                }
            }
        }
    }

    public class Updater : UnityEngine.Object
    {
        private readonly static string[] deletableFolders = { "Editor", "Engine", "Examples", "Images" };

        // MonoBehaviour for running network coroutines.
        private class NetworkManager : MonoBehaviour 
        {
            // Gets a desired request from the internet
            public static IEnumerator GetFileRequest(string url, string filePath, Action<UnityWebRequest> callback)
            {
                UnityWebRequest request = UnityWebRequest.Get(url);
                request.downloadHandler = new DownloadHandlerFile(filePath);
                yield return request.SendWebRequest();
                callback(request);
            }

            // Retrieves text from a provided URL.
            public static IEnumerator GetText(string url, Action<string> result)
            {
                UnityWebRequest www = UnityWebRequest.Get(url);
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError)
                {
                    Debug.LogError(www.error);
                    result?.Invoke("");
                }
                else
                {
                    result?.Invoke(www.downloadHandler.text);
                }
            }
        }

        // Returns the contents of the VERSION file if present.
        public static string GetVersion()
        {
            // Get the relative path.
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
            if (relativePath == "")
                return "";

            //Read VERSION file
            string installedVersion = (AssetDatabase.FindAssets("VERSION", new string[] { relativePath }).Length > 0) ? File.ReadAllText(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("VERSION", new string[] { relativePath })[0])) : "";

            return installedVersion;
        }

        // Gets the path to the settings asset
        private static string GetSettingsPath()
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

        // Compares the VERSION file present to the one on GitHub to see if a newer version is available.
        public static void CheckForUpdates(bool auto = false)
        {
            // Read VERSION file.
            string installedVersion = GetVersion();

            // Create hidden object to run the coroutine.
            GameObject temp = new GameObject { hideFlags = HideFlags.HideInHierarchy };

            // Run a coroutine to retrieve the GitHub data.
            NetworkManager manager = temp.AddComponent<NetworkManager>();
            manager.StartCoroutine(NetworkManager.GetText("https://raw.githubusercontent.com/Joshuarox100/VRC-Inventory-Inventor/master/Editor/VERSION", latestVersion => {
                // Network Error.
                if (latestVersion == "" && !auto)
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "Failed to fetch the latest version.\n(Check console for details.)", "Close");
                }
                // VERSION file missing.
                else if (installedVersion == "" && !auto)
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "Failed to identify installed version.\n(VERSION file was not found.)", "Close");
                }
                // Project has been archived.
                else if (latestVersion == "RIP" && !auto)
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "Project has been put on hold indefinitely.", "Close");
                }
                // An update is available.
                else if (installedVersion != latestVersion)
                {
                    if (EditorUtility.DisplayDialog("Inventory Inventor", "A new update is available! (" + latestVersion + ")\nDownload and install from GitHub?" + (auto ? "\n(You can disable update checks within Project Settings)": ""), "Yes", "No"))
                    {
                        // Download the update.
                        DownloadUpdate(latestVersion);
                    }
                }
                // Using latest version.
                else if (!auto)
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "You are using the latest version.", "Close");
                }
                DestroyImmediate(temp);
            }));
        }

        // Downloads and installs a given version of the package.
        public static void DownloadUpdate(string version)
        {
            string downloadURL = "https://github.com/Joshuarox100/VRC-Inventory-Inventor/releases/download/" + version + "/Inventory.Inventor." + version.Substring(1) + ".unitypackage";
            string filePath = $"{Application.persistentDataPath}/Files/Inventory.Inventor." + version.Substring(1) + ".unitypackage";
            Debug.Log("Download URL: " + downloadURL + "\nFile Path: " + filePath);

            // Create hidden object to run the coroutine.
            GameObject temp = new GameObject { hideFlags = HideFlags.HideInHierarchy };

            // Run a coroutine to retrieve the GitHub data.
            NetworkManager manager = temp.AddComponent<NetworkManager>();
            manager.StartCoroutine(NetworkManager.GetFileRequest(downloadURL, filePath, (UnityWebRequest req) =>
            {
                // Log potential errors
                if (req.isNetworkError || req.isHttpError)
                {
                    // Log any errors that may happen
                    EditorUtility.DisplayDialog("Inventory Inventor", "Failed to download the latest version.\n(Check console for details.)", "Close");
                    Debug.LogError("Inventory Inventor: " + req.error);
                }
                else
                {
                    if (File.Exists(filePath))
                    {
                        // Store the current settings
                        string path = GetSettingsPath();
                        bool foundSettings = false;
                        if (File.Exists(path))
                        {
                            File.Copy(path, $"{ Application.persistentDataPath}/Files/SETTINGS.asset");
                            foundSettings = true;
                        }

                        // Delete the previous version
                        string mainPath = path.Substring(0, path.LastIndexOf("Editor") - 1);
                        string[] folders = Directory.GetDirectories(mainPath);
                        foreach (string folder in folders)
                            foreach (string entry in deletableFolders)
                                if (folder.EndsWith(entry))
                                    Directory.Delete(folder, true);

                        // Import and delete the downloaded package
                        AssetDatabase.ImportPackage(filePath, false);
                        File.Delete(filePath);

                        // Restore the previous settings
                        if (foundSettings)
                        {
                            if (!Directory.Exists(mainPath + "/Editor"))
                                Directory.CreateDirectory(mainPath + "/Editor");
                            File.Copy($"{ Application.persistentDataPath}/Files/SETTINGS.asset", path);
                            File.Delete($"{ Application.persistentDataPath}/Files/SETTINGS.asset");
                        }

                        // Upgrade Presets
                        if (Type.GetType("InventoryPresetUtility") != null && Type.GetType("InventoryPresetUtility").GetMethod("UpgradeAll") != null)
                            InventoryPresetUtility.UpgradeAll();

                        // Refresh database
                        AssetDatabase.Refresh();
                    }
                    else
                        EditorUtility.DisplayDialog("Inventory Inventor", "Failed to install the latest version.\n(File could not be found.)", "Close");
                }
                DestroyImmediate(temp);
            }));
        }
    }
}
#endif