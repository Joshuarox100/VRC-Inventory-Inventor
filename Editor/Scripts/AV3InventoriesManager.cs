using BMBLibraries.Classes;
using BMBLibraries.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
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

            bool toggleExists = false;
            bool stateExists = false;
            int paramCount = 0;
            int present = 0;
            foreach (VRCExpressionParameters.Parameter param in avatar.expressionParameters.parameters)
            {
                if (toggleExists && stateExists)
                {
                    break;
                }
                switch (param.name) 
                {
                    case "Inventory Toggle":
                        if (param.valueType == VRCExpressionParameters.ValueType.Int)
                        {
                            present++;
                            toggleExists = true;
                        }
                        else
                        {
                            Debug.LogError("expression parameter exists with wrong type");
                            return;
                        }
                        break;
                    case "Inventory State":
                        if (param.valueType == VRCExpressionParameters.ValueType.Float)
                        {
                            present++;
                            stateExists = true;
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

            if (16 - paramCount < 2 - present)
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

            backupManager = new Backup();
            generated = new AssetList();

            /*
                1. Get FX Animator
             */

            AnimatorController animator = (avatar.baseAnimationLayers[4].animatorController != null) ? (AnimatorController)avatar.baseAnimationLayers[4].animatorController : null;

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

            /*
                2. Create parameters
            */

            //toggle, state, and one bool for each anim.
            AnimatorControllerParameter[] srcParam = animator.parameters;
            bool[] existing = new bool[toggleables.ToArray().Length + 2];
            for (int i = 0; i < srcParam.Length; i++)
            {
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
                    else if (srcParam[i].name == "Inventory Toggle")
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
                    else if (srcParam[i].name == ("Inventory State"))
                    {
                        if (srcParam[i].type == AnimatorControllerParameterType.Float)
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
                if (i < existing.Length - 2)
                {
                    if (!existing[i])
                    {
                        animator.AddParameter("Inventory " + (i + 1), AnimatorControllerParameterType.Bool);
                    }
                }
                else if (i == existing.Length - 2)
                {
                    if (!existing[i])
                    {
                        animator.AddParameter("Inventory Toggle", AnimatorControllerParameterType.Int);
                    }
                }
                else if (i == existing.Length - 1)
                {
                    if (!existing[i])
                    {
                        animator.AddParameter("Inventory State", AnimatorControllerParameterType.Float);
                    }
                }
            }

            AssetDatabase.SaveAssets();

            /*
                3. Create layers
            */

            CreateRemoteLayer(animator, toggleables.ToArray().Length);
            CreateMasterLayer(animator, toggleables.ToArray().Length, out List<KeyValuePair<int, double[]>> activeStates); ;
            CreateItemLayers(animator, activeStates);

            animator.SaveController();
            AssetDatabase.SaveAssets();

            /*
                4. Add expression parameters to the list.
            */

            foreach (VRCExpressionParameters.Parameter param in avatar.expressionParameters.parameters)
            {
                if (toggleExists && stateExists)
                {
                    break;
                }
                else if (param.name == "")
                {
                    if (!toggleExists)
                    {
                        param.name = "Inventory Toggle";
                        param.valueType = VRCExpressionParameters.ValueType.Int;
                        toggleExists = true;
                    }
                    else if (!stateExists)
                    {
                        param.name = "Inventory State";
                        param.valueType = VRCExpressionParameters.ValueType.Float;
                        stateExists = true;
                    }
                }
            }

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
        int totalMenus = (toggleables.ToArray().Length / 8) + 1;
        VRCExpressionsMenu inventory = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
        inventory.name = avatar.name + "_Inventory";
        if (totalMenus == 1)
        {
            for (int i = 0; i < toggleables.ToArray().Length; i++)
            {
                inventory.controls.Add(new VRCExpressionsMenu.Control() { name = (aliases[i] != "") ? aliases[i] : "Slot " + (i + 1), type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter() { name = "Inventory Toggle" }, value = i + 1 });
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
            }
            else
            {
                existed = false;
            }
            AssetDatabase.CreateAsset(inventory, outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + inventory.name + ".asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (AssetDatabase.FindAssets(avatar.name + "_Inventory", new string[] { outputPath + Path.DirectorySeparatorChar + "Menus" }).Length == 0)
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
            for(int i = 0; i < totalMenus && i < 3; i++)
            {
                pages.Add(ScriptableObject.CreateInstance<VRCExpressionsMenu>());
                pages[pages.ToArray().Length - 1].name = "Page " + (i + 1);
                inventory.controls.Add(new VRCExpressionsMenu.Control() { name = pages[pages.ToArray().Length - 1].name, type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = pages[pages.ToArray().Length - 1] });
            }
            for (int i = 0; i < toggleables.ToArray().Length; i++)
            {
                pages[i / 8].controls.Add(new VRCExpressionsMenu.Control() { name = (aliases[i] != "") ? aliases[i] : "Slot " + (i + 1), type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter() { name = "Inventory Toggle" }, value = i + 1 });
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
                }
                else
                {
                    exists = false;
                }
                AssetDatabase.CreateAsset(inventory, outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + avatar.name + "_" + page.name + ".asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                if (AssetDatabase.FindAssets(avatar.name + "_" + page.name, new string[] { outputPath + Path.DirectorySeparatorChar + "Menus" }).Length == 0)
                {
                    return 3;
                }
                else
                {
                    AssetDatabase.Refresh();
                    if (!exists)
                        generated.Add(new Asset(outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + avatar.name + "_" + page.name + ".asset"));
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
            }
            else
            {
                existed = false;
            }
            AssetDatabase.CreateAsset(inventory, outputPath + Path.DirectorySeparatorChar + "Menus" + Path.DirectorySeparatorChar + inventory.name + ".asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (AssetDatabase.FindAssets(avatar.name + "_Inventory", new string[] { outputPath + Path.DirectorySeparatorChar + "Menus" }).Length == 0)
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

    private void CreateItemLayers(AnimatorController source, List<KeyValuePair<int, double[]>> activeStates)
    {
        for (int i = 0; i < toggleables.ToArray().Length; i++)
        {
            double[] active = activeStates[i].Value;
            for (int j = 0; j < source.layers.Length; j++)
            {
                if (source.layers[j].name == "Inventory" + (j + 1))
                {
                    source.RemoveLayer(j);
                    break;
                }
            }
            source.AddLayer("Inventory " + (i + 1));
            AnimatorControllerLayer currentLayer = source.layers[source.layers.Length - 1];
            currentLayer.defaultWeight = 1;

            AnimatorState offState = currentLayer.stateMachine.AddState("Off");
            currentLayer.stateMachine.defaultState = offState;
            AnimatorStateTransition offTransition = currentLayer.stateMachine.AddAnyStateTransition(offState);
            offTransition.canTransitionToSelf = false;
            offTransition.hasExitTime = false;
            offTransition.duration = 0;
            foreach (float state in active)
            {
                offTransition.AddCondition(AnimatorConditionMode.Less, state - 0.0000001f, "Inventory State");
                offTransition.AddCondition(AnimatorConditionMode.Greater, state + 0.0000001f, "Inventory State");
            }

            AnimatorState onState = currentLayer.stateMachine.AddState("On");
            onState.motion = toggleables[i];
            foreach(float state in active)
            {
                AnimatorStateTransition transition = currentLayer.stateMachine.AddAnyStateTransition(onState);
                transition.canTransitionToSelf = false;
                transition.hasExitTime = false;
                transition.duration = 0;
                transition.AddCondition(AnimatorConditionMode.Greater, state - 0.0000001f, "Inventory State");
                transition.AddCondition(AnimatorConditionMode.Less, state + 0.0000001f, "Inventory State");
            }
            AnimatorControllerLayer[] layers = source.layers;
            layers[layers.Length - 1] = currentLayer;
            source.layers = layers;
        }
        return;
    }

    private void CreateRemoteLayer(AnimatorController source, int itemTotal)
    {
        for (int i = 0; i < source.layers.Length; i++)
        {
            if (source.layers[i].name == "Inventory Remote")
            {
                source.RemoveLayer(i);
                break;
            }
        }
        source.AddLayer("Inventory Remote");
        AnimatorControllerLayer masterLayer = source.layers[source.layers.Length - 1];
        AnimatorState waitState = masterLayer.stateMachine.AddState("Wait");
        masterLayer.stateMachine.defaultState = waitState;
        AnimatorState resetState = masterLayer.stateMachine.AddState("Reset");
        VRCAvatarParameterDriver resetDriver = resetState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
        resetDriver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter() { name = "Inventory Toggle", value = 0 });
        AnimatorStateTransition exitTransition = resetState.AddExitTransition();
        exitTransition.duration = 0;
        exitTransition.hasExitTime = false;
        exitTransition.AddCondition(AnimatorConditionMode.Equals, 0, "Inventory Toggle");

        for (int i = 0; i < itemTotal; i++)
        {
            AnimatorState stateOn = masterLayer.stateMachine.AddState((i + 1) + " On");
            AnimatorStateTransition transitionOnIn= waitState.AddTransition(stateOn);
            transitionOnIn.hasExitTime = false;
            transitionOnIn.duration = 0;
            transitionOnIn.AddCondition(AnimatorConditionMode.IfNot, 0, "Inventory " + (i + 1));
            transitionOnIn.AddCondition(AnimatorConditionMode.Equals, (i + 1), "Inventory Toggle");
            AnimatorStateTransition transitionOnOut = stateOn.AddTransition(resetState);
            transitionOnOut.hasExitTime = false;
            transitionOnOut.duration = 0;
            transitionOnOut.AddCondition(AnimatorConditionMode.If, 0, "Inventory " + (i + 1));          
            VRCAvatarParameterDriver driverOn = stateOn.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driverOn.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter() { name = "Inventory " + (i + 1), value = 1 });

            AnimatorState stateOff = masterLayer.stateMachine.AddState((i + 1) + " Off");
            AnimatorStateTransition transitionOffIn = waitState.AddTransition(stateOff);
            transitionOffIn.hasExitTime = false;
            transitionOffIn.duration = 0;
            transitionOffIn.AddCondition(AnimatorConditionMode.If, 0, "Inventory " + (i + 1));
            transitionOffIn.AddCondition(AnimatorConditionMode.Equals, (i + 1), "Inventory Toggle");
            AnimatorStateTransition transitionOffOut = stateOff.AddTransition(resetState);
            transitionOffOut.hasExitTime = false;
            transitionOffOut.duration = 0;
            transitionOffOut.AddCondition(AnimatorConditionMode.IfNot, 0, "Inventory " + (i + 1));
            VRCAvatarParameterDriver driverOff = stateOff.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driverOff.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter() { name = "Inventory " + (i + 1), value = 0 });
        }

        AnimatorControllerLayer[] layers = source.layers;
        layers[layers.Length - 1] = masterLayer;
        source.layers = layers;
        return;
    }

    private void CreateMasterLayer(AnimatorController source, int itemTotal, out List<KeyValuePair<int, double[]>> activeStates)
    {
        for (int i = 0; i < source.layers.Length; i++)
        {
            if (source.layers[i].name == "Inventory Master")
            {
                source.RemoveLayer(i);
                break;
            }
        }
        source.AddLayer("Inventory Master");
        AnimatorControllerLayer masterLayer = source.layers[source.layers.Length - 1];
        activeStates = new List<KeyValuePair<int, double[]>>();

        List<int> temp = new List<int>();
        for (int i = 0; i < itemTotal; i++)
        {
            temp.Add(i);
            activeStates.Add(new KeyValuePair<int, double[]>(i, new double[0]));
        }
        int[][] tempArray = ExtraMath.GetPowerSet(temp).Select(subset => subset.ToArray()).ToArray();
        //convert to bool array
        bool[][] itemArray = new bool[tempArray.Length][];
        for (int i = 0; i < itemArray.Length; i++)
        {
            itemArray[i] = new bool[itemTotal];
            for (int j = 0; j < itemTotal; j++)
            {
                if (tempArray[i].Contains(j))
                {
                    itemArray[i][j] = true;
                    double[] active = activeStates[j].Value;
                    double[] newActive = new double[active.Length + 1];
                    active.CopyTo(newActive, 0);
                    newActive[active.Length] = (Convert.ToDouble(i) % 20000000 / 10000000d) - 1;
                    activeStates[j] = new KeyValuePair<int, double[]>(j, newActive);
                }
            }
        }

        for (int i = 0; i < itemArray.Length; i++)
        {
            AnimatorState state = masterLayer.stateMachine.AddState(i.ToString());
            AnimatorStateTransition transition = masterLayer.stateMachine.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.duration = 0;
            transition.canTransitionToSelf = false;
            for (int j = 0; j < itemArray[0].Length; j++)
            {
                switch (itemArray[i][j])
                {
                    case true:
                        transition.AddCondition(AnimatorConditionMode.If, 0, "Inventory " + (j + 1));
                        break;
                    case false:
                        transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Inventory " + (j + 1));
                        break;
                }
            }
            VRCAvatarParameterDriver driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter() { name = "Inventory State", value = float.Parse(((i % 20000000 / 10000000d) - 1).ToString()) });
            if (i == 0)
            {
                masterLayer.stateMachine.defaultState = state;
            }
        }

        AnimatorControllerLayer[] layers = source.layers;
        layers[layers.Length - 1] = masterLayer;
        source.layers = layers;
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
            if (!AssetDatabase.DeleteAsset(generated[i].path))
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
