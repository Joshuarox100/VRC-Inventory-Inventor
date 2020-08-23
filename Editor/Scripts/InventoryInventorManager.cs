using BMBLibraries.Classes;
using BMBLibraries.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Networking;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

public class InventoryInventorManager : UnityEngine.Object
{
    //Objects to modify
    public VRCAvatarDescriptor avatar;
    public VRCExpressionsMenu menu;
    public AnimatorController controller;

    //Input objects
    public AnimationClip[] toggleables = new AnimationClip[0];
    public string[] aliases = new string[0];
    public int[] pageLength = new int[0];
    public string[] pageNames = new string[0];
    public int syncMode = 1;
    public float refreshRate = 0.05f;

    //Path related
    public string relativePath;
    public string outputPath;
    public bool autoOverwrite = false;    

    //File backup
    private Backup backupManager;
    private AssetList generated;

    public InventoryInventorManager() { }

    public void CreateInventory()
    {
        try
        {
            //Initial Save
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            //Check that the selected avatar is valid
            if (avatar == null)
            {
                EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: No Avatar selected.", "Close");
                return;
            }
            else if (avatar.baseAnimationLayers.Length != 5)
            {
                EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Avatar is not humanoid.\n(Non-Humanoid avatars are not supported yet)", "Close");
                Selection.activeObject = avatar.gameObject;
                return;
            }
            else if (avatar.expressionParameters == null)
            {
                EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Avatar does not have an Expression Parameters object assigned in the descriptor.", "Close");
                Selection.activeObject = avatar;
                return;
            }

            /*
                Check for space in parameters list & check for incompatible Animations.
            */

            int paramCount = 0;
            int present = 0;
            //foreach parameter, check if it's one to be added and if it is the correct type.
            foreach (VRCExpressionParameters.Parameter param in avatar.expressionParameters.parameters)
            {
                if (present == 1)
                {
                    break;
                }
                switch (param.name) 
                {
                    case "Inventory":
                        if (param.valueType == VRCExpressionParameters.ValueType.Int)
                        {
                            present++;
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Expression Parameter \"" + param.name + "\" is present with the wrong type.", "Close");
                            Selection.activeObject = avatar.expressionParameters;
                            return;
                        }
                        break;
                    default:
                        if (param.name != "")
                            paramCount++;
                        break;
                }
            }

            if (16 - paramCount < 1 - present)
            {
                EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: No unused Expression Parameters found.", "Close");
                Selection.activeObject = avatar.expressionParameters;
                return;
            }

            //Check that no animations modify a rig or Transform
            foreach (AnimationClip clip in toggleables)
            {
                if (!CheckCompatibility(clip, false, out Type problem, out string propertyName))
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: " + clip.name + " cannot be used because it modifies an invalid property type!\n\nInvalid Property Type: " + problem.Name + "\nName: " + propertyName, "Close");
                    Selection.activeObject = clip;
                    return;
                }
            }

            //Check that the file destination exists
            VerifyDestination();

            EditorUtility.DisplayProgressBar("Inventory Inventor", "Starting", 0);

            //Initialize backup objects
            backupManager = new Backup();
            generated = new AssetList();

            /*
                Get FX Animator
             */

            AnimatorController animator = (avatar.baseAnimationLayers[4].animatorController != null) ? (AnimatorController)avatar.baseAnimationLayers[4].animatorController : null;
            bool replaceAnimator = animator != null;
            animator = controller != null ? controller : animator;
            
            //Create new Animator from SDK template if none provided.
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

                animator = (AssetDatabase.FindAssets(avatar.name + "_FX", new string[] { outputPath + Path.DirectorySeparatorChar + "Animators" }).Length != 0) ? (AnimatorController)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets(avatar.name + "_FX", new string[] { outputPath + Path.DirectorySeparatorChar + "Animators" })[0]), typeof(AnimatorController)) : null;

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

            //Create fresh and clean Animator object
            AnimatorController newAnimator = new AnimatorController
            {
                name = animator.name,
                parameters = animator.parameters,
                hideFlags = animator.hideFlags
            };
            AssetDatabase.CreateAsset(newAnimator, relativePath + Path.DirectorySeparatorChar + "temp.controller");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            generated.Add(new Asset(AssetDatabase.GetAssetPath(newAnimator)));

            //Clone provided Animator into the new object, without any Inventory layers or parameters.
            for (int i = 0; i < animator.layers.Length; i++)
            {
                bool invLayer = false;
                for (int j = 0; j < 12; j++)
                {
                    if (animator.layers[i].stateMachine.name == "Inventory " + (j + 1))
                    {
                        invLayer = true;
                        break;
                    }
                }
                if (!invLayer && animator.layers[i].stateMachine.name != "Inventory Master")
                {
                    EditorUtility.DisplayProgressBar("Inventory Inventor", string.Format("Cloning Layers: {0}", animator.layers[i].name), 0.05f * (float.Parse(i.ToString()) / animator.layers.Length));
                    newAnimator.AddLayer(animator.layers[i].name);
                    AnimatorControllerLayer[] layers = newAnimator.layers;
                    AnimatorControllerLayer layer = layers[layers.Length - 1];
                    layer = animator.layers[i].DeepClone();
                    layers[layers.Length - 1] = layer;
                    newAnimator.layers = layers;
                    EditorUtility.DisplayProgressBar("Inventory Inventor", string.Format("Cloning Layers: {0}", animator.layers[i].name), 0.05f * ((i + 1f) / animator.layers.Length));
                }
            }                    
            newAnimator.SaveController();
            string path = AssetDatabase.GetAssetPath(animator);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(newAnimator), path);
            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(newAnimator), path.Substring(path.LastIndexOf(Path.DirectorySeparatorChar) + 1));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (replaceAnimator)
                avatar.baseAnimationLayers[4].animatorController = newAnimator;

            /*
                Create parameters
            */

            EditorUtility.DisplayProgressBar("Inventory Inventor", "Creating Parameters", 0.05f);

            //Adds needed parameters to the Animator If one already exists as the wrong type, abort.
            //toggle, state, and one bool for each anim.
            AnimatorControllerParameter[] srcParam = newAnimator.parameters;
            bool[] existing = new bool[toggleables.Length + 2];
            for (int i = 0; i < srcParam.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Inventory Inventor", "Creating Parameters", 0.05f + (0.025f * (float.Parse(i.ToString()) / srcParam.Length)));
                bool flag = true;
                foreach (bool exists in existing)
                {
                    if (!exists)
                    {
                        flag = false;
                    }
                }
                if (flag)
                {
                    break;
                }
                for (int j = 0; j < toggleables.Length; j++)
                {
                    if (srcParam[i].name == "Inventory " + (j + 1))
                    {
                        if (srcParam[i].type == AnimatorControllerParameterType.Bool)
                        {
                            existing[j] = true;
                            break;
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Animator Parameter \"" + srcParam[i].name + "\" already exists as the incorrect type.", "Close");
                            RevertChanges();
                            Selection.activeObject = animator;
                            return;
                        }
                    }
                    else if (srcParam[i].name == "Inventory")
                    {
                        if (srcParam[i].type == AnimatorControllerParameterType.Int)
                        {
                            existing[existing.Length - 2] = true;
                            break;
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Animator Parameter \"" + srcParam[i].name + "\" already exists as the incorrect type.", "Close");
                            RevertChanges();
                            Selection.activeObject = animator;
                            return;
                        }
                    }
                    else if (srcParam[i].name == "IsLocal")
                    {
                        if (srcParam[i].type == AnimatorControllerParameterType.Bool)
                        {
                            existing[existing.Length - 1] = true;
                            break;
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: Animator Parameter \"" + srcParam[i].name + "\" already exists as the incorrect type.", "Close");
                            RevertChanges();
                            Selection.activeObject = animator;
                            return;
                        }
                    }
                }
            }

            for (int i = 0; i < existing.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Inventory Inventor", "Creating Parameters", 0.075f + (0.025f * (float.Parse(i.ToString()) / existing.Length)));
                if (i < existing.Length - 2)
                {
                    if (!existing[i])
                    {
                        newAnimator.AddParameter("Inventory " + (i + 1), AnimatorControllerParameterType.Bool);
                    }
                }
                else if (i == existing.Length - 2)
                {
                    if (!existing[i])
                    {
                        newAnimator.AddParameter("Inventory", AnimatorControllerParameterType.Int);
                    }
                }
                else if (i == existing.Length - 1)
                {
                    if (!existing[i])
                    {
                        newAnimator.AddParameter("IsLocal", AnimatorControllerParameterType.Bool);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayProgressBar("Inventory Inventor", "Creating Parameters", 0.1f);

            /*
                Create layers
            */

            CreateMasterLayer(newAnimator, toggleables.Length, out List<int[]> activeStates);
            CreateItemLayers(newAnimator, ref activeStates);

            EditorUtility.DisplayProgressBar("Inventory Inventor", "Saving Controller", 0.9f);
            newAnimator.SaveController();
            AssetDatabase.SaveAssets();

            /*
                Add expression parameters to the list.
            */

            EditorUtility.DisplayProgressBar("Inventory Inventor", "Finalizing", 0.9f);
            foreach (VRCExpressionParameters.Parameter param in avatar.expressionParameters.parameters)
            {
                if (present == 1)
                {
                    break;
                }
                else if (param.name == "")
                {
                    param.name = "Inventory";
                    param.valueType = VRCExpressionParameters.ValueType.Int;
                    break;
                }
            }
            EditorUtility.DisplayProgressBar("Inventory Inventor", "Finalizing", 0.95f);

            /*
                Create Expressions menu for toggles.
            */

            
            switch (CreateMenus(out VRCExpressionsMenu inventory))
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

            //Assign inventory menu to given menu if possible.
            if (menu != null && menu.controls.ToArray().Length < 8)
            {
                bool exists = false;
                foreach (VRCExpressionsMenu.Control control in menu.controls)
                {
                    if (control.name == "Inventory" && control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu == null)
                    {
                        exists = true;
                        control.subMenu = inventory;
                        break;
                    }
                }
                if (!exists)
                {
                    menu.controls.Add(new VRCExpressionsMenu.Control() { name = "Inventory", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = inventory });
                }
            }
            
            EditorUtility.DisplayProgressBar("Inventory Inventor", "Finalizing", 1f);

            /*
                6. Save configuration
             */

            AssetDatabase.SaveAssets();
            Selection.activeObject = menu != null ? menu : inventory;
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

    //Checks if an AnimationClip contains invalid bindings.
    private bool CheckCompatibility(AnimationClip clip, bool transformsOnly, out Type problem, out string name)
    {
        if (clip != null)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if ((transformsOnly && binding.type != typeof(Transform) && binding.type != typeof(Animator)) || (!transformsOnly && (binding.type == typeof(Transform) || binding.type == typeof(Animator))))
                {
                    problem = binding.type;
                    name = binding.propertyName;
                    return false;
                }
            }
        }

        problem = null;
        name = "";
        return true;
    }

    //Checks if the destination is valid.
    private void VerifyDestination()
    {
        if (!AssetDatabase.IsValidFolder(outputPath))
        {
            if (!AssetDatabase.IsValidFolder(relativePath + Path.DirectorySeparatorChar + "Output"))
            {
                string guid = AssetDatabase.CreateFolder(relativePath, "Output");
                outputPath = AssetDatabase.GUIDToAssetPath(guid);
            }
            else
            {
                outputPath = relativePath + Path.DirectorySeparatorChar + "Output";
            }
        }
    }

    //Copies an Animator from the VRCSDK to the given location.
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

    //Creates all the menus needed for the generated inventory.
    private int CreateMenus(out VRCExpressionsMenu mainMenu)
    {
        mainMenu = null;
        
        //Create a main menu
        VRCExpressionsMenu inventory = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
        inventory.name = avatar.name + "_Inventory";

        //If there's a single page, put all the controls on the top level
        if (pageLength.Length == 1)
        {
            //For each item in the page, add it to the menu
            for (int i = 0; i < toggleables.Length; i++)
            {
                inventory.controls.Add(new VRCExpressionsMenu.Control() { name = aliases[i], type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter() { name = "Inventory" }, value = i + 1 });
            }
            
            //Create output folder if not present
            if (!AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Menus"))
                AssetDatabase.CreateFolder(outputPath, "Menus");

            //Create or overwrite the menu asset
            bool existed = true;
            if (File.Exists(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + inventory.name + ".asset"))
            {
                if (!autoOverwrite)
                {
                    switch (EditorUtility.DisplayDialogComplex("Inventory Inventor", inventory.name + ".asset" + " already exists!\nOverwrite the file?", "Overwrite", "Cancel", "Skip"))
                    {
                        case 1:
                            return 1;
                        case 2:
                            return 2;
                    }
                }
                backupManager.AddToBackup(new Asset(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + inventory.name + ".asset"));
                AssetDatabase.DeleteAsset(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + inventory.name + ".asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            else
            {
                existed = false;
            }
            AssetDatabase.CreateAsset(inventory, outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + inventory.name + ".asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            //Check that it was created successfully
            if (AssetDatabase.FindAssets(inventory.name, new string[] { outputPath + Path.DirectorySeparatorChar + "Menus" }).Length == 0)
            {
                return 3;
            }
            else
            {
                AssetDatabase.Refresh();
                if (!existed)
                    generated.Add(new Asset(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + inventory.name + ".asset"));
            }

            //out the main menu
            mainMenu = inventory;
            return 0;
        }
        //If there is more than one page...
        else
        {
            //Create a list of menu objects and instantiate a new one for each page
            List<VRCExpressionsMenu> pages = new List<VRCExpressionsMenu>();
            int index = 0;
            for (int i = 0; i < pageLength.Length; i++)
            {
                //Add controls for the items contained in each page
                pages.Add(ScriptableObject.CreateInstance<VRCExpressionsMenu>());
                pages[i].name = pageNames[i];
                inventory.controls.Add(new VRCExpressionsMenu.Control() { name = pages[i].name, type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = pages[i] });
                for (int j = 0; j < pageLength[i]; j++)
                {
                    pages[i].controls.Add(new VRCExpressionsMenu.Control() { name = aliases[index], type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter() { name = "Inventory" }, value = index + 1 });
                    index++;
                }
            }           

            //Create output directory if not present
            if (!AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Menus"))
                AssetDatabase.CreateFolder(outputPath, "Menus");

            //Create / overwrite each menu asset to the directory
            foreach (VRCExpressionsMenu page in pages)
            {
                bool exists = true;
                if (File.Exists(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + avatar.name + "_" + page.name + ".asset"))
                {
                    if (!autoOverwrite)
                    {
                        switch (EditorUtility.DisplayDialogComplex("Inventory Inventor", avatar.name + "_" + page.name + ".asset" + " already exists!\nOverwrite the file?", "Overwrite", "Cancel", "Skip"))
                        {
                            case 1:
                                return 1;
                            case 2:
                                return 2;
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
                
                //Check that the asset was saved successfully
                if (AssetDatabase.FindAssets(page.name, new string[] { outputPath + Path.DirectorySeparatorChar + "Menus" }).Length == 0)
                {
                    return 3;
                }
                else
                {
                    AssetDatabase.Refresh();
                    if (!exists)
                        generated.Add(new Asset(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + page.name + ".asset"));
                }
            }

            //Create / overwrite the main menu asset
            bool existed = true;
            if (File.Exists(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + inventory.name + ".asset"))
            {
                if (!autoOverwrite)
                {
                    switch (EditorUtility.DisplayDialogComplex("Inventory Inventor", inventory.name + ".asset" + " already exists!\nOverwrite the file?", "Overwrite", "Cancel", "Skip"))
                    {
                        case 1:
                            return 1;
                        case 2:
                            return 2;
                    }
                }
                backupManager.AddToBackup(new Asset(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + inventory.name + ".asset"));
                AssetDatabase.DeleteAsset(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + inventory.name + ".asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            else
            {
                existed = false;
            }
            AssetDatabase.CreateAsset(inventory, outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + inventory.name + ".asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            //Check that the asset was saved successfully
            if (AssetDatabase.FindAssets(inventory.name, new string[] { outputPath + Path.DirectorySeparatorChar + "Menus" }).Length == 0)
            {
                return 3;
            }
            else
            {
                AssetDatabase.Refresh();
                if (!existed)
                    generated.Add(new Asset(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + inventory.name + ".asset"));
            }

            //out the main menu
            mainMenu = inventory;
            return 0;
        }
    }

    //Creates layers for each item in the inventory (ordered by page)
    private void CreateItemLayers(AnimatorController source, ref List<int[]> activeStates)
    {
        //Create a template machine to duplicate
        AnimatorStateMachine templateMachine = new AnimatorStateMachine();
        ChildAnimatorState[] states = new ChildAnimatorState[templateMachine.states.Length + 2];
        templateMachine.states.CopyTo(states, 2);

        //Create a template state to duplicate
        ChildAnimatorState templateState = new ChildAnimatorState
        {
            state = new AnimatorState
            {
                name = "",
                motion = null,
            }
        };

        //Get a starting position for the states
        Vector3 pos = templateMachine.anyStatePosition;

        //Create an off state
        ChangeState(templateState, "Off");
        templateState.position = pos - new Vector3(150, -45);
        states[0] = templateState.DeepClone();

        //Create an on state
        templateState.position = pos + new Vector3(100, 45);
        ChangeState(templateState, "On");
        states[1] = templateState.DeepClone();

        //Add the states to the template machine
        templateMachine.states = states;

        //Create a template transition
        AnimatorStateTransition templateTransition = new AnimatorStateTransition
        {
            destinationState = null,
            isExit = false,
            hasExitTime = false,
            duration = 0,
            canTransitionToSelf = false,
            conditions = null
        };

        //For each item in the inventory...
        for (int i = 0; i < toggleables.Length; i++)
        {
            EditorUtility.DisplayProgressBar("Inventory Inventor", string.Format(CultureInfo.InvariantCulture, "Creating Item Layers: {0} ({1:#0.##%})", aliases[i], (i + 1f) / toggleables.Length), 0.55f + (0.35f * (float.Parse(i.ToString()) / toggleables.Length)));
            int[] active = activeStates[i];

            //Create a layer
            source.AddLayer(aliases[i]);
            AnimatorControllerLayer[] layers = source.layers;
            AnimatorControllerLayer currentLayer = layers[layers.Length - 1];
            currentLayer.defaultWeight = 1;

            //Create an AnyState transition to the on and off state with their assigned conditionals
            AnimatorStateTransition[] transitions = new AnimatorStateTransition[2];
            ChangeTransition(templateTransition, ref active[0], templateMachine.states[0].state);
            transitions[0] = (AnimatorStateTransition)templateTransition.DeepClone(templateMachine.states[0]);
            ChangeState(templateMachine.states[1].state, toggleables[i]);
            ChangeTransition(templateTransition, ref active[1], templateMachine.states[1].state);
            transitions[1] = (AnimatorStateTransition)templateTransition.DeepClone(templateMachine.states[1]);
            templateMachine.anyStateTransitions = transitions;

            //Name the machine for detection later and clone it
            templateMachine.name = "Inventory " + (i + 1);
            currentLayer.stateMachine = templateMachine.DeepClone();
            layers[layers.Length - 1] = currentLayer;
            source.layers = layers;
        }
        return;
    }

    //Creates the master layer that handles menu inputs and the idle sync
    private void CreateMasterLayer(AnimatorController source, int itemTotal, out List<int[]> activeStates)
    {
        EditorUtility.DisplayProgressBar("Inventory Inventor", "Creating Master Layer: Preparing", 0.1f);

        for (int i = 0; i < source.layers.Length; i++)
        {
            //Remove layer if already present
            if (source.layers[i].name == "Inventory Master")
            {
                source.RemoveLayer(i);
                break;
            }
        }
        //Add Master Layer
        source.AddLayer("Inventory Master");
        AnimatorControllerLayer masterLayer = source.layers[source.layers.Length - 1];

        //Create List of state values
        activeStates = new List<int[]>();
        int value = itemTotal + 1;
        for (int i = 0; i < itemTotal; i++)
        {
            activeStates.Add(new int[] { value, value + 1 });
            value += 2;
        } 

        //Create an array states to be created
        ChildAnimatorState[] states = new ChildAnimatorState[masterLayer.stateMachine.states.Length + (itemTotal * 4) + 1];
        masterLayer.stateMachine.states.CopyTo(states, itemTotal * 4 + 1);

        //Store a starting position for the states
        Vector3 pos = masterLayer.stateMachine.entryPosition;

        //Create the starting state
        states[itemTotal * 4] = new ChildAnimatorState
        {
            position = pos + new Vector3(-25, 50),
            state = new AnimatorState
            {
                name = "Remote Clients",
            }
        };

        //Create a template state for cloning
        ChildAnimatorState templateState = new ChildAnimatorState
        {
            state = new AnimatorState
            {
                name = "",
                behaviours = new StateMachineBehaviour[] { ScriptableObject.CreateInstance<VRCAvatarParameterDriver>() }
            }
        };
        ((VRCAvatarParameterDriver)templateState.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = 0 });

        //Move down a row in the grid
        pos += new Vector3(0, 125);

        //Create a template transition
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

        //Start with the 3rd state
        int index = 2;
        
        //Only create the sync loop if Auto Sync is enabled
        if (syncMode == 1)
        {
            //Create the first pair of syncing states
            ChangeState(templateState, "Syncing " + 1, activeStates[0], true);
            templateState.position = pos - new Vector3(150, 0);
            states[0] = templateState.DeepClone();
            ChangeState(templateState, "Syncing " + 1 + " ", activeStates[0], false);
            templateState.position = pos + new Vector3(100, 0);
            states[1] = templateState.DeepClone();

            //Move down a row
            pos += new Vector3(0, 75);

            //foreach entry in the state array from 2 to 1 less then itemTotal doubled...
            for (int i = 2; i < itemTotal * 2 && syncMode == 1; i++)
            {
                EditorUtility.DisplayProgressBar("Inventory Inventor", string.Format(CultureInfo.InvariantCulture, "Creating Master Layer: Creating States ({0:#0.##%})", (i + 1f) / (itemTotal * 4)), 0.1f + (0.225f * ((i + 1f) / (itemTotal * 4))));

                //If 'i' is even, create an on sync state, otherwise make an off sync state
                switch (i % 2 == 0)
                {
                    case true:
                        ChangeState(templateState, "Syncing " + index, activeStates[index - 1], true);
                        templateState.position = pos - new Vector3(150, 0);
                        states[i] = templateState.DeepClone();

                        //Create transitions to this state from the previous pair
                        ChangeTransition(templateTransition, states[i], index, true);
                        states[i - 2].state.AddTransition((AnimatorStateTransition)AnimatorExtensions.DeepClone(templateTransition, states[i]));
                        ChangeTransition(templateTransition, states[i], index, true);
                        states[i - 1].state.AddTransition((AnimatorStateTransition)AnimatorExtensions.DeepClone(templateTransition, states[i]));
                        break;
                    case false:
                        ChangeState(templateState, "Syncing " + index + " ", activeStates[index - 1], false);
                        templateState.position = pos + new Vector3(100, 0);
                        states[i] = templateState.DeepClone();

                        //Create transitions to this state from the previous pair
                        ChangeTransition(templateTransition, states[i], index, false);
                        states[i - 2].state.AddTransition((AnimatorStateTransition)AnimatorExtensions.DeepClone(templateTransition, states[i]));
                        ChangeTransition(templateTransition, states[i], index, false);
                        states[i - 3].state.AddTransition((AnimatorStateTransition)AnimatorExtensions.DeepClone(templateTransition, states[i]));

                        //Move on to the next item in the inventory
                        index++;

                        //Move down a row
                        pos += new Vector3(0, 75);
                        break;
                }
            }
            //Final Transitions
            states[(itemTotal * 2) - 1].state.AddExitTransition();
            states[(itemTotal * 2) - 1].state.transitions[0].AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            states[(itemTotal * 2) - 1].state.transitions[0].hasExitTime = true;
            states[(itemTotal * 2) - 1].state.transitions[0].exitTime = refreshRate;
            states[(itemTotal * 2) - 1].state.transitions[0].duration = 0;
            states[(itemTotal * 2) - 2].state.AddExitTransition();
            states[(itemTotal * 2) - 2].state.transitions[0].AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            states[(itemTotal * 2) - 2].state.transitions[0].hasExitTime = true;
            states[(itemTotal * 2) - 2].state.transitions[0].exitTime = refreshRate;
            states[(itemTotal * 2) - 2].state.transitions[0].duration = 0;
        }    
        //First transition to trap remote clients (or acts as an idle state when Auto Sync is disabled)
        states[itemTotal * 4].state.AddExitTransition();
        states[itemTotal * 4].state.transitions[0].AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
        states[itemTotal * 4].state.transitions[0].hasExitTime = false;
        states[itemTotal * 4].state.transitions[0].duration = 0;
        masterLayer.stateMachine.exitPosition = pos;

        //Create a template toggle state
        ChildAnimatorState templateToggle = new ChildAnimatorState
        {
            state = new AnimatorState { behaviours = new StateMachineBehaviour[] { ScriptableObject.CreateInstance<VRCAvatarParameterDriver>() } }
        };
        ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = 0 });

        //Create a template toggle transition
        AnimatorStateTransition toggleTransition = new AnimatorStateTransition
        {
            isExit = true,
            exitTime = 1f,
            hasExitTime = true,
            duration = 0f,
            canTransitionToSelf = false
        };

        //Reset or adjust some existing values
        templateTransition.hasExitTime = false;
        index = itemTotal * 2;
        pos += new Vector3(0, 60);

        //Create an array of AnyState transitions
        AnimatorStateTransition[] anyTransitions = new AnimatorStateTransition[itemTotal * 2];

        //For each item in the inventory... (Loops in reverse to make UI nicer)
        for (int i = itemTotal - 1; i >= 0; i--)
        {
            EditorUtility.DisplayProgressBar("Inventory Inventor", string.Format(CultureInfo.InvariantCulture, "Creating Master Layer: Creating States ({0:#0.##%})", index / (itemTotal * 4)), 0.1f + (0.225f * (index / (itemTotal * 4f))));

            //Create an On state
            templateToggle.state.name = ("Toggling " + (i + 1) + ": On");
            templateToggle.position = pos - new Vector3(150, 0);

            //Adjust parameter settings
            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters[0].value = activeStates[i][1];
            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory " + (i + 1), value = 1 });

            //Clone the template state
            states[index] = templateToggle.DeepClone();

            //Remove additional parameter in the driver for the next state
            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.RemoveAt(1);

            //Clone an exit transition
            states[index].state.transitions = new AnimatorStateTransition[] { (AnimatorStateTransition)toggleTransition.DeepClone() };

            //Configure the AnyState transition template
            templateTransition.destinationState = states[index].state;
            templateTransition.conditions = new AnimatorCondition[0];
            templateTransition.AddCondition(AnimatorConditionMode.Equals, i + 1, "Inventory");
            templateTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "Inventory " + (i + 1));
            templateTransition.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");

            //Clone the transition and move on to the Off state
            anyTransitions[index - (itemTotal * 2)] = (AnimatorStateTransition)templateTransition.DeepClone(states[index]);
            index++;

            //Create an Off state
            templateToggle.state.name = ("Toggling " + (i + 1) + ": Off");
            templateToggle.position = pos + new Vector3(100, 0);

            //Adjust parameter settings
            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters[0].value = activeStates[i][0];
            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory " + (i + 1), value = 0 });

            //Clone the template state
            states[index] = templateToggle.DeepClone();

            //Remove additional parameter in the driver for the next state
            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.RemoveAt(1);

            //Clone an exit transition
            states[index].state.transitions = new AnimatorStateTransition[] { (AnimatorStateTransition)toggleTransition.DeepClone() };

            //Configure the AnyState transition template
            templateTransition.destinationState = states[index].state;
            templateTransition.conditions = new AnimatorCondition[0];
            templateTransition.AddCondition(AnimatorConditionMode.Equals, i + 1, "Inventory");
            templateTransition.AddCondition(AnimatorConditionMode.If, 0, "Inventory " + (i + 1));
            templateTransition.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");

            //Clone the transition and move on to next item in the inventory
            anyTransitions[index - (itemTotal * 2)] = (AnimatorStateTransition)templateTransition.DeepClone(states[index]);
            index++;

            //Move down a row
            pos += new Vector3(0, 75);
        }

        //Assign the states and transitions to the master layer
        masterLayer.stateMachine.anyStatePosition = pos;
        masterLayer.stateMachine.states = states;
        masterLayer.stateMachine.anyStateTransitions = anyTransitions;
        masterLayer.stateMachine.defaultState = states[itemTotal * 4].state;

        //Add the entry transitions if Auto Sync is enabled
        if (syncMode == 1)
        {
            masterLayer.stateMachine.AddEntryTransition(states[0].state);
            masterLayer.stateMachine.AddEntryTransition(states[1].state);
            AnimatorTransition[] entryTransitions = masterLayer.stateMachine.entryTransitions;
            entryTransitions[0].AddCondition(AnimatorConditionMode.If, 0, "Inventory 1");
            entryTransitions[1].AddCondition(AnimatorConditionMode.IfNot, 0, "Inventory 1");
            masterLayer.stateMachine.entryTransitions = entryTransitions;
        }

        //Replace the layer in the Animator
        AnimatorControllerLayer[] layers = source.layers;
        layers[layers.Length - 1] = masterLayer;
        source.layers = layers;
        return;
    }

    //Helper method for modifying transitions
    public static void ChangeTransition(AnimatorStateTransition transition, ref int value, AnimatorState state)
    {
        transition.destinationState = state;
        transition.conditions = new AnimatorCondition[0];
        transition.AddCondition(AnimatorConditionMode.Equals, value, "Inventory");
    }

    //Helper method for modifying transitions
    public static void ChangeTransition(AnimatorStateTransition transition, ChildAnimatorState childState, int name, bool value)
    {
        transition.destinationState = childState.state;
        transition.conditions = new AnimatorCondition[0];
        switch (value)
        {
            case true:
                transition.AddCondition(AnimatorConditionMode.If, 0, "Inventory " + name);
                break;
            case false:
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Inventory " + name);
                break;
        }      
    }

    //Helper method for modifying states
    public static void ChangeState(ChildAnimatorState childState, string name, int[] value, bool state)
    {
        switch (state)
        {
            case true:
                ChangeState(childState.state, name, value[1]);
                break;
            case false:
                ChangeState(childState.state, name, value[0]);
                break;
        }
        return;
    }

    //Helper method for modifying states
    public static void ChangeState(AnimatorState state, string name, int value)
    {
        state.name = name;
        ((VRCAvatarParameterDriver)state.behaviours[0]).parameters[0].value = value;
        return;
    }

    //Helper method for modifying states
    public static void ChangeState(AnimatorState state, string name)
    {
        state.name = name;
        return;
    }

    //Helper method for modifying states
    public static void ChangeState(ChildAnimatorState childState, string name)
    {
        ChangeState(childState.state, name);
        return;
    }

    //Helper method for modifying states
    public static void ChangeState(AnimatorState state, Motion motion)
    {
        state.motion = motion;
        return;
    }

    //Reverts any changes made during the process in case of an error or exception
    private void RevertChanges()
    {
        //Save Assets
        AssetDatabase.SaveAssets();

        //Restore original data to pre-existing files
        if (backupManager != null && !backupManager.RestoreAssets())
            Debug.LogError("[Inventory Inventor] Failed to revert all changes.");

        //Delete any generated assets that didn't overwrite files
        for (int i = 0; generated != null && i < generated.ToArray().Length; i++)
            if (File.Exists(generated[i].path) && !AssetDatabase.DeleteAsset(generated[i].path))
                Debug.LogError("[Inventory Inventor] Failed to revert all changes.");

        //Save assets so folders will be seen as empty
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        //Delete created folders if now empty
        if (AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Animators") && AssetDatabase.FindAssets("", new string[] { outputPath + Path.DirectorySeparatorChar + "Animators" }).Length == 0)
            if (!AssetDatabase.DeleteAsset(outputPath + Path.DirectorySeparatorChar + "Animators"))
                Debug.LogError("[Inventory Inventor] Failed to revert all changes.");
        if (AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Menus") && AssetDatabase.FindAssets("", new string[] { outputPath + Path.DirectorySeparatorChar + "Menus" }).Length == 0)
            if (!AssetDatabase.DeleteAsset(outputPath + Path.DirectorySeparatorChar + "Menus"))
                Debug.LogError("[Inventory Inventor] Failed to revert all changes.");

        //Final asset save
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    //Updates output and relative paths if the directory of this package changes
    public void UpdatePaths()
    {
        string old = relativePath;
        relativePath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("InventoryInventor")[0]).Substring(0, AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("InventoryInventor")[0]).LastIndexOf("Editor") - 1);
        if (relativePath == old)
            return;
        else if (outputPath == null || !AssetDatabase.IsValidFolder(outputPath))
        {
            outputPath = relativePath + Path.DirectorySeparatorChar + "Output";
        }
    }

    //Blank MonoBehaviour for running network coroutines
    private class NetworkManager : MonoBehaviour { }

    //Compares the VERSION file present to the one on GitHub to see if a newer version is available
    public static void CheckForUpdates()
    {
        //Static, so path must be reobtained
        string relativePath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("InventoryInventor")[0]).Substring(0, AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("InventoryInventor")[0]).LastIndexOf("Editor") - 1);
        
        //Read VERSION file
        string installedVersion = (AssetDatabase.FindAssets("VERSION", new string[] { relativePath }).Length > 0) ? File.ReadAllText(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("VERSION", new string[] { relativePath })[0])) : "";

        //Create hidden object to run the coroutine.
        GameObject netMan = new GameObject { hideFlags = HideFlags.HideInHierarchy };

        //Run a coroutine to retrieve the GitHub data.
        netMan.AddComponent<NetworkManager>().StartCoroutine(GetText("https://raw.githubusercontent.com/Joshuarox100/VRC-Inventory-Inventor/master/Editor/VERSION", latestVersion => {
            //Network Error
            if (latestVersion == "")
            {
                EditorUtility.DisplayDialog("Inventory Inventor", "Failed to fetch the latest version.\n(Check console for details.)", "Close");
            }
            //VERSION file missing
            else if (installedVersion == "")
            {
                EditorUtility.DisplayDialog("Inventory Inventor", "Failed to identify installed version.\n(VERSION file was not found.)", "Close");
            }
            //Project has been archived
            else if (latestVersion == "RIP")
            {
                EditorUtility.DisplayDialog("Inventory Inventor", "Project has been put on hold indefinitely.", "Close");
            }
            //An update is available
            else if (installedVersion != latestVersion)
            {
                if (EditorUtility.DisplayDialog("Inventory Inventor", "A new update is available! (" + latestVersion + ")\nOpen the Releases page?", "Yes", "No"))
                {
                    Application.OpenURL("https://github.com/Joshuarox100/VRC-Inventory-Inventor");
                }
            }
            //Using latest version
            else
            {
                EditorUtility.DisplayDialog("Inventory Inventor", "You are using the latest version.", "Close");
            }
            DestroyImmediate(netMan);
        }));
    }

    //Retrieves text from a provided URL
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
