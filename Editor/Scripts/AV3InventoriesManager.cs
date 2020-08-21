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

public class AV3InventoriesManager : UnityEngine.Object
{
    public VRCAvatarDescriptor avatar;
    public VRCExpressionsMenu menu;

    public List<AnimationClip> toggleables = new List<AnimationClip>() { null };
    public List<string> aliases = new List<string>() { "Slot 1" };

    public string relativePath;
    public string outputPath;
    public bool autoOverwrite = false;
    public float refreshRate = 0.05f;

    private Backup backupManager;
    private AssetList generated;

    public AV3InventoriesManager() { }

    public void CreateInventory()
    {
        try
        {

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            /*
                0. Check for space in parameters list & check for incompatible Animations.
            */

            if (avatar == null)
            {
                Debug.LogError("no avatar");
                return;
            }
            else if (avatar.baseAnimationLayers.Length != 5)
            {
                Debug.LogError("avatar is not humanoid");
                return;
            }
            else if (avatar.expressionParameters == null)
            {
                Debug.LogError("no parameters");
                return;
            }
            else if (menu == null && avatar.expressionsMenu == null)
            {
                Debug.LogError("no menu");
                return;
            }
  
            int paramCount = 0;
            int present = 0;
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
                            Debug.LogError("expression parameter exists with wrong type");
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
                Debug.LogError("too many parameters");
                return;
            }

            foreach (AnimationClip clip in toggleables)
            {
                if (!CheckCompatibility(clip, false, out Type problem, out string propertyName))
                {
                    EditorUtility.DisplayDialog("Inventory Inventor", "ERROR: " + clip.name + " cannot be used because it modifies an invalid property type!\n\nInvalid Property Type: " + problem.Name + "\nName: " + propertyName, "Close");
                    return;
                }
            }

            VerifyDestination();

            EditorUtility.DisplayProgressBar("Inventory Inventor", "Starting", 0);

            backupManager = new Backup();
            generated = new AssetList();

            /*
                1. Get FX Animator
             */

            AnimatorController animator = (avatar.baseAnimationLayers[4].animatorController != null) ? (AnimatorController)avatar.baseAnimationLayers[4].animatorController : null;
            bool replaceAnimator = animator != null;
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
                    Debug.LogError("failed to find template");
                    RevertChanges();
                    return;
                }
            }
            else
            {
                backupManager.AddToBackup(new Asset(AssetDatabase.GetAssetPath(animator)));
            }

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
                2. Create parameters
            */

            EditorUtility.DisplayProgressBar("Inventory Inventor", "Creating Parameters", 0.05f);

            //toggle, state, and one bool for each anim.
            AnimatorControllerParameter[] srcParam = newAnimator.parameters;
            bool[] existing = new bool[toggleables.ToArray().Length + 2];
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
                for (int j = 0; j < toggleables.ToArray().Length; j++)
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
                            Debug.LogError("parameter exists with wrong type");
                            RevertChanges();
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
                            Debug.LogError("parameter exists with wrong type");
                            RevertChanges();
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
                            Debug.LogError("parameter exists with wrong type");
                            RevertChanges();
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
                3. Create layers
            */

            if (!CreateMasterLayer(newAnimator, toggleables.ToArray().Length, out List<int[]> activeStates))
            {
                Debug.LogError("Failed to locate data files.");
                RevertChanges();
                return;
            }
            CreateItemLayers(newAnimator, ref activeStates);

            EditorUtility.DisplayProgressBar("Inventory Inventor", "Saving Controller", 0.9f);
            newAnimator.SaveController();
            AssetDatabase.SaveAssets();

            /*
                4. Add expression parameters to the list.
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
                5. Create Expressions menu for toggles.
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

    private int CreateMenus(out VRCExpressionsMenu mainMenu)
    {
        mainMenu = null;
        int totalMenus = (toggleables.ToArray().Length % 8 == 0) ? toggleables.ToArray().Length / 8 : (toggleables.ToArray().Length / 8) + 1;
        VRCExpressionsMenu inventory = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
        inventory.name = avatar.name + "_Inventory";
        if (totalMenus == 1)
        {
            for (int i = 0; i < toggleables.ToArray().Length; i++)
            {
                inventory.controls.Add(new VRCExpressionsMenu.Control() { name = aliases[i], type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter() { name = "Inventory" }, value = i + 1 });
            }
            if (!AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Menus"))
                AssetDatabase.CreateFolder(outputPath, "Menus");
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
            mainMenu = inventory;
            return 0;
        }
        else
        {
            List<VRCExpressionsMenu> pages = new List<VRCExpressionsMenu>();
            for(int i = 0; i < totalMenus; i++)
            {
                pages.Add(ScriptableObject.CreateInstance<VRCExpressionsMenu>());
                pages[pages.ToArray().Length - 1].name = "Page " + (i + 1);
                inventory.controls.Add(new VRCExpressionsMenu.Control() { name = pages[pages.ToArray().Length - 1].name, type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = pages[pages.ToArray().Length - 1] });
            }
            for (int i = 0; i < toggleables.ToArray().Length; i++)
            {
                pages[i / 8].controls.Add(new VRCExpressionsMenu.Control() { name = aliases[i], type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter() { name = "Inventory" }, value = i + 1 });
            }
            if (!AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Menus"))
                AssetDatabase.CreateFolder(outputPath, "Menus");
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
            mainMenu = inventory;
            return 0;
        }
    }

    private void CreateItemLayers(AnimatorController source, ref List<int[]> activeStates)
    {

        AnimatorStateMachine templateMachine = new AnimatorStateMachine();
        ChildAnimatorState[] states = new ChildAnimatorState[templateMachine.states.Length + 2];
        templateMachine.states.CopyTo(states, 2);
        ChildAnimatorState templateState = new ChildAnimatorState
        {
            state = new AnimatorState
            {
                name = "",
                motion = null,
            }
        };
        Vector3 pos = templateMachine.anyStatePosition;
        ChangeState(templateState, "Off");
        templateState.position = pos - new Vector3(150, -45);
        states[0] = templateState.DeepClone();
        templateState.position = pos + new Vector3(100, 45);
        ChangeState(templateState, "On");
        states[1] = templateState.DeepClone();
        templateMachine.states = states;

        AnimatorStateTransition templateTransition = new AnimatorStateTransition
        {
            destinationState = null,
            isExit = false,
            hasExitTime = false,
            duration = 0,
            canTransitionToSelf = false,
            conditions = null
        };

        for (int i = 0; i < toggleables.ToArray().Length; i++)
        {
            EditorUtility.DisplayProgressBar("Inventory Inventor", string.Format(CultureInfo.InvariantCulture, "Creating Item Layers: {0} ({1:#0.##%})", aliases[i], (i + 1f) / toggleables.ToArray().Length), 0.55f + (0.35f * (float.Parse(i.ToString()) / toggleables.ToArray().Length)));
            int[] active = activeStates[i];
            source.AddLayer(aliases[i]);
            AnimatorControllerLayer[] layers = source.layers;
            AnimatorControllerLayer currentLayer = layers[layers.Length - 1];
            currentLayer.defaultWeight = 1;

            AnimatorStateTransition[] transitions = new AnimatorStateTransition[2];
            ChangeTransition(templateTransition, ref active[0], templateMachine.states[0].state);
            transitions[0] = (AnimatorStateTransition)templateTransition.DeepClone(templateMachine.states[0]);
            ChangeState(templateMachine.states[1].state, toggleables[i]);
            ChangeTransition(templateTransition, ref active[1], templateMachine.states[1].state);
            transitions[1] = (AnimatorStateTransition)templateTransition.DeepClone(templateMachine.states[1]);
            templateMachine.anyStateTransitions = transitions;

            templateMachine.name = "Inventory " + (i + 1);
            currentLayer.stateMachine = templateMachine.DeepClone();
            layers[layers.Length - 1] = currentLayer;
            source.layers = layers;
        }
        return;
    }

    private bool CreateMasterLayer(AnimatorController source, int itemTotal, out List<int[]> activeStates)
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

        //Create List
        activeStates = new List<int[]>();
        int value = itemTotal + 1;
        for (int i = 0; i < itemTotal; i++)
        {
            activeStates.Add(new int[] { value, value + 1 });
            value += 2;
        } 

        ChildAnimatorState[] states = new ChildAnimatorState[masterLayer.stateMachine.states.Length + (itemTotal * 4) + 1];
        masterLayer.stateMachine.states.CopyTo(states, itemTotal * 4 + 1);
        Vector3 pos = masterLayer.stateMachine.entryPosition;
        states[itemTotal * 4] = new ChildAnimatorState
        {
            position = pos + new Vector3(-25, 50),
            state = new AnimatorState
            {
                name = "Remote Clients",
            }
        };

        ChildAnimatorState templateState = new ChildAnimatorState
        {
            state = new AnimatorState
            {
                name = "",
                behaviours = new StateMachineBehaviour[] { ScriptableObject.CreateInstance<VRCAvatarParameterDriver>() }
            }
        };
        ((VRCAvatarParameterDriver)templateState.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = 0 });
        pos += new Vector3(0, 125);

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

        ChangeState(templateState, "Syncing " + 1, activeStates[0], true);
        templateState.position = pos - new Vector3(150, 0);
        states[0] = templateState.DeepClone();
        ChangeState(templateState, "Syncing " + 1 + " ", activeStates[0], false);
        templateState.position = pos + new Vector3(100, 0);
        states[1] = templateState.DeepClone();
        pos += new Vector3(0, 75);
        int index = 2;
        for (int i = 2; i < itemTotal * 2; i++)
        {
            EditorUtility.DisplayProgressBar("Inventory Inventor", string.Format(CultureInfo.InvariantCulture, "Creating Master Layer: Creating States ({0:#0.##%})", (i + 1f) / (itemTotal * 4)), 0.1f + (0.225f * ((i + 1f) / (itemTotal * 4))));
            switch (i % 2 == 0)
            {
                case true:
                    ChangeState(templateState, "Syncing " + index, activeStates[index - 1], true);
                    templateState.position = pos - new Vector3(150, 0);
                    states[i] = templateState.DeepClone();
                    ChangeTransition(templateTransition, states[i], index, true);
                    states[i - 2].state.AddTransition((AnimatorStateTransition)AnimatorExtensions.DeepClone(templateTransition, states[i]));
                    ChangeTransition(templateTransition, states[i], index, true);
                    states[i - 1].state.AddTransition((AnimatorStateTransition)AnimatorExtensions.DeepClone(templateTransition, states[i]));
                    break;
                case false:
                    ChangeState(templateState, "Syncing " + index + " ", activeStates[index - 1], false);
                    templateState.position = pos + new Vector3(100, 0);
                    states[i] = templateState.DeepClone();
                    ChangeTransition(templateTransition, states[i], index, false);
                    states[i - 2].state.AddTransition((AnimatorStateTransition)AnimatorExtensions.DeepClone(templateTransition, states[i]));
                    ChangeTransition(templateTransition, states[i], index, false);
                    states[i - 3].state.AddTransition((AnimatorStateTransition)AnimatorExtensions.DeepClone(templateTransition, states[i]));
                    index++;
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
        states[itemTotal * 4].state.AddExitTransition();
        states[itemTotal * 4].state.transitions[0].AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
        states[itemTotal * 4].state.transitions[0].hasExitTime = false;
        states[itemTotal * 4].state.transitions[0].duration = 0;
        masterLayer.stateMachine.exitPosition = pos;

        ChildAnimatorState templateToggle = new ChildAnimatorState
        {
            state = new AnimatorState { behaviours = new StateMachineBehaviour[] { ScriptableObject.CreateInstance<VRCAvatarParameterDriver>() } }
        };
        ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory", value = 0 });
        AnimatorStateTransition toggleTransition = new AnimatorStateTransition
        {
            isExit = true,
            exitTime = 1f,
            hasExitTime = true,
            duration = 0f,
            canTransitionToSelf = false
        };

        templateTransition.hasExitTime = false;
        index = itemTotal * 2;
        pos += new Vector3(0, 60);

        AnimatorStateTransition[] anyTransitions = new AnimatorStateTransition[itemTotal * 2];

        for (int i = itemTotal - 1; i >= 0; i--)
        {
            EditorUtility.DisplayProgressBar("Inventory Inventor", string.Format(CultureInfo.InvariantCulture, "Creating Master Layer: Creating States ({0:#0.##%})", index / (itemTotal * 4)), 0.1f + (0.225f * (index / (itemTotal * 4f))));
            templateToggle.state.name = ("Toggling " + (i + 1) + ": On");
            templateToggle.position = pos - new Vector3(150, 0);
            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters[0].value = activeStates[i][1];
            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory " + (i + 1), value = 1 });
            states[index] = templateToggle.DeepClone();
            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.RemoveAt(1);
            states[index].state.transitions = new AnimatorStateTransition[] { (AnimatorStateTransition)toggleTransition.DeepClone() };
            templateTransition.destinationState = states[index].state;
            templateTransition.conditions = new AnimatorCondition[0];
            templateTransition.AddCondition(AnimatorConditionMode.Equals, i + 1, "Inventory");
            templateTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "Inventory " + (i + 1));
            templateTransition.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            anyTransitions[index - (itemTotal * 2)] = (AnimatorStateTransition)templateTransition.DeepClone(states[index]);
            index++;

            templateToggle.state.name = ("Toggling " + (i + 1) + ": Off");
            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters[0].value = activeStates[i][0];
            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "Inventory " + (i + 1), value = 0 });
            templateToggle.position = pos + new Vector3(100, 0);
            states[index] = templateToggle.DeepClone();
            ((VRCAvatarParameterDriver)templateToggle.state.behaviours[0]).parameters.RemoveAt(1);
            states[index].state.transitions = new AnimatorStateTransition[] { (AnimatorStateTransition)toggleTransition.DeepClone() };
            templateTransition.destinationState = states[index].state;
            templateTransition.conditions = new AnimatorCondition[0];
            templateTransition.AddCondition(AnimatorConditionMode.Equals, i + 1, "Inventory");
            templateTransition.AddCondition(AnimatorConditionMode.If, 0, "Inventory " + (i + 1));
            templateTransition.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");
            anyTransitions[index - (itemTotal * 2)] = (AnimatorStateTransition)templateTransition.DeepClone(states[index]);
            index++;

            pos += new Vector3(0, 75);
        }
        masterLayer.stateMachine.anyStatePosition = pos;
        masterLayer.stateMachine.states = states;
        masterLayer.stateMachine.anyStateTransitions = anyTransitions;
        masterLayer.stateMachine.defaultState = states[itemTotal * 4].state;
        //Start Transitions
        masterLayer.stateMachine.AddEntryTransition(states[0].state);
        masterLayer.stateMachine.AddEntryTransition(states[1].state);
        AnimatorTransition[] entryTransitions = masterLayer.stateMachine.entryTransitions;
        entryTransitions[0].AddCondition(AnimatorConditionMode.If, 0, "Inventory 1");
        entryTransitions[1].AddCondition(AnimatorConditionMode.IfNot, 0, "Inventory 1");
        masterLayer.stateMachine.entryTransitions = entryTransitions;
        AnimatorControllerLayer[] layers = source.layers;
        layers[layers.Length - 1] = masterLayer;
        source.layers = layers;
        return true;
    }

    public static void ChangeTransition(AnimatorStateTransition transition, ref int value, AnimatorState state)
    {
        transition.destinationState = state;
        transition.conditions = new AnimatorCondition[0];
        transition.AddCondition(AnimatorConditionMode.Equals, value, "Inventory");
    }

    public static void ChangeTransition(AnimatorStateTransition transition, ref double value, ChildAnimatorState childState, bool first = true)
    {
        transition.destinationState = childState.state;
        transition.conditions = new AnimatorCondition[0];
        switch (first)
        {
            case true:
                transition.AddCondition(AnimatorConditionMode.Less, float.Parse(value.ToString()) - 0.0000002f, "Inventory State");
                break;
            case false:
                transition.AddCondition(AnimatorConditionMode.Greater, float.Parse(value.ToString()) + 0.0000002f, "Inventory State");
                break;
        }                      
    }

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

    public static void ChangeState(AnimatorState state, string name, int value)
    {
        state.name = name;
        ((VRCAvatarParameterDriver)state.behaviours[0]).parameters[0].value = value;
        return;
    }

    public static void ChangeState(AnimatorState state, string name)
    {
        state.name = name;
        return;
    }

    public static void ChangeState(ChildAnimatorState childState, string name)
    {
        ChangeState(childState.state, name);
        return;
    }

    public static void ChangeState(AnimatorState state, Motion motion)
    {
        state.motion = motion;
        return;
    }

    private void RevertChanges()
    {
        AssetDatabase.SaveAssets();
        if (backupManager != null && !backupManager.RestoreAssets())
            Debug.LogError("[Inventory Inventor] Failed to revert all changes.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        for (int i = 0; generated != null && i < generated.ToArray().Length; i++)
            if (File.Exists(generated[i].path) && !AssetDatabase.DeleteAsset(generated[i].path))
                Debug.LogError("[Inventory Inventor] Failed to revert all changes.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Animators") && AssetDatabase.FindAssets("", new string[] { outputPath + Path.DirectorySeparatorChar + "Animators" }).Length == 0)
            if (!AssetDatabase.DeleteAsset(outputPath + Path.DirectorySeparatorChar + "Animators"))
                Debug.LogError("[Inventory Inventor] Failed to revert all changes.");
        if (AssetDatabase.IsValidFolder(outputPath + Path.DirectorySeparatorChar + "Menus") && AssetDatabase.FindAssets("", new string[] { outputPath + Path.DirectorySeparatorChar + "Menus" }).Length == 0)
            if (!AssetDatabase.DeleteAsset(outputPath + Path.DirectorySeparatorChar + "Menus"))
                Debug.LogError("[Inventory Inventor] Failed to revert all changes.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public void UpdatePaths()
    {
        string old = relativePath;
        relativePath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("AV3InventoriesWindow")[0]).Substring(0, AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("AV3InventoriesWindow")[0]).LastIndexOf("Editor") - 1);
        if (relativePath == old)
            return;
        else if (outputPath == null || !AssetDatabase.IsValidFolder(outputPath))
        {
            outputPath = relativePath + Path.DirectorySeparatorChar + "Output";
        }
    }

    private class NetworkManager : MonoBehaviour { }

    public static void CheckForUpdates()
    {
        string relativePath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("AV3InventoriesWindow")[0]).Substring(0, AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("AV3InventoriesWindow")[0]).LastIndexOf("Editor") - 1);
        string installedVersion = (AssetDatabase.FindAssets("VERSION", new string[] { relativePath }).Length > 0) ? File.ReadAllText(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("VERSION", new string[] { relativePath })[0])) : "";

        GameObject netMan = new GameObject { hideFlags = HideFlags.HideInHierarchy };
        netMan.AddComponent<NetworkManager>().StartCoroutine(GetText("https://raw.githubusercontent.com/Joshuarox100/VRC-AV3-Overrides/master/Editor/VERSION", latestVersion => {
            if (latestVersion == "")
            {
                EditorUtility.DisplayDialog("AV3 Inventories", "Failed to fetch the latest version.\n(Check console for details.)", "Close");
            }
            else if (installedVersion == "")
            {
                EditorUtility.DisplayDialog("AV3 Inventories", "Failed to identify installed version.\n(VERSION file was not found.)", "Close");
            }
            else if (latestVersion == "RIP")
            {
                EditorUtility.DisplayDialog("AV3 Inventories", "Project has been put on hold indefinitely.", "Close");
            }
            else if (installedVersion != latestVersion)
            {
                if (EditorUtility.DisplayDialog("AV3 Inventories", "A new update is available! (" + latestVersion + ")\nOpen the Releases page?", "Yes", "No"))
                {
                    Application.OpenURL("https://github.com/Joshuarox100/VRC-AV3-Overrides/releases");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("AV3 Inventories", "You are using the latest version.", "Close");
            }
            DestroyImmediate(netMan);
        }));
    }

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
