﻿using InventoryInventor.Libraries.BMBLibraries.Classes;
using InventoryInventor.Libraries.BMBLibraries.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.VersionControl;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using InventoryInventor.Preset;
using InventoryInventor.Deprecated;
using InventoryInventor.Settings;

namespace InventoryInventor
{
    public class Manager : UnityEngine.Object
    {
        // Objects to modify.
        public VRCAvatarDescriptor avatar;
        public VRCExpressionsMenu menu;
        public AnimatorController controller;

        // Input objects.
        public InventoryPreset preset;
        public float refreshRate = 0.05f;
        public bool removeParameters = false;
        public bool removeExpParams = false;

        // Path related.
        public string relativePath;
        public string outputPath;
        public bool autoOverwrite = false;

        // File backup.
        private Backup backupManager;
        private AssetList generated;

        // Other data.
        private int totalToggles;

        /// <summary>
        /// Called by the Editor window to create an inventory from the stored preset onto the selected Avatar.
        /// </summary>
        public void CreateInventory()
        {
            // Try catch block because there's surely some Exception I can't account for.
            try
            {
                // Initial Save.
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Check that the selected avatar is valid.
                if (avatar == null)
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: No Avatar selected.", "Close");
                    return;
                }

                if (avatar.expressionParameters == null)
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Avatar does not have an Expression Parameters object assigned in the descriptor.", "Close");
                    Selection.activeObject = avatar;
                    return;
                }

                // Make sure a preset was actually provided.
                if (preset == null)
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: No preset provided.", "Close");
                    return;
                }

                // Upgrade the Preset if necessary.
                if (preset.Version < InventoryPresetUtility.currentVersion)
                {
                    InventoryPresetUtility.UpgradePreset(preset);
                }
                
                // Check Compatability of Clips
                if (!InventorSettings.GetSerializedSettings().FindProperty("m_AllowInvalid").boolValue)
                {
                    foreach (var item in preset.Pages.SelectMany(page => page.Items))
                    {
                        if (!item.UseAnimations)
                        {
                            continue;
                        }
                        
                        if (!item.IsEnableClipCompatible)
                        {
                            EditorUtility.DisplayDialog("Inventory Inventor",
                                $"ERROR: {item.EnableClip.name} cannot be used because it modifies an invalid property type!\nUse \"Allow Invalid Animations\" to ignore this and continue.", "Close");
                            Selection.activeObject = item.EnableClip;
                            return;
                        }

                        if (!item.IsDisableClipCompatible)
                        {
                            EditorUtility.DisplayDialog("Inventory Inventor",
                                $"ERROR: {item.DisableClip.name} cannot be used because it modifies an invalid property type!\nUse \"Allow Invalid Animations\" to ignore this and continue.", "Close");
                            Selection.activeObject = item.DisableClip;
                            return;
                        }
                    }
                }

                var parameterManager = new ParameterManager();
                
                parameterManager.AddExpressionParameter(new VRCExpressionParameters.Parameter
                {
                    name = "Inventory",
                    valueType = VRCExpressionParameters.ValueType.Int,
                    defaultValue = 0,
                    saved = false,
                });
                
                parameterManager.AddExpressionParameter(new VRCExpressionParameters.Parameter
                {
                    name = "Inventory Loaded",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = 1,
                    saved = true,
                });
                
                parameterManager.AddAnimatorParameter(new AnimatorControllerParameter
                {
                    name = "IsLocal",
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = false,
                });

                // Add all item Parameters
                preset.Pages.ForEach(page => page.Items.ForEach(item =>
                {
                    parameterManager.AddExpressionParameters(item.ExpressionParameters);
                    parameterManager.AddAnimatorParameters(item.AnimatorParameters);
                }));

                if (!parameterManager.CanApplyToExpressions(avatar.expressionParameters))
                {
                    // ToDo: make the error more detailed
                    EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Not enough memory available for Expression Parameters.", "Close");
                    Selection.activeObject = avatar.expressionParameters;
                    return;
                }
                
                var totalUsage = preset.Pages.Aggregate(
                    1,
                    (pageSum, page) =>
                        pageSum + page.Items.Aggregate(
                            0,
                            (pageItemSum, item) => pageItemSum + item.RequiredStates
                        )
                );
                
                // Larger than int to manage (Failsafe).
                if (totalUsage > 256)
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Preset uses too much data for syncing! (Limit: 255; Used: " + (totalUsage - 1) + ")", "Close");
                    Selection.activeObject = preset;
                    return;
                }

                totalToggles = preset.Pages.Aggregate(
                    1,
                    (pageSum, page) =>
                        pageSum + page.Items.Aggregate(
                            0,
                            (pageItemSum, item) => pageItemSum + (item.Type == PageItem.ItemType.Toggle ? 1 : 0)
                        )
                );

                // Check that the file destination exists.
                if (!VerifyDestination())
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Neither the chosen destination folder or the default folder could be created.", "Close");
                    return;
                }

                // Start doing modifications.
                EditorUtility.DisplayProgressBar("Inventory Inventor", "Starting", 0);

                // Initialize backup objects.
                backupManager = new Backup();
                generated = new AssetList();

                // Get FX Animator.
                var animator = controller != null ? controller : null;

                // Replace the Animator Controller in the descriptor if this Controller was there to begin with.
                var replaceAnimator = new List<bool>();
                foreach (var c in avatar.baseAnimationLayers)
                {
                    if (c.animatorController != null && animator == (AnimatorController)c.animatorController)
                        replaceAnimator.Add(true);
                    else
                        replaceAnimator.Add(false);
                }
                foreach (var c in avatar.specialAnimationLayers)
                {
                    if (c.animatorController != null && animator == (AnimatorController)c.animatorController)
                        replaceAnimator.Add(true);
                    else
                        replaceAnimator.Add(false);
                }

                // Create new Animator Controller from SDK template if none was provided.
                if (animator == null)
                {
                    switch (CopySDKTemplate(avatar.name + "_FX.controller", "vrc_AvatarV3FaceLayer"))
                    {
                        case 1:
                            EditorUtility.DisplayDialog("Inventory Inventor", "Cancelled.", "Close");
                            RevertChanges();
                            return;
                        case 3:
                            EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Failed to create one or more files!", "Close");
                            RevertChanges();
                            return;
                    }

                    animator = (AssetDatabase.FindAssets(avatar.name + "_FX", new[] { outputPath + Path.DirectorySeparatorChar + "Animators" }).Length != 0) ? (AnimatorController)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets(avatar.name + "_FX", new string[] { outputPath + Path.DirectorySeparatorChar + "Animators" })[0]), typeof(AnimatorController)) : null;

                    if (animator == null)
                    {
                        EditorUtility.DisplayDialog("Inventory Inventor", "Failed to copy template Animator from VRCSDK.", "Close");
                        RevertChanges();
                        return;
                    }
                }
                else
                {
                    backupManager.AddToBackup(new Asset(AssetDatabase.GetAssetPath(animator)));
                }

                // Duplicate the source controller.
                var controllerPath = Path.Combine(relativePath, "temp.controller");
                AssetDatabase.CopyAsset(new Asset(AssetDatabase.GetAssetPath(animator)).path, controllerPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                var newAnimator = (AnimatorController)AssetDatabase.LoadAssetAtPath(controllerPath, typeof(AnimatorController));
                generated.Add(new Asset(AssetDatabase.GetAssetPath(newAnimator)));

                // Remove Inventory layers.
                for (var i = animator.layers.Length - 1; i >= 0; i--)
                {
                    // A layer is an Inventory layer if the State Machine has a InventoryMachine behaviour attached.
                    // Or it has a "special transition"
                    var hasSpecialTransition = false;
                    foreach (var transition in animator.layers[i].stateMachine.anyStateTransitions)
                    {
                        if (transition.name == "InventoryMachineIdentifier" &&
                            transition.isExit == false &&
                            transition.mute &&
                            transition.destinationState == null &&
                            transition.destinationStateMachine == null
                        )
                        {
                            hasSpecialTransition = true;
                            break;
                        }
                    }

                    // If it's not a layer we care about, skip
                    if (!hasSpecialTransition)
                    {
                        continue;
                    }

                    if (animator.layers[i].stateMachine.behaviours.Length >= 1 &&
                        animator.layers[i].stateMachine.behaviours[0].GetType() == typeof(InventoryMachine)
                    )
                    {
                        EditorUtility.DisplayProgressBar("Inventory Inventor",
                            $"Removing Layers: {animator.layers[i].name}", 0.05f * (float.Parse((animator.layers.Length - i).ToString()) / animator.layers.Length));

                        newAnimator.RemoveLayer(i);

                        EditorUtility.DisplayProgressBar("Inventory Inventor",
                            $"Removing Layers: {animator.layers[i].name}", 0.05f * (((animator.layers.Length - i) + 1f) / animator.layers.Length));
                    }
                }
                newAnimator.SaveController();

                // Replace the old Animator Controller.
                var path = AssetDatabase.GetAssetPath(animator);
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(newAnimator), path);
                AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(newAnimator), path.Substring(path.LastIndexOf(Path.DirectorySeparatorChar) + 1));
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Replace the Animator Controller in the descriptor if it was there.
                if (replaceAnimator.Contains(true))
                {
                    for (var i = 0; i < avatar.baseAnimationLayers.Length; i++)
                        if (replaceAnimator[i])
                            avatar.baseAnimationLayers[i].animatorController = newAnimator;
                    for (var i = 0; i < avatar.specialAnimationLayers.Length; i++)
                        if (replaceAnimator[i + avatar.baseAnimationLayers.Length])
                            avatar.specialAnimationLayers[i].animatorController = newAnimator;
                }
                controller = newAnimator;

                /*
                    Generate Clips for Game Objects.
                 */

                var missingItems = false;
                foreach (Page page in preset.Pages)
                {
                    foreach (PageItem item in page.Items)
                    {
                        if (item.Type == PageItem.ItemType.Toggle && !item.UseAnimations)
                        {
                            if (!item.ObjectReference.Equals("") && avatar.transform.Find(item.ObjectReference) != null)
                            {
                                switch (GenerateToggleClip(avatar.transform.Find(item.ObjectReference).gameObject, true))
                                {
                                    case 1:
                                        EditorUtility.DisplayDialog("Inventory Inventor", "Cancelled.", "Close");
                                        RevertChanges();
                                        return;
                                    case 2:
                                        EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Failed to create one or more files!", "Close");
                                        RevertChanges();
                                        return;
                                }

                                switch (GenerateToggleClip(avatar.transform.Find(item.ObjectReference).gameObject, false))
                                {
                                    case 1:
                                        EditorUtility.DisplayDialog("Inventory Inventor", "Cancelled.", "Close");
                                        RevertChanges();
                                        return;
                                    case 2:
                                        EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Failed to create one or more files!", "Close");
                                        RevertChanges();
                                        return;
                                }
                            }
                            else if (avatar.transform.Find(item.ObjectReference) == null)
                                missingItems = true;
                        }
                    }
                }
                if (missingItems && !EditorUtility.DisplayDialog("Inventory Inventor", "WARNING: One or more objects used by this Preset is not present on the Avatar. Continue?\n(Their respective States will be left empty.)", "Continue", "Cancel"))
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "Cancelled.", "Close");
                    RevertChanges();
                    Selection.activeObject = preset;
                    return;
                }

                /*
                    Create parameters.
                */
                EditorUtility.DisplayProgressBar("Inventory Inventor", "Creating Parameters", 0.05f);
                parameterManager.ApplyToAnimator(newAnimator);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayProgressBar("Inventory Inventor", "Creating Parameters", 0.1f);

                /*
                    Create layers.
                */
                CreateMasterLayer(newAnimator, totalToggles, out List<PageItem> items, out List<KeyValuePair<List<int>, List<int>>> activeStates);
                CreateItemLayers(newAnimator, ref items, ref activeStates);

                /*
                    Set default bool states.
                */
                EditorUtility.DisplayProgressBar("Inventory Inventor", "Saving Controller", 0.9f);
                newAnimator.SaveController();
                AssetDatabase.SaveAssets();

                /*
                    Add expression parameters to the list.
                */

                parameterManager.ApplyToExpressions(avatar.expressionParameters);
                
                EditorUtility.DisplayProgressBar("Inventory Inventor", "Finalizing", 0.9f);

                /*
                    Create Expressions menu for toggles.
                */
                var menuExists = menu != null;

                switch (CreateMenus(out VRCExpressionsMenu inventory, items.Count))
                {
                    case CreateMenuResult.Cancel:
                        EditorUtility.DisplayDialog("Inventory Inventor", "Cancelled.", "Close");
                        RevertChanges();
                        return;
                    case CreateMenuResult.FailedAsset:
                        EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Failed to create one or more files!", "Close");
                        RevertChanges();
                        return;
                }

                // Assign the Inventory menu to given menu if possible and provided.
                if (menuExists && menu != null)
                {
                    bool exists = false;

                    // Check if the control existed prior. If it did, just replace it.
                    foreach (VRCExpressionsMenu.Control control in menu.controls)
                    {
                        if (control.name == preset.Pages[0].name && control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu == null)
                        {
                            exists = true;
                            control.icon = preset.Pages[0].Icon;
                            control.subMenu = inventory;
                            break;
                        }
                    }

                    // Otherwise, if there is free space available, add the menu as a control.
                    if (!exists && menu.controls.ToArray().Length < 8)
                    {
                        menu.controls.Add(new VRCExpressionsMenu.Control() { name = preset.Pages[0].name, icon = preset.Pages[0].Icon, type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = inventory });
                        EditorUtility.SetDirty(menu);
                    }
                    else if (!exists)
                    {
                        EditorUtility.DisplayDialog("Inventory Inventory", "WARNING: Inventory controls were not added to the provided menu.\n(No space is available.)", "Close");
                    }
                }
                else if (menuExists)
                {
                    avatar.expressionsMenu = inventory;
                    EditorUtility.SetDirty(avatar);
                    menu = inventory;
                }

                EditorUtility.DisplayProgressBar("Inventory Inventor", "Finalizing", 1f);

                /*
                    Save configuration.
                 */

                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Inventory Inventory", "Success!", "Close");

                // Focus the Editor on the Inventory menu.
                Selection.activeObject = inventory;
                return;
            }
            catch (Exception err)
            {
                EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: An exception has occured!\nCheck the console for more details.", "Close");
                Debug.LogError(err);
                RevertChanges();
                return;
            }
        }

        /// <summary>
        /// Checks if the destination is valid.
        /// </summary>
        /// <returns>True if destination is valid.</returns>
        private bool VerifyDestination()
        {
            // Load the settings object.
            var settings = InventorSettings.GetSerializedSettings();

            // If the destination is not valid, create it if possible or use the default.
            if (!AssetDatabase.IsValidFolder(outputPath))
            {
                try
                {
                    Directory.CreateDirectory(outputPath);
                }
                catch (Exception err)
                {
                    if (outputPath == settings.FindProperty("m_DefaultPath").stringValue ||
                        !EditorUtility.DisplayDialog("Inventory Inventor",
                            "WARNING: Could not create the chosen destination.\nTry again with the default location?",
                            "Yes", "No"))
                    {
                        outputPath = settings.FindProperty("m_LastPath").stringValue;
                        Debug.LogError(err);
                        return false;
                    }
                    outputPath = settings.FindProperty("m_DefaultPath").stringValue;
                    VerifyDestination();
                }
            }

            // Update the last path used.
            settings.FindProperty("m_LastPath").stringValue = outputPath;
            settings.ApplyModifiedProperties();
            preset.LastPath = outputPath;
            InventoryPresetUtility.SaveChanges(preset);
            return true;
        }

        // Copies an Animator Controller from the VRCSDK to the given location.
        private int CopySDKTemplate(string outFile, string SDKfile)
        {
            if (!AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Animators"))
                AssetDatabase.CreateFolder(outputPath, "Animators");
            bool existed = true;
            if (File.Exists(outputPath + Path.DirectorySeparatorChar + "Animators" + Path.DirectorySeparatorChar + outFile))
            {
                if (!autoOverwrite)
                {
                    switch (EditorUtility.DisplayDialogComplex("Inventory Inventor", outFile + " already exists!\nOverwrite the file?", "Overwrite", "Cancel", "Skip"))
                    {
                        case 1:
                            return 1;
                        case 2:
                            return 2;
                    }
                }
                backupManager.AddToBackup(new Asset(outputPath + Path.DirectorySeparatorChar + "Animators" + Path.DirectorySeparatorChar + outFile));
            }
            else
            {
                existed = false;
            }
            if (!AssetDatabase.CopyAsset(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets(SDKfile, new string[] { "Assets" + Path.DirectorySeparatorChar + "VRCSDK" + Path.DirectorySeparatorChar + "Examples3" + Path.DirectorySeparatorChar + "Animation" + Path.DirectorySeparatorChar + "Controllers" })[0]), outputPath + Path.DirectorySeparatorChar + "Animators" + Path.DirectorySeparatorChar + outFile))
            {
                return 3;
            }
            else
            {
                AssetDatabase.Refresh();
                if (!existed)
                    generated.Add(new Asset(outputPath + Path.DirectorySeparatorChar + "Animators" + Path.DirectorySeparatorChar + outFile));
            }
            return 0;
        }

        enum CreateMenuResult
        {
            Success = 0,
            Cancel = 1,
            Skip = 2,
            FailedAsset = 3,
        }
        /// <summary>
        /// Creates all the menus needed for the generated Inventory.
        /// </summary>
        /// <param name="mainMenu"></param>
        /// <param name="totalItems"></param>
        /// <returns></returns>
        private CreateMenuResult CreateMenus(out VRCExpressionsMenu mainMenu, int totalItems)
        {
            mainMenu = null;

            // Create a list of menu objects and instantiate a new one for each page.
            List<VRCExpressionsMenu> pages = new List<VRCExpressionsMenu>();

            // Instantiate the menus prior to configuring them.
            for (int i = 0; i < preset.Pages.Count; i++)
            {
                pages.Add(ScriptableObject.CreateInstance<VRCExpressionsMenu>());
                pages[i].name = preset.Pages[i].name;
            }

            // Loop through each page, adding controls as the preset specifies.
            int index = 0;
            int index2 = 0;

            var stateSequence = new StateSequence();
            
            for (var i = 0; i < preset.Pages.Count; i++)
            {
                foreach (var item in preset.Pages[i].Items)
                {
                    item.MakeControl(stateSequence);
                    switch (item.Type)
                    {
                        case PageItem.ItemType.Toggle:
                            pages[i].controls.Add(new VRCExpressionsMenu.Control
                            {
                                name = item.name,
                                icon = item.Icon,
                                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                                parameter = new VRCExpressionsMenu.Control.Parameter() { name = "Inventory" },
                                value = index + 1,
                            });
                            index++;
                            break;
                        case PageItem.ItemType.Subpage:
                            int val = item.PageReference != null && preset.Pages.Contains(item.PageReference)
                                ? preset.Pages.IndexOf(item.PageReference)
                                : 0;
                            pages[i].controls.Add(new VRCExpressionsMenu.Control
                            {
                                name = item.name,
                                icon = item.PageReference != null && preset.Pages.Contains(item.PageReference)
                                    ? preset.Pages[val].Icon
                                    : null,
                                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                                subMenu = item.PageReference != null && preset.Pages.Contains(item.PageReference)
                                    ? pages[val]
                                    : null
                            });
                            break;
                        case PageItem.ItemType.Control:
                            pages[i].controls.Add(new VRCExpressionsMenu.Control
                            {
                                name = item.Control.name,
                                icon = item.Control.icon,
                                type = item.Control.type,
                                parameter = item.Control.parameter,
                                value = item.Control.value,
                                style = item.Control.style,
                                labels = item.Control.labels,
                                subMenu = item.Control.subMenu,
                                subParameters = item.Control.subParameters
                            });
                            break;
                        case PageItem.ItemType.Button:
                            pages[i].controls.Add(new VRCExpressionsMenu.Control()
                            {
                                name = item.name, icon = item.Icon,
                                type = VRCExpressionsMenu.Control.ControlType.Button,
                                parameter = new VRCExpressionsMenu.Control.Parameter() { name = "Inventory" },
                                value = totalItems + index2 + 1
                            });
                            index2++;
                            break;
                    }
                }

                EditorUtility.DisplayProgressBar("Inventory Inventor", "Finalizing", 0.95f + i * 1f / preset.Pages.Count * 0.025f);
            }

            // Create output directory if not present.
            if (!AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Menus"))
                AssetDatabase.CreateFolder(outputPath, "Menus");

            // Create / overwrite each menu asset to the directory.
            for (int i = 0; i < pages.Count; i++)
            {
                VRCExpressionsMenu page = pages[i];
                bool exists = true;
                if (File.Exists(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + avatar.name + "_" + page.name + ".asset"))
                {
                    if (!autoOverwrite)
                    {
                        switch (EditorUtility.DisplayDialogComplex("Inventory Inventor", avatar.name + "_" + page.name + ".asset" + " already exists!\nOverwrite the file?", "Overwrite", "Cancel", "Skip"))
                        {
                            case 1:
                                return CreateMenuResult.Cancel;
                            case 2:
                                return CreateMenuResult.Skip;
                        }
                    }
                    backupManager.AddToBackup(new Asset(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + avatar.name + "_" + page.name + ".asset"));
                    AssetDatabase.DeleteAsset(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + avatar.name + "_" + page.name + ".asset");
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                else
                {
                    exists = false;
                }
                AssetDatabase.CreateAsset(page, outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + avatar.name + "_" + page.name + ".asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Check that the asset was saved successfully.
                if (AssetDatabase.FindAssets(page.name + " t:VRCExpressionsMenu", new string[] { outputPath + Path.DirectorySeparatorChar + "Menus" }).Length == 0)
                {
                    return CreateMenuResult.FailedAsset;
                }
                else
                {
                    AssetDatabase.Refresh();
                    // Update the object in list
                    pages[i] = (VRCExpressionsMenu)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets(page.name + " t:VRCExpressionsMenu", new string[] { outputPath + Path.DirectorySeparatorChar + "Menus" })[0]), typeof(VRCExpressionsMenu));
                    if (pages[i] == null)
                    {
                        Debug.LogError("Inventory Inventor: Type mismatch when loading menu: " + page.name);
                        return CreateMenuResult.FailedAsset;
                    }    
                    if (!exists)
                        generated.Add(new Asset(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + page.name + ".asset"));
                }
                EditorUtility.DisplayProgressBar("Inventory Inventor", "Finalizing", 0.975f + i * 1f / pages.Count * 0.025f);
            }

            // Reassign created menus to each other as submenus so the reference persists post restart.
            for (int i = 0; i < pages.Count; i++)
            {
                VRCExpressionsMenu current = pages[i];
                Page currentPage = preset.Pages[i];
                EditorUtility.SetDirty(current);
                index = 0;
                foreach (VRCExpressionsMenu.Control control in current.controls)
                {
                    if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && currentPage.Items[index].Type == PageItem.ItemType.Subpage && currentPage.Items[index].PageReference != null)
                        control.subMenu = preset.Pages.Contains(currentPage.Items[index].PageReference) ? pages[preset.Pages.IndexOf(currentPage.Items[index].PageReference)] : null;
                    index++;
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Out the top level menu.
            mainMenu = pages[0];

            return CreateMenuResult.Success;
        }

        // Creates layers for each item in the inventory (ordered by page).
        private void CreateItemLayers(AnimatorController source, ref List<PageItem> items, ref List<KeyValuePair<List<int>, List<int>>> activeStates)
        {
            /*
               CODERS NOTE: Instantiation of Unity Objects is unbelievably slow. Back when I was using a float instead of an int, duplicating a template object instead of instantiating a new one decreased the total execution time immensely. So although this method is absolutely required for large data sets to complete in a reasonable amount of time, it likely isn't as important with the data set used now. It still works with smaller sets though so I didn't bother to change it. "If it ain't broke, don't fix it."
             */

            // Create a template machine to duplicate.
            AnimatorStateMachine templateMachine = new AnimatorStateMachine
            {
                // For whatever reason, using this behaviour crashes VRChat when switching avatars even though it doesn't get uploaded :/
                // For now, a special transition is used instead later on.

                //// This behaviour is added so it will be detected as a Inventory layer.
                //behaviours = new StateMachineBehaviour[] { ScriptableObject.CreateInstance<InventoryMachine>() }
            };
            ChildAnimatorState[] states = new ChildAnimatorState[templateMachine.states.Length + 3];
            templateMachine.states.CopyTo(states, 3);

            // Create a template state to duplicate.
            ChildAnimatorState templateState = new ChildAnimatorState
            {
                state = new AnimatorState
                {
                    name = "",
                    motion = null,
                    behaviours = new StateMachineBehaviour[] { ScriptableObject.CreateInstance<VRCAvatarParameterDriver>() }
                }
            };
            ((VRCAvatarParameterDriver)templateState.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = 0 });

            // Get a starting position for the states.
            Vector3 pos = templateMachine.anyStatePosition;

            // Create an off state.
            Helper.ChangeState(templateState, "Off");
            templateState.position = pos - new Vector3(150, -45);
            states[0] = templateState.DeepClone();

            // Create an on state.
            Helper.ChangeState(templateState, "On");
            templateState.position = pos + new Vector3(100, 45);
            states[1] = templateState.DeepClone();

            // Create an idle state.
            Helper.ChangeState(templateState, "Default");
            templateState.position = pos + new Vector3(-25, 145);
            templateState.state.behaviours = new StateMachineBehaviour[0];
            states[2] = templateState.DeepClone();

            // Add the states to the template machine.
            templateMachine.states = states;

            // Create a template transition.
            AnimatorStateTransition templateTransition = new AnimatorStateTransition
            {
                destinationState = null,
                isExit = false,
                hasExitTime = false,
                duration = 0,
                canTransitionToSelf = false,
                conditions = null
            };

            // Pregenerate and assign layer names.
            List<string> layerNames = new List<string>();

            for (int i = 0; i < items.Count; i++)
            {
                string name = items[i].name;

                // Deal with layers that have the same name.          
                List<string> names = new List<string>();
                for (int j = 0; j < layerNames.Count; j++)
                {
                    if (i != j)
                        names.Add(layerNames[j]);
                }
                string pageName = "";
                foreach (Page page in preset.Pages)
                {
                    if (page.Items.Contains(items[i]))
                    {
                        pageName = page.name;
                        break;
                    }
                }
                if (names.Contains(name + " (" + pageName + ")"))
                {
                    int occurance = 0;
                    while (names.Contains(name + " (" + pageName + ") " + occurance))
                    {
                        occurance++;
                    }
                    name = items[i].name + " (" + pageName + ") " + occurance;
                }
                else if (names.Contains(name))
                {
                    int otherIndex = layerNames.IndexOf(name);
                    string otherPageName = "";
                    foreach (Page page in preset.Pages)
                    {
                        if (page.Items.Contains(items[otherIndex]))
                        {
                            otherPageName = page.name;
                            break;
                        }
                    }
                    layerNames[otherIndex] = items[otherIndex].name + " (" + otherPageName + ")";

                    name = items[i].name + " (" + pageName + ")";
                }

                layerNames.Add(name);
            }

            // For each item in the inventory...
            for (int i = 0; i < items.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Inventory Inventor", string.Format(CultureInfo.InvariantCulture, "Creating Item Layers: {0} ({1:#0.##%})", items[i].name, (i + 1f) / items.Count), 0.55f + (0.35f * (float.Parse(i.ToString()) / items.Count)));

                // Grab its active states.
                KeyValuePair<List<int>, List<int>> active = activeStates[i];

                // Create a layer.
                source.AddLayer(layerNames[i]);
                AnimatorControllerLayer[] layers = source.layers;
                AnimatorControllerLayer currentLayer = layers[layers.Length - 1];
                currentLayer.defaultWeight = 1;

                // Create transitions to the on and off state with their assigned conditionals.
                List<AnimatorStateTransition> transitions = new List<AnimatorStateTransition>();
                List<AnimatorStateTransition> defaultTransitions = new List<AnimatorStateTransition>();
                if (items[i].UseAnimations && items[i].TransitionDuration > 0)
                {
                    templateTransition.hasFixedDuration = !items[i].TransitionType;
                    templateTransition.duration = items[i].TransitionDuration;
                }
                else
                {
                    templateTransition.hasFixedDuration = false;
                    templateTransition.duration = 0f;
                }

                // Disabled state.
                AnimationClip toggleClip = !items[i].UseAnimations ? null : items[i].DisableClip;
                if (!items[i].UseAnimations && !items[i].ObjectReference.Equals("") && avatar.transform.Find(items[i].ObjectReference) != null)
                {
                    foreach (string guid in AssetDatabase.FindAssets(avatar.transform.Find(items[i].ObjectReference).gameObject.name + "_Off", new string[] { outputPath + Path.DirectorySeparatorChar + "Clips" }))
                    {
                        AnimationClip tempClip = (AnimationClip)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), typeof(AnimationClip));
                        if (tempClip.name == avatar.transform.Find(items[i].ObjectReference).gameObject.name + "_Off")
                        {
                            toggleClip = tempClip;
                            break;
                        }
                    }
                }
                Helper.ChangeState(templateMachine.states[0].state, toggleClip);
                ((VRCAvatarParameterDriver)templateMachine.states[0].state.behaviours[0]).parameters[0].name = "Inventory " + (i + 1);
                ((VRCAvatarParameterDriver)templateMachine.states[0].state.behaviours[0]).parameters[0].value = 0;

                // Add a transition for each disabled value.
                for (int j = 0; j < active.Key.Count; j++)
                {
                    Helper.ChangeTransition(templateTransition, active.Key[j], templateMachine.states[0].state);
                    transitions.Add((AnimatorStateTransition)templateTransition.DeepClone(templateMachine.states[0]));
                    if (items[i].Sync == PageItem.SyncMode.Off)
                        transitions[transitions.Count - 1].AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
                    transitions[transitions.Count - 1].AddCondition(AnimatorConditionMode.If, 0, "Inventory Loaded");
                }

                // Special transition for local toggles.
                if (items[i].Sync == PageItem.SyncMode.Off || (items[i].Sync == PageItem.SyncMode.Auto && items[i].Saved))
                {
                    Helper.ChangeTransition(templateTransition, "" + (i + 1), false, templateMachine.states[0].state);
                    transitions.Add((AnimatorStateTransition)templateTransition.DeepClone(templateMachine.states[0]));
                    if (items[i].Sync == PageItem.SyncMode.Off)
                        transitions[transitions.Count - 1].AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
                    defaultTransitions.Add((AnimatorStateTransition)transitions[transitions.Count - 1].DeepClone(templateMachine.states[0]));
                    transitions[transitions.Count - 1].AddCondition(AnimatorConditionMode.If, 0, "Inventory Loaded");
                }
                else
                {
                    Helper.ChangeTransition(templateTransition, "" + (i + 1), false, templateMachine.states[0].state);
                    defaultTransitions.Add((AnimatorStateTransition)templateTransition.DeepClone(templateMachine.states[0]));
                }
                defaultTransitions[defaultTransitions.Count - 1].AddCondition(AnimatorConditionMode.If, 0, "Inventory Loaded");
                if (items[i].TransitionOffset)
                    defaultTransitions[defaultTransitions.Count - 1].offset = 1f;

                // Add transitions to the other state.
                templateMachine.states[1].state.transitions = transitions.ToArray();
                transitions.Clear();

                // Enabled state.
                toggleClip = !items[i].UseAnimations ? null : items[i].EnableClip;
                if (!items[i].UseAnimations && !items[i].ObjectReference.Equals("") && avatar.transform.Find(items[i].ObjectReference) != null)
                {
                    foreach (string guid in AssetDatabase.FindAssets(avatar.transform.Find(items[i].ObjectReference).gameObject.name + "_On", new string[] { outputPath + Path.DirectorySeparatorChar + "Clips" }))
                    {
                        AnimationClip tempClip = (AnimationClip)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), typeof(AnimationClip));
                        if (tempClip.name == avatar.transform.Find(items[i].ObjectReference).gameObject.name + "_On")
                        {
                            toggleClip = tempClip;
                            break;
                        }
                    }
                }
                Helper.ChangeState(templateMachine.states[1].state, toggleClip);
                ((VRCAvatarParameterDriver)templateMachine.states[1].state.behaviours[0]).parameters[0].name = "Inventory " + (i + 1);
                ((VRCAvatarParameterDriver)templateMachine.states[1].state.behaviours[0]).parameters[0].value = 1;

                // Add a transition for each enabled value.
                for (int j = 0; j < active.Value.Count; j++)
                {
                    Helper.ChangeTransition(templateTransition, active.Value[j], templateMachine.states[1].state);
                    transitions.Add((AnimatorStateTransition)templateTransition.DeepClone(templateMachine.states[1]));
                    if (items[i].Sync == PageItem.SyncMode.Off)
                        transitions[transitions.Count - 1].AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
                    transitions[transitions.Count - 1].AddCondition(AnimatorConditionMode.If, 0, "Inventory Loaded");
                }

                // Special transition for local toggles.
                if (items[i].Sync == PageItem.SyncMode.Off || (items[i].Sync == PageItem.SyncMode.Auto && items[i].Saved))
                {
                    Helper.ChangeTransition(templateTransition, "" + (i + 1), true, templateMachine.states[1].state);
                    transitions.Add((AnimatorStateTransition)templateTransition.DeepClone(templateMachine.states[1]));
                    if (items[i].Sync == PageItem.SyncMode.Off)
                        transitions[transitions.Count - 1].AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
                    defaultTransitions.Add((AnimatorStateTransition)transitions[transitions.Count - 1].DeepClone(templateMachine.states[1]));
                    transitions[transitions.Count - 1].AddCondition(AnimatorConditionMode.If, 0, "Inventory Loaded");
                }
                else
                {
                    Helper.ChangeTransition(templateTransition, "" + (i + 1), true, templateMachine.states[1].state);
                    defaultTransitions.Add((AnimatorStateTransition)templateTransition.DeepClone(templateMachine.states[1]));
                }
                defaultTransitions[defaultTransitions.Count - 1].AddCondition(AnimatorConditionMode.If, 0, "Inventory Loaded");
                if (items[i].TransitionOffset)
                    defaultTransitions[defaultTransitions.Count - 1].offset = 1f;

                // Add transitions to the other state.
                templateMachine.states[0].state.transitions = transitions.ToArray();
                transitions.Clear();

                // Idle state.
                toggleClip = !items[i].UseAnimations ? null : items[i].InitialState ? items[i].EnableClip : items[i].DisableClip;
                if (!items[i].UseAnimations && !items[i].ObjectReference.Equals("") && avatar.transform.Find(items[i].ObjectReference) != null)
                {
                    foreach (string guid in items[i].InitialState ? AssetDatabase.FindAssets(avatar.transform.Find(items[i].ObjectReference).gameObject.name + "_On", new string[] { outputPath + Path.DirectorySeparatorChar + "Clips" }) : AssetDatabase.FindAssets(avatar.transform.Find(items[i].ObjectReference).gameObject.name + "_Off", new string[] { outputPath + Path.DirectorySeparatorChar + "Clips" }))
                    {
                        AnimationClip tempClip = (AnimationClip)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), typeof(AnimationClip));
                        if (tempClip.name == (items[i].InitialState ? avatar.transform.Find(items[i].ObjectReference).gameObject.name + "_On" : avatar.transform.Find(items[i].ObjectReference).gameObject.name + "_Off"))
                        {
                            toggleClip = tempClip;
                            break;
                        }
                    }
                }
                Helper.ChangeState(templateMachine.states[2].state, toggleClip);
                if (items[i].TransitionOffset)
                {
                    // Set the speed to the smallest negative number possible, forces animations to skip to their end point on the first possible frame even with looping turned off.
                    templateMachine.states[2].state.speed = -0.000000000000001f;
                }
                else
                    templateMachine.states[2].state.speed = 0f;

                // Add transitions to the default state.
                templateMachine.states[2].state.transitions = defaultTransitions.ToArray();
                defaultTransitions.Clear();

                // Inventory machine identifier
                transitions.Add(new AnimatorStateTransition
                {
                    name = "InventoryMachineIdentifier",
                    hasExitTime = false,
                    isExit = false,
                    mute = true,
                    destinationState = null,
                    destinationStateMachine = null,
                });

                // Configure the machine and clone it
                templateMachine.name = currentLayer.name;
                templateMachine.defaultState = templateMachine.states[2].state;
                templateMachine.anyStateTransitions = transitions.ToArray();
                currentLayer.stateMachine = templateMachine.DeepClone();
                layers[layers.Length - 1] = currentLayer;
                source.layers = layers;

                transitions.Clear();
            }
            return;
        }

        // Creates the master layer that handles menu inputs and the idle sync.
        private void CreateMasterLayer(AnimatorController source, int itemTotal, out List<PageItem> items, out List<KeyValuePair<List<int>, List<int>>> activeStates)
        {
            /*
               CODERS NOTE: Instantiation of Unity Objects is unbelievably slow. Back when I was using a float instead of an int, duplicating a template object instead of instantiating a new one decreased the total execution time immensely. So although this method is absolutely required for large data sets to complete in a reasonable amount of time, it likely isn't as important with the data set used now. It still works with smaller sets though so I didn't bother to change it. "If it ain't broke, don't fix it."
             */

            EditorUtility.DisplayProgressBar("Inventory Inventor", "Creating Master Layer: Preparing", 0.1f);

            // Add Master Layer.
            source.AddLayer("Inventory Master");
            AnimatorControllerLayer masterLayer = source.layers[source.layers.Length - 1];

            // For whatever reason, using this behaviour crashes VRChat when switching avatars even though it doesn't get uploaded :/
            // For now, a special transition is used instead later on.

            //// This behaviour is added to identify this layer as an Inventory layer.
            //masterLayer.stateMachine.behaviours = new StateMachineBehaviour[] { ScriptableObject.CreateInstance<InventoryMachine>() };

            // Get a list of toggles.
            items = new List<PageItem>();
            List<PageItem> buttons = new List<PageItem>();
            foreach (Page page in preset.Pages)
            {
                foreach (PageItem item in page.Items)
                {
                    if (item.Type == PageItem.ItemType.Toggle)
                    {
                        items.Add(item);
                    }
                    else if (item.Type == PageItem.ItemType.Button)
                    {
                        buttons.Add(item);
                    }
                }
            }

            // Create List of state values (KEY = DISABLE | VALUE = ENABLE).
            activeStates = new List<KeyValuePair<List<int>, List<int>>>();

            // Fill the list with initial values.
            int value = itemTotal + buttons.Count + 1;
            for (int i = 0; i < itemTotal; i++)
            {
                switch (items[i].Sync)
                {
                    // Off uses up to 3 values.
                    case PageItem.SyncMode.Off:
                        activeStates.Add(new KeyValuePair<List<int>, List<int>>(new List<int>() { }, new List<int>() { }));
                        if (items[i].EnableGroup.Length > 0)
                        {
                            activeStates[activeStates.Count - 1].Value.Add(value);
                            value++;
                        }
                        if (items[i].DisableGroup.Length > 0)
                        {
                            activeStates[activeStates.Count - 1].Key.Add(value);
                            value++;
                        }
                        break;
                    // Manual uses 3 values always.
                    case PageItem.SyncMode.Manual:
                        activeStates.Add(new KeyValuePair<List<int>, List<int>>(new List<int>() { value }, new List<int>() { value + 1 }));
                        value += 2;
                        break;
                    // Auto uses between 1 and 5 values.
                    case PageItem.SyncMode.Auto:
                        if (items[i].Saved)
                        {
                            activeStates.Add(new KeyValuePair<List<int>, List<int>>(new List<int>(), new List<int>()));
                        }
                        else
                        {
                            activeStates.Add(new KeyValuePair<List<int>, List<int>>(new List<int>() { value }, new List<int>() { value + 1 }));
                            value += 2;
                        }
                        if (items[i].EnableGroup.Length > 0)
                        {
                            activeStates[activeStates.Count - 1].Value.Add(value);
                            value++;
                        }
                        if (items[i].DisableGroup.Length > 0)
                        {
                            activeStates[activeStates.Count - 1].Key.Add(value);
                            value++;
                        }
                        break;
                }
            }

            // Create an array states to be created.
            List<ChildAnimatorState> states = new List<ChildAnimatorState>();
            states.AddRange(masterLayer.stateMachine.states);

            // Store a starting position for the states.
            Vector3 pos = masterLayer.stateMachine.entryPosition;

            // Create the starting state.
            states.Add(new ChildAnimatorState
            {
                position = pos + new Vector3(-25, 50),
                state = new AnimatorState
                {
                    name = "Idle",
                    behaviours = new StateMachineBehaviour[] { ScriptableObject.CreateInstance<VRCAvatarParameterDriver>() }
                }
            });
            ((VRCAvatarParameterDriver)states[states.Count - 1].state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = 0 });

            // Create a template state for cloning.
            ChildAnimatorState templateState = new ChildAnimatorState
            {
                state = new AnimatorState
                {
                    name = "",
                    behaviours = new StateMachineBehaviour[] { ScriptableObject.CreateInstance<VRCAvatarParameterDriver>() }
                }
            };
            ((VRCAvatarParameterDriver)templateState.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = 0 });

            // Move down a row in the grid.
            pos += new Vector3(0, 125);

            // Create a template transition.
            AnimatorStateTransition templateTransition = new AnimatorStateTransition
            {
                destinationState = null,
                isExit = false,
                hasExitTime = true,
                exitTime = refreshRate,
                duration = 0,
                canTransitionToSelf = false,
                conditions = null
            };

            // Loop through the toggles and add them to the synchronization loop if Auto Sync is enabled.
            bool syncExists = false;
            for (int i = 0; i < items.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Inventory Inventor", string.Format(CultureInfo.InvariantCulture, "Creating Master Layer: Creating States ({0:#0.##%})", (i + 1f) / (items.Count * 2)), 0.1f + (0.225f * ((i + 1f) / (items.Count * 2))));

                if (items[i].Sync == PageItem.SyncMode.Auto && !items[i].Saved)
                {
                    // Create the enabled sync state.
                    Helper.ChangeState(templateState.state, "Syncing " + (i + 1), activeStates[i].Value[0]);
                    templateState.position = pos - new Vector3(150, 0);
                    states.Add(templateState.DeepClone());

                    // Create the disabled sync state.
                    Helper.ChangeState(templateState.state, "Syncing " + (i + 1) + " ", activeStates[i].Key[0]);
                    templateState.position = pos + new Vector3(100, 0);
                    states.Add(templateState.DeepClone());

                    if (i > 0 && syncExists)
                    {
                        // Create transitions to enabled state from the previous pair.
                        Helper.ChangeTransition(templateTransition, states[states.Count - 2], (i + 1), true);
                        states[states.Count - 3].state.AddTransition((AnimatorStateTransition)AnimatorExtensions.DeepClone(templateTransition, states[states.Count - 2]));
                        Helper.ChangeTransition(templateTransition, states[states.Count - 2], (i + 1), true);
                        states[states.Count - 4].state.AddTransition((AnimatorStateTransition)AnimatorExtensions.DeepClone(templateTransition, states[states.Count - 2]));

                        // Create transitions to disabled state from the previous pair.
                        Helper.ChangeTransition(templateTransition, states[states.Count - 1], (i + 1), false);
                        states[states.Count - 3].state.AddTransition((AnimatorStateTransition)AnimatorExtensions.DeepClone(templateTransition, states[states.Count - 1]));
                        Helper.ChangeTransition(templateTransition, states[states.Count - 1], (i + 1), false);
                        states[states.Count - 4].state.AddTransition((AnimatorStateTransition)AnimatorExtensions.DeepClone(templateTransition, states[states.Count - 1]));
                    }
                    else
                    {
                        // Track that at least one item is synced for later.
                        syncExists = true;
                    }

                    // Move down a row.
                    pos += new Vector3(0, 75);
                }
            }
            // Final transitions for the Auto Sync loop.
            if (states.Count > 2)
            {
                // Template transition not used here since it's faster and more efficient to write it this way.
                states[states.Count - 1].state.AddExitTransition();
                states[states.Count - 1].state.transitions[0].AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
                states[states.Count - 1].state.transitions[0].hasExitTime = true;
                states[states.Count - 1].state.transitions[0].exitTime = refreshRate;
                states[states.Count - 1].state.transitions[0].duration = 0;
                states[states.Count - 2].state.AddExitTransition();
                states[states.Count - 2].state.transitions[0].AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
                states[states.Count - 2].state.transitions[0].hasExitTime = true;
                states[states.Count - 2].state.transitions[0].exitTime = refreshRate;
                states[states.Count - 2].state.transitions[0].duration = 0;
            }

            // First transition to trap remote clients (or acts as an idle state when Auto Sync is disabled for all items).
            states[0].state.AddExitTransition();
            states[0].state.transitions[0].AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            states[0].state.transitions[0].hasExitTime = false;
            states[0].state.transitions[0].duration = 0;
            masterLayer.stateMachine.exitPosition = pos;

            // Create a template toggle state.
            ChildAnimatorState templateToggle = new ChildAnimatorState
            {
                state = new AnimatorState { behaviours = new StateMachineBehaviour[] { ScriptableObject.CreateInstance<VRCAvatarParameterDriver>() } }
            };

            // Create a template toggle transition.
            AnimatorStateTransition toggleTransition = new AnimatorStateTransition
            {
                isExit = true,
                exitTime = 1f,
                hasExitTime = true,
                duration = 0f,
                canTransitionToSelf = false
            };

            // Reset or adjust some existing values.
            templateTransition.hasExitTime = false;
            pos += new Vector3(0, 60);

            // Create an array of AnyState transitions.
            List<AnimatorStateTransition> anyTransitions = new List<AnimatorStateTransition>();

            // For each item in the inventory...
            for (int i = 0; i < items.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Inventory Inventor", string.Format(CultureInfo.InvariantCulture, "Creating Master Layer: Creating States ({0:#0.##%})", (i + items.Count) / (items.Count * 2f)), 0.1f + (0.225f * ((i + items.Count) / (items.Count * 2f))));

                // Create an On state.
                templateToggle.state.name = ("Toggling " + (i + 1) + ": On");
                templateToggle.position = pos - new Vector3(150, 0);

                // Adjust parameter settings.
                switch (items[i].Sync)
                {
                    // Off: If a group is present, use that value instead of the boolean.
                    case PageItem.SyncMode.Off:
                        if (items[i].EnableGroup.Length > 0)
                            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = activeStates[i].Value[0] });
                        else
                        {
                            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = 0 });
                            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory " + (i + 1), value = 1 });
                        }
                        break;
                    // Manual: Use the first values in active states.
                    case PageItem.SyncMode.Manual:
                        ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = 0 });
                        ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters[0].value = activeStates[i].Value[0];
                        break;
                    // Auto: If a group is present, use a value different than the one used in the syncing loop (if the item isn't saved).
                    case PageItem.SyncMode.Auto:
                        ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = 0 });
                        if (items[i].EnableGroup.Length > 0 && !items[i].Saved)
                            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters[0].value = activeStates[i].Value[1];
                        else
                        {
                            if (items[i].Saved && items[i].EnableGroup.Length == 0)
                            {
                                ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                                {
                                    name = "Inventory " + (i + 1),
                                    value = 1f
                                });
                            }
                            else
                                ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters[0].value = activeStates[i].Value[0];
                        }
                        break;
                }
                // Add group settings.
                for (int j = 0; j < items[i].EnableGroup.Length; j++)
                {
                    // Catch faulty data
                    if (items[i].EnableGroup[j].Item != null && items.IndexOf(items[i].EnableGroup[j].Item) == -1)
                    {
                        EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: " + items[i].name + "\'s Enable Group has a corrupt member in Slot " + (j + 1) + ". Please reassign the member to the desired Item to fix it. The program will now abort forcefully.", "Close");
                        Selection.activeObject = preset;
                        throw new Exception("Inventory Inventor: Corrupt member data found in " + items[i].name + "/Enable Group/Slot " + (j + 1) + ".");
                    }
                    // If the group item refers to an actual toggle.
                    if (items[i].EnableGroup[j].Item != null)
                        switch (items[i].EnableGroup[j].Reaction)
                        {
                            // Add this toggle's value to the list of disabled states for the group item.
                            case GroupItem.GroupType.Disable:
                                switch (items[i].Sync)
                                {
                                    case PageItem.SyncMode.Off:
                                        if (items[i].EnableGroup.Length > 0)
                                            if (!activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Key.Contains(activeStates[i].Value[0]))
                                                activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Key.Add(activeStates[i].Value[0]);
                                        break;
                                    case PageItem.SyncMode.Manual:
                                        if (!activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Key.Contains(activeStates[i].Value[0]))
                                            activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Key.Add(activeStates[i].Value[0]);
                                        break;
                                    case PageItem.SyncMode.Auto:
                                        if (items[i].EnableGroup.Length > 0 && !items[i].Saved)
                                        {
                                            if (!activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Key.Contains(activeStates[i].Value[1]))
                                                activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Key.Add(activeStates[i].Value[1]);
                                        }
                                        else
                                            if (!activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Key.Contains(activeStates[i].Value[0]))
                                            activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Key.Add(activeStates[i].Value[0]);
                                        break;
                                }
                                break;
                            // Add this toggle's value to the list of enabled states for the group item.
                            case GroupItem.GroupType.Enable:
                                switch (items[i].Sync)
                                {
                                    case PageItem.SyncMode.Off:
                                        if (items[i].EnableGroup.Length > 0)
                                            if (!activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Value.Contains(activeStates[i].Value[0]))
                                                activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Value.Add(activeStates[i].Value[0]);
                                        break;
                                    case PageItem.SyncMode.Manual:
                                        if (!activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Value.Contains(activeStates[i].Value[0]))
                                            activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Value.Add(activeStates[i].Value[0]);
                                        break;
                                    case PageItem.SyncMode.Auto:
                                        if (items[i].EnableGroup.Length > 0 && !items[i].Saved)
                                        {
                                            if (!activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Value.Contains(activeStates[i].Value[1]))
                                                activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Value.Add(activeStates[i].Value[1]);
                                        }
                                        else
                                            if (!activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Value.Contains(activeStates[i].Value[0]))
                                            activeStates[items.IndexOf(items[i].EnableGroup[j].Item)].Value.Add(activeStates[i].Value[0]);
                                        break;
                                }
                                break;
                        }
                }

                // Clone the template state.
                states.Add(templateToggle.DeepClone());

                // Clone and configure exit transitions.
                AnimatorStateTransition[] exitTransitions = new AnimatorStateTransition[] { (AnimatorStateTransition)toggleTransition.DeepClone() };
                states[states.Count - 1].state.transitions = exitTransitions;

                // Remove the parameters.
                while (((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Count > 0)
                {
                    ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.RemoveAt(0);
                }

                // Configure the AnyState transition template.
                templateTransition.destinationState = states[states.Count - 1].state;
                templateTransition.conditions = new AnimatorCondition[0];
                templateTransition.AddCondition(AnimatorConditionMode.Equals, i + 1, "Inventory");
                templateTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "Inventory " + (i + 1));
                templateTransition.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");

                // Clone the transition and move on to the Off state.
                anyTransitions.Add((AnimatorStateTransition)templateTransition.DeepClone(states[states.Count - 1]));

                // Create an Off state.
                templateToggle.state.name = ("Toggling " + (i + 1) + ": Off");
                templateToggle.position = pos + new Vector3(100, 0);

                // Adjust parameter settings.
                switch (items[i].Sync)
                {
                    // Off: If a group is present, use that value instead of the boolean.
                    case PageItem.SyncMode.Off:
                        if (items[i].DisableGroup.Length > 0)
                            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = activeStates[i].Key[0] });
                        else
                        {
                            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = 0 });
                            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory " + (i + 1), value = 0 });
                        }
                        break;
                    // Manual: Use the first values in active states.
                    case PageItem.SyncMode.Manual:
                        ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = 0 });
                        ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters[0].value = activeStates[i].Key[0];
                        break;
                    // Auto: If a group is present, use a value different than the one used in the syncing loop.
                    case PageItem.SyncMode.Auto:
                        ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = 0 });
                        if (items[i].DisableGroup.Length > 0 && !items[i].Saved)
                            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters[0].value = activeStates[i].Key[1];
                        else
                        {
                            if (items[i].Saved && items[i].DisableGroup.Length == 0)
                            {
                                ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter()
                                {
                                    name = "Inventory " + (i + 1),
                                    value = 0f
                                });
                            }
                            else
                                ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters[0].value = activeStates[i].Key[0];
                        }
                        break;
                }

                // Add group settings.
                for (int j = 0; j < items[i].DisableGroup.Length; j++)
                {
                    // Catch faulty data
                    if (items[i].DisableGroup[j].Item != null && items.IndexOf(items[i].DisableGroup[j].Item) == -1)
                    {
                        EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: " + items[i].name + "\'s Disable Group has a corrupt member in Slot " + (j + 1) + ". Please reassign the member to the desired Item to fix it. The program will now abort forcefully.", "Close");
                        Selection.activeObject = preset;
                        throw new Exception("Inventory Inventor: Corrupt member data found in " + items[i].name + "/Disable Group/Slot " + (j + 1) + ".");
                    }
                    // If the group item refers to an actual toggle.
                    if (items[i].DisableGroup[j].Item != null)
                        switch (items[i].DisableGroup[j].Reaction)
                        {
                            // Add this toggle's value to the list of disabled states for the group item.
                            case GroupItem.GroupType.Disable:
                                switch (items[i].Sync)
                                {
                                    case PageItem.SyncMode.Off:
                                        if (items[i].DisableGroup.Length > 0)
                                            if (!activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Key.Contains(activeStates[i].Key[0]))
                                                activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Key.Add(activeStates[i].Key[0]);
                                        break;
                                    case PageItem.SyncMode.Manual:
                                        if (!activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Key.Contains(activeStates[i].Key[0]))
                                            activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Key.Add(activeStates[i].Key[0]);
                                        break;
                                    case PageItem.SyncMode.Auto:
                                        if (items[i].DisableGroup.Length > 0 && !items[i].Saved)
                                        {
                                            if (!activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Key.Contains(activeStates[i].Key[1]))
                                                activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Key.Add(activeStates[i].Key[1]);
                                        }
                                        else
                                            if (!activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Key.Contains(activeStates[i].Key[0]))
                                            activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Key.Add(activeStates[i].Key[0]);
                                        break;
                                }
                                break;
                            // Add this toggle's value to the list of enabled states for the group item.
                            case GroupItem.GroupType.Enable:
                                switch (items[i].Sync)
                                {
                                    case PageItem.SyncMode.Off:
                                        if (items[i].DisableGroup.Length > 0)
                                            if (!activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Value.Contains(activeStates[i].Key[0]))
                                                activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Value.Add(activeStates[i].Key[0]);
                                        break;
                                    case PageItem.SyncMode.Manual:
                                        if (!activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Value.Contains(activeStates[i].Key[0]))
                                            activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Value.Add(activeStates[i].Key[0]);
                                        break;
                                    case PageItem.SyncMode.Auto:
                                        if (items[i].DisableGroup.Length > 0 && !items[i].Saved)
                                        {
                                            if (!activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Value.Contains(activeStates[i].Key[1]))
                                                activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Value.Add(activeStates[i].Key[1]);
                                        }
                                        else
                                            if (!activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Value.Contains(activeStates[i].Key[0]))
                                            activeStates[items.IndexOf(items[i].DisableGroup[j].Item)].Value.Add(activeStates[i].Key[0]);
                                        break;
                                }
                                break;
                        }
                }

                // Clone the template state.
                states.Add(templateToggle.DeepClone());

                // Clone and configure exit transitions.
                exitTransitions = new AnimatorStateTransition[] { (AnimatorStateTransition)toggleTransition.DeepClone() };
                states[states.Count - 1].state.transitions = exitTransitions;

                // Remove the parameters.
                while (((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Count > 0)
                {
                    ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.RemoveAt(0);
                }

                // Configure the AnyState transition template.
                templateTransition.destinationState = states[states.Count - 1].state;
                templateTransition.conditions = new AnimatorCondition[0];
                templateTransition.AddCondition(AnimatorConditionMode.Equals, i + 1, "Inventory");
                templateTransition.AddCondition(AnimatorConditionMode.If, 0, "Inventory " + (i + 1));
                templateTransition.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");

                // Clone the transition and move on to next item in the inventory.
                anyTransitions.Add((AnimatorStateTransition)templateTransition.DeepClone(states[states.Count - 1]));

                // Move down a row.
                pos += new Vector3(0, 75);
            }

            // Buttons

            // Configure the template state.
            templateToggle.state.name = ("Pressing Buttons");
            templateToggle.state.behaviours = new StateMachineBehaviour[0];
            templateToggle.position = pos - new Vector3(25, 0);

            // Clone the template state.
            states.Add(templateToggle.DeepClone());

            // Clone and configure exit transitions.
            AnimatorStateTransition[] exitTransitions2 = new AnimatorStateTransition[] { (AnimatorStateTransition)toggleTransition.DeepClone() };
            states[states.Count - 1].state.transitions = exitTransitions2;

            // Configure the AnyState transition template.
            templateTransition.destinationState = states[states.Count - 1].state;
            templateTransition.conditions = new AnimatorCondition[0];
            templateTransition.canTransitionToSelf = true;
            templateTransition.AddCondition(AnimatorConditionMode.Greater, itemTotal, "Inventory");
            templateTransition.AddCondition(AnimatorConditionMode.Less, itemTotal + buttons.Count + 1, "Inventory");
            templateTransition.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");

            // Clone the transition.
            anyTransitions.Add((AnimatorStateTransition)templateTransition.DeepClone(states[states.Count - 1]));

            // Add group settings.
            for (int i = 0; i < buttons.Count; i++)
            {
                for (int j = 0; j < buttons[i].ButtonGroup.Length; j++)
                {
                    // Catch faulty data
                    if (buttons[i].ButtonGroup[j] != null && items.IndexOf(buttons[i].ButtonGroup[j].Item) == -1)
                    {
                        EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: " + buttons[i].name + " has a corrupt member in Slot " + (j + 1) + ". Please reassign the member to the desired Item to fix it. The program will now abort forcefully.", "Close");
                        Selection.activeObject = preset;
                        throw new Exception("Inventory Inventor: Corrupt member data found in " + buttons[i].name + "/Slot " + (j + 1) + ".");
                    }
                    // If the group item refers to an actual toggle.
                    if (buttons[i].ButtonGroup[j] != null && buttons[i].ButtonGroup[j].Item != null)
                        switch (buttons[i].ButtonGroup[j].Reaction)
                        {
                            // Add this button's value to the list of disabled states for the group item.
                            case GroupItem.GroupType.Disable:
                                activeStates[items.IndexOf(buttons[i].ButtonGroup[j].Item)].Key.Add(itemTotal + i + 1);
                                break;
                            // Add this button's value to the list of enabled states for the group item.
                            case GroupItem.GroupType.Enable:
                                activeStates[items.IndexOf(buttons[i].ButtonGroup[j].Item)].Value.Add(itemTotal + i + 1);
                                break;
                        }
                }
            }
            pos += new Vector3(0, 75);

            // Assign the states and transitions to the master layer.
            masterLayer.stateMachine.anyStatePosition = pos;
            masterLayer.stateMachine.states = states.ToArray();
            masterLayer.stateMachine.anyStateTransitions = anyTransitions.ToArray();
            masterLayer.stateMachine.defaultState = states[0].state;

            // Add the entry transitions if at least one object is synced.
            if (syncExists)
            {
                masterLayer.stateMachine.AddEntryTransition(states[1].state);
                masterLayer.stateMachine.AddEntryTransition(states[2].state);
                AnimatorTransition[] entryTransitions = masterLayer.stateMachine.entryTransitions;
                entryTransitions[0].AddCondition(AnimatorConditionMode.If, 0, "Inventory " + states[1].state.name.Substring(8));
                entryTransitions[1].AddCondition(AnimatorConditionMode.IfNot, 0, "Inventory " + states[1].state.name.Substring(8));
                masterLayer.stateMachine.entryTransitions = entryTransitions;
            }

            // Inventory machine identifier.
            AnimatorStateTransition identifier = masterLayer.stateMachine.AddAnyStateTransition(states[0].state);
            identifier.name = "InventoryMachineIdentifier";
            identifier.hasExitTime = false;
            identifier.isExit = false;
            identifier.mute = true;
            identifier.destinationState = null;
            identifier.destinationStateMachine = null;

            // Replace the layer in the Animator Controller.
            AnimatorControllerLayer[] layers = source.layers;
            layers[layers.Length - 1] = masterLayer;
            source.layers = layers;
            return;
        }

        // Generates two Animation Clips for when the Game Object is enabled or disabled.
        private int GenerateToggleClip(GameObject obj, bool state)
        {
            if (!AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Clips"))
                AssetDatabase.CreateFolder(outputPath, "Clips");
            string outFile = obj.name + (state ? "_On" : "_Off");

            // Create the Animation Clip
            AnimationClip clip = new AnimationClip();
            string path = Helper.GetGameObjectPath(obj.transform);
            path = path.Substring(path.IndexOf(avatar.transform.name) + avatar.transform.name.Length + 1);
            clip.SetCurve(path, typeof(GameObject), "m_IsActive", new AnimationCurve(new Keyframe[2] { new Keyframe() { value = state ? 1 : 0, time = 0 }, new Keyframe() { value = state ? 1 : 0, time = 0.016666668f } }));

            // Save the file
            bool existed = true;
            if (File.Exists(outputPath + Path.DirectorySeparatorChar + "Clips" + Path.DirectorySeparatorChar + outFile + ".anim"))
            {
                if (!autoOverwrite && !EditorUtility.DisplayDialog("Inventory Inventor", outFile + ".anim" + " already exists!\nOverwrite the file?", "Overwrite", "Cancel"))
                    return 1;
                backupManager.AddToBackup(new Asset(outputPath + Path.DirectorySeparatorChar + "Clips" + Path.DirectorySeparatorChar + outFile + ".anim"));
                AssetDatabase.DeleteAsset(outputPath + Path.DirectorySeparatorChar + "Clips" + Path.DirectorySeparatorChar + outFile + ".anim");
                AssetDatabase.Refresh();
            }
            else
            {
                existed = false;
            }

            AssetDatabase.CreateAsset(clip, outputPath + Path.DirectorySeparatorChar + "Clips" + Path.DirectorySeparatorChar + outFile + ".anim");
            AssetDatabase.Refresh();
            if (!existed)
                generated.Add(new Asset(AssetDatabase.GetAssetPath(clip)));

            return 0;
        }

        // Returns a list of layers and parameters that would be removed with the current settings.
        public void PreviewRemoval(out List<AnimatorControllerLayer> layers, out List<AnimatorControllerParameter> parameters, out List<bool> expression)
        {
            layers = new List<AnimatorControllerLayer>();
            parameters = new List<AnimatorControllerParameter>();
            expression = new List<bool>();

            if (controller == null)
                return;

            // Store Inventory Parameters.
            if (removeParameters)
            {
                Regex nameFilter = new Regex(@"^Inventory( [1-9]|$)([0-9]|$)([0-9]|$)$");

                for (int i = 0; i < controller.parameters.Length; i++)
                {
                    if (nameFilter.IsMatch(controller.parameters[i].name))
                    {
                        parameters.Add(controller.parameters[i]);
                        if (avatar != null && avatar.expressionParameters != null)
                        {
                            bool found = false;
                            foreach (VRCExpressionParameters.Parameter parameter in avatar.expressionParameters.parameters)
                                if (controller.parameters[i].name == parameter.name)
                                {
                                    expression.Add(true);
                                    found = true;
                                    break;
                                }
                            if (!found)
                                expression.Add(false);
                        }
                        else
                            expression.Add(false);
                    }
                }
            }

            // Store Inventory Layers.
            for (int i = 0; i < controller.layers.Length; i++)
            {
                bool hasSpecialTransition = false;
                foreach (AnimatorStateTransition transition in controller.layers[i].stateMachine.anyStateTransitions)
                {
                    if (transition.name == "InventoryMachineIdentifier" && transition.isExit == false && transition.mute == true && transition.destinationState == null && transition.destinationStateMachine == null)
                    {
                        hasSpecialTransition = true;
                        break;
                    }
                }

                if (!(!hasSpecialTransition && (controller.layers[i].stateMachine.behaviours.Length < 1 || controller.layers[i].stateMachine.behaviours[0].GetType() != typeof(InventoryMachine))))
                {
                    layers.Add(controller.layers[i]);
                }
            }

            return;
        }

        // Removes Inventory layers and parameters.
        public void RemoveInventory()
        {
            // try catch block because Exceptions exist.
            try
            {
                // Initial Save.
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Check that the selected avatar is valid.
                if (avatar == null)
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: No Avatar selected.", "Close");
                    return;
                }

                // An avatar is humanoid if the descriptor has the Gesture and Additive layers available.
                bool humanoid = avatar.baseAnimationLayers.Length == 5;

                EditorUtility.DisplayProgressBar("Inventory Inventor", "Removing...", 0);

                // Replace the Animator Controller in the descriptor if this Controller was there to begin with.
                bool replaceAnimator = humanoid ? (avatar.baseAnimationLayers[4].animatorController != null && controller == (AnimatorController)avatar.baseAnimationLayers[4].animatorController) : (avatar.baseAnimationLayers[2].animatorController != null && controller == (AnimatorController)avatar.baseAnimationLayers[2].animatorController);

                // Initialize backup objects.
                backupManager = new Backup();
                generated = new AssetList();

                // Backup the Animator before modifying it.
                backupManager.AddToBackup(new Asset(AssetDatabase.GetAssetPath(controller)));
                backupManager.AddToBackup(new Asset(AssetDatabase.GetAssetPath(avatar.expressionParameters)));

                // Duplicate the source controller.
                AssetDatabase.CopyAsset(new Asset(AssetDatabase.GetAssetPath(controller)).path, relativePath + Path.DirectorySeparatorChar + "temp.controller");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                AnimatorController animator = (AnimatorController)AssetDatabase.LoadAssetAtPath(relativePath + Path.DirectorySeparatorChar + "temp.controller", typeof(AnimatorController));
                generated.Add(new Asset(AssetDatabase.GetAssetPath(animator)));

                // Remove Inventory parameters if wanted.
                if (removeParameters)
                {
                    AnimatorControllerParameter[] parameters = animator.parameters;
                    Regex nameFilter = new Regex(@"^Inventory( [1-9]|$)([0-9]|$)([0-9]|$)$");

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        EditorUtility.DisplayProgressBar("Inventory Inventor", "Removing...", float.Parse(i.ToString()) / parameters.Length * 0.05f);
                        if (nameFilter.IsMatch(parameters[i].name))
                        {
                            animator.RemoveParameter(parameters[i]);
                        }
                    }
                    if (removeExpParams)
                    {
                        List<VRCExpressionParameters.Parameter> expParameters = new List<VRCExpressionParameters.Parameter>();
                        expParameters.AddRange(avatar.expressionParameters.parameters);

                        for (int i = expParameters.Count - 1; i >= 0; i--)
                        {
                            EditorUtility.DisplayProgressBar("Inventory Inventor", "Removing...", 0.05f + (float.Parse((expParameters.Count - i).ToString()) / expParameters.Count * 0.05f));
                            if (nameFilter.IsMatch(expParameters[i].name))
                            {
                                expParameters.RemoveAt(i);
                            }
                        }
                        avatar.expressionParameters.parameters = expParameters.ToArray();
                        EditorUtility.SetDirty(avatar.expressionParameters);
                    }
                }

                // Remove Inventory layers.
                for (int i = animator.layers.Length - 1; i >= 0; i--)
                {
                    EditorUtility.DisplayProgressBar("Inventory Inventor", "Removing...", 0.1f + float.Parse((controller.layers.Length - i).ToString()) / controller.layers.Length * 0.85f);

                    // A layer is an Inventory layer if the State Machine has a InventoryMachine behaviour attached.
                    // Or it has a "special transition"
                    bool hasSpecialTransition = false;
                    foreach (AnimatorStateTransition transition in controller.layers[i].stateMachine.anyStateTransitions)
                    {
                        if (transition.name == "InventoryMachineIdentifier" && transition.isExit == false && transition.mute == true && transition.destinationState == null && transition.destinationStateMachine == null)
                        {
                            hasSpecialTransition = true;
                            break;
                        }
                    }

                    if (hasSpecialTransition || (controller.layers[i].stateMachine.behaviours.Length >= 1 && controller.layers[i].stateMachine.behaviours[0].GetType() == typeof(InventoryMachine)))
                    {
                        animator.RemoveLayer(i);
                    }
                }
                animator.SaveController();

                // Replace the old Animator Controller.
                string path = AssetDatabase.GetAssetPath(controller);
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(animator), path);
                AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(animator), path.Substring(path.LastIndexOf(Path.DirectorySeparatorChar) + 1));
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Replace the Animator Controller in the descriptor if it was there.
                if (replaceAnimator)
                {
                    if (humanoid)
                        avatar.baseAnimationLayers[4].animatorController = animator;
                    else
                        avatar.baseAnimationLayers[2].animatorController = animator;
                }
                controller = animator;

                EditorUtility.DisplayProgressBar("Inventory Inventor", "Removing...", 1f);

                // Final Save.
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Inventory Inventor", "Success!", "Close");
            }
            catch (Exception err)
            {
                EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: An exception has occured!\nCheck the console for more details.", "Close");
                Debug.LogError(err);
                RevertChanges();
            }
        }

        // Reverts any changes made during the process in case of an error or exception.
        private void RevertChanges()
        {
            // Save Assets.
            AssetDatabase.SaveAssets();

            // Restore original data to pre-existing files.
            if (backupManager != null && !backupManager.RestoreAssets())
                Debug.LogError("[Inventory Inventor] Failed to revert all changes.");

            // Delete any generated assets that didn't overwrite files.
            for (int i = 0; generated != null && i < generated.ToArray().Length; i++)
                if (File.Exists(generated[i].path) && !AssetDatabase.DeleteAsset(generated[i].path))
                    Debug.LogError("[Inventory Inventor] Failed to revert all changes.");

            // Save assets so folders will be seen as empty.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Delete created folders if now empty.
            if (AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Animators") && AssetDatabase.FindAssets("", new string[] { outputPath + Path.DirectorySeparatorChar + "Animators" }).Length == 0)
                if (!AssetDatabase.DeleteAsset(outputPath + Path.DirectorySeparatorChar + "Animators"))
                    Debug.LogError("[Inventory Inventor] Failed to revert all changes.");
            if (AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Menus") && AssetDatabase.FindAssets("", new string[] { outputPath + Path.DirectorySeparatorChar + "Menus" }).Length == 0)
                if (!AssetDatabase.DeleteAsset(outputPath + Path.DirectorySeparatorChar + "Menus"))
                    Debug.LogError("[Inventory Inventor] Failed to revert all changes.");
            if (AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Clips") && AssetDatabase.FindAssets("", new string[] { outputPath + Path.DirectorySeparatorChar + "Clips" }).Length == 0)
                if (!AssetDatabase.DeleteAsset(outputPath + Path.DirectorySeparatorChar + "Clips"))
                    Debug.LogError("[Inventory Inventor] Failed to revert all changes.");

            // Final asset save.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Updates output and relative paths if the directory of this package changes.
        public void UpdatePaths()
        {
            // Get the relative path.
            string filter = "InventoryInventor";
            string[] guids = AssetDatabase.FindAssets(filter);
            foreach (string guid in guids)
            {
                string tempPath = AssetDatabase.GUIDToAssetPath(guid);
                if (tempPath.LastIndexOf(filter) == tempPath.Length - filter.Length - 3)
                {
                    relativePath = tempPath.Substring(0, tempPath.LastIndexOf("Editor") - 1);
                    break;
                }
            }
        }
    }
}

