#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections;
using UnityEngine.Networking;
using System;
using System.IO;

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
            EditorApplication.update -= OnInit;
            m_Instance = FindObjectOfType<AutoUpdater>();
            if (m_Instance == null)
            {
                m_Instance = CreateInstance<AutoUpdater>();
                Updater.CheckForUpdates(true);
            }
        }
    }

    public class Updater : UnityEngine.Object
    {
        // Blank MonoBehaviour for running network coroutines.
        private class NetworkManager : MonoBehaviour { }

        // Returns the contents of the VERSION file if present.
        public static string GetVersion()
        {
            // Get the relative path.
            string[] guids = AssetDatabase.FindAssets(typeof(Updater).ToString());
            string relativePath = "";
            foreach (string guid in guids)
            {
                string tempPath = AssetDatabase.GUIDToAssetPath(guid);
                if (tempPath.LastIndexOf(typeof(Updater).ToString()) == tempPath.Length - typeof(Updater).ToString().Length - 3)
                {
                    relativePath = tempPath.Substring(0, tempPath.LastIndexOf("Inventory Inventor") - 1);
                    break;
                }
            }
            if (relativePath == "")
                return "";

            //Read VERSION file
            string installedVersion = (AssetDatabase.FindAssets("VERSION", new string[] { relativePath }).Length > 0) ? File.ReadAllText(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("VERSION", new string[] { relativePath })[0])) : "";

            return installedVersion;
        }

        // Compares the VERSION file present to the one on GitHub to see if a newer version is available.
        public static void CheckForUpdates(bool auto = false)
        {
            // Read VERSION file.
            string installedVersion = GetVersion();

            // Create hidden object to run the coroutine.
            GameObject netMan = new GameObject { hideFlags = HideFlags.HideInHierarchy };

            // Run a coroutine to retrieve the GitHub data.
            netMan.AddComponent<NetworkManager>().StartCoroutine(GetText("https://raw.githubusercontent.com/Joshuarox100/VRC-Inventory-Inventor/master/Editor/VERSION", latestVersion => {
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
                    if (EditorUtility.DisplayDialog("Inventory Inventor", "A new update is available! (" + latestVersion + ")\nOpen the Releases page?", "Yes", "No"))
                    {
                        Application.OpenURL("https://github.com/Joshuarox100/VRC-Inventory-Inventor/releases/latest");
                    }
                }
                // Using latest version.
                else if (!auto)
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "You are using the latest version.", "Close");
                }
                DestroyImmediate(netMan);
            }));
        }

        // Retrieves text from a provided URL.
        private static IEnumerator GetText(string url, Action<string> result)
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
}
#endif