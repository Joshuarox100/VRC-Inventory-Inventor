using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace InventoryInventor.BMBLibraries
{
    namespace Extensions
    {
        public static class AnimatorExtensions
        {
            public static AnimatorControllerLayer DeepClone(this AnimatorControllerLayer layer)
            {
                AnimatorControllerLayer output = new AnimatorControllerLayer
                {
                    name = layer.name,
                    defaultWeight = layer.defaultWeight,
                    avatarMask = layer.avatarMask,
                    blendingMode = layer.blendingMode,
                    iKPass = layer.iKPass,
                    syncedLayerIndex = layer.syncedLayerIndex,
                    syncedLayerAffectsTiming = layer.syncedLayerAffectsTiming,
                    stateMachine = DeepClone(layer.stateMachine)
                };
                return output;
            }
            public static AnimatorStateMachine DeepClone(this AnimatorStateMachine machine)
            {
                AnimatorStateMachine output = new AnimatorStateMachine();

                //All Serializable Fields (ex. Primitives)
                EditorUtility.CopySerialized(machine, output);

                //State Machines
                ChildAnimatorStateMachine[] outMachines = new ChildAnimatorStateMachine[machine.stateMachines.Length];
                for (int i = 0; i < outMachines.Length; i++)
                {
                    outMachines[i] = new ChildAnimatorStateMachine
                    {
                        position = machine.stateMachines[i].position,
                        stateMachine = DeepClone(machine.stateMachines[i].stateMachine)
                    };
                }
                output.stateMachines = outMachines;

                //States
                ChildAnimatorState[] outStates = new ChildAnimatorState[machine.states.Length];
                for (int i = 0; i < outStates.Length; i++)
                {
                    outStates[i] = new ChildAnimatorState
                    {
                        position = machine.states[i].position,
                        state = DeepClone(machine.states[i].state)
                    };
                }

                //State Transitions
                for (int i = 0; i < outStates.Length; i++)
                {
                    AnimatorStateTransition[] outTransitions = new AnimatorStateTransition[machine.states[i].state.transitions.Length];
                    for (int j = 0; j < outTransitions.Length; j++)
                    {
                        outTransitions[j] = (AnimatorStateTransition)DeepClone(machine.states[i].state.transitions[j], outStates, outMachines);
                    }
                    outStates[i].state.transitions = outTransitions;
                }
                output.states = outStates;

                //Any Transitions
                AnimatorStateTransition[] outAnyTransitions = new AnimatorStateTransition[machine.anyStateTransitions.Length];
                for (int i = 0; i < outAnyTransitions.Length; i++)
                {
                    outAnyTransitions[i] = (AnimatorStateTransition)DeepClone(machine.anyStateTransitions[i], outStates, outMachines);
                }
                output.anyStateTransitions = outAnyTransitions;

                //Entry Transitions
                AnimatorTransition[] outEntryTransitions = new AnimatorTransition[machine.entryTransitions.Length];
                for (int i = 0; i < outEntryTransitions.Length; i++)
                {
                    outEntryTransitions[i] = (AnimatorTransition)DeepClone(machine.entryTransitions[i], outStates, outMachines);
                }
                output.entryTransitions = outEntryTransitions;

                //Behaviors
                StateMachineBehaviour[] outBehaviors = new StateMachineBehaviour[machine.behaviours.Length];
                for (int i = 0; i < outBehaviors.Length; i++)
                {
                    outBehaviors[i] = DeepClone(machine.behaviours[i]);
                }
                output.behaviours = outBehaviors;

                //Default State
                foreach (ChildAnimatorState childState in outStates)
                {
                    if (childState.state.name == machine.defaultState.name)
                    {
                        output.defaultState = childState.state;
                        break;
                    }
                }

                return output;
            }

            public static ChildAnimatorState DeepClone(this ChildAnimatorState childState)
            {
                ChildAnimatorState output = new ChildAnimatorState
                {
                    position = childState.position,
                    state = childState.state.DeepClone()
                };
                return output;
            }

            public static AnimatorState DeepClone(this AnimatorState state)
            {
                AnimatorState output = new AnimatorState();
                EditorUtility.CopySerialized(state, output);

                if (state.motion != null && state.motion.GetType() == typeof(BlendTree) && AssetDatabase.GetAssetPath(((BlendTree)state.motion).GetInstanceID()) == AssetDatabase.GetAssetPath(state.GetInstanceID()))
                {
                    output.motion = ((BlendTree)state.motion).DeepClone();
                }

                StateMachineBehaviour[] outBehaviors = new StateMachineBehaviour[state.behaviours.Length];
                for (int i = 0; i < outBehaviors.Length; i++)
                {
                    outBehaviors[i] = DeepClone(state.behaviours[i]);
                }
                output.behaviours = outBehaviors;

                return output;
            }

            public static BlendTree DeepClone(this BlendTree tree)
            {
                BlendTree output = new BlendTree();
                EditorUtility.CopySerialized(tree, output);

                ChildMotion[] children = new ChildMotion[tree.children.Length];
                for (int i = 0; i < children.Length; i++)
                {
                    children[i] = tree.children[i].DeepClone();
                }
                output.children = children;

                return output;
            }

            public static ChildMotion DeepClone(this ChildMotion child)
            {
                ChildMotion output = new ChildMotion
                {
                    cycleOffset = child.cycleOffset,
                    directBlendParameter = child.directBlendParameter,
                    mirror = child.mirror,
                    position = child.position,
                    threshold = child.threshold,
                    timeScale = child.timeScale,
                    motion = (child.motion != null && child.motion.GetType() == typeof(BlendTree)) ? ((BlendTree)child.motion).DeepClone() : child.motion
                };

                return output;
            }

            public static StateMachineBehaviour DeepClone(this StateMachineBehaviour behavior)
            {
                StateMachineBehaviour output = (StateMachineBehaviour)ScriptableObject.CreateInstance(behavior.GetType());
                EditorUtility.CopySerialized(behavior, output);
                return output;
            }
            public static AnimatorTransitionBase DeepClone(this AnimatorTransitionBase transition, ChildAnimatorState[] states, ChildAnimatorStateMachine[] machines)
            {
                AnimatorTransitionBase output = UnityEngine.Object.Instantiate(transition);
                EditorUtility.CopySerialized(transition, output);
                for (int i = 0; i < states.Length && output.destinationState != null; i++)
                {
                    if (output.destinationState.name == states[i].state.name)
                    {
                        output.destinationState = states[i].state;
                        break;
                    }
                }
                for (int i = 0; i < machines.Length && output.destinationStateMachine != null; i++)
                {
                    if (output.destinationStateMachine.name == machines[i].stateMachine.name)
                    {
                        output.destinationStateMachine = machines[i].stateMachine;
                        break;
                    }
                }
                return output;
            }
            public static AnimatorTransitionBase DeepClone(this AnimatorTransitionBase transition, ChildAnimatorState childState)
            {
                AnimatorTransitionBase output = UnityEngine.Object.Instantiate(transition);
                EditorUtility.CopySerialized(transition, output);
                output.destinationState = childState.state;
                return output;
            }

            public static AnimatorTransitionBase DeepClone(this AnimatorTransitionBase transition)
            {
                AnimatorTransitionBase output = UnityEngine.Object.Instantiate(transition);
                EditorUtility.CopySerialized(transition, output);
                return output;
            }

            public static void SaveController(this AnimatorController source)
            {
                string sourcePath = AssetDatabase.GetAssetPath(source);
                foreach (AnimatorControllerLayer layer in source.layers)
                {
                    if (AssetDatabase.GetAssetPath(layer.stateMachine).Length == 0)
                    {
                        AssetDatabase.AddObjectToAsset(layer.stateMachine, sourcePath);
                        layer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
                    }
                    foreach (var subStateMachine in layer.stateMachine.stateMachines)
                    {
                        if (AssetDatabase.GetAssetPath(subStateMachine.stateMachine).Length == 0)
                        {
                            AssetDatabase.AddObjectToAsset(subStateMachine.stateMachine, sourcePath);
                            subStateMachine.stateMachine.hideFlags = HideFlags.HideInHierarchy;
                        }
                    }
                    foreach (var childState in layer.stateMachine.states)
                    {
                        if (AssetDatabase.GetAssetPath(childState.state).Length == 0)
                        {
                            AssetDatabase.AddObjectToAsset(childState.state, sourcePath);
                            childState.state.hideFlags = HideFlags.HideInHierarchy;
                        }
                        if (childState.state.motion != null && childState.state.motion.GetType() == typeof(BlendTree))
                        {
                            SaveControllerHelper((BlendTree)childState.state.motion, sourcePath);
                        }
                        foreach (var stateBehavior in childState.state.behaviours)
                        {
                            if (AssetDatabase.GetAssetPath(stateBehavior).Length == 0)
                            {
                                AssetDatabase.AddObjectToAsset(stateBehavior, sourcePath);
                                stateBehavior.hideFlags = HideFlags.HideInHierarchy;
                            }
                        }
                        foreach (var stateTransition in childState.state.transitions)
                        {
                            if (AssetDatabase.GetAssetPath(stateTransition).Length == 0)
                            {
                                AssetDatabase.AddObjectToAsset(stateTransition, sourcePath);
                                stateTransition.hideFlags = HideFlags.HideInHierarchy;
                            }
                        }
                    }
                    foreach (var anyStateTransition in layer.stateMachine.anyStateTransitions)
                    {
                        if (AssetDatabase.GetAssetPath(anyStateTransition).Length == 0)
                        {
                            AssetDatabase.AddObjectToAsset(anyStateTransition, sourcePath);
                            anyStateTransition.hideFlags = HideFlags.HideInHierarchy;
                        }
                    }
                    foreach (var entryTransition in layer.stateMachine.entryTransitions)
                    {
                        if (AssetDatabase.GetAssetPath(entryTransition).Length == 0)
                        {
                            AssetDatabase.AddObjectToAsset(entryTransition, sourcePath);
                            entryTransition.hideFlags = HideFlags.HideInHierarchy;
                        }
                    }
                    foreach (var machineBehavior in layer.stateMachine.behaviours)
                    {
                        if (AssetDatabase.GetAssetPath(machineBehavior).Length == 0)
                        {
                            AssetDatabase.AddObjectToAsset(machineBehavior, sourcePath);
                            machineBehavior.hideFlags = HideFlags.HideInHierarchy;
                        }
                    }
                }
            }

            private static void SaveControllerHelper(BlendTree tree, string sourcePath)
            {
                foreach (ChildMotion motion in tree.children)
                {
                    if (motion.motion != null && motion.motion.GetType() == typeof(BlendTree))
                    {
                        SaveControllerHelper((BlendTree)motion.motion, sourcePath);
                    }
                }
                if (AssetDatabase.GetAssetPath(tree).Length == 0)
                {
                    AssetDatabase.AddObjectToAsset(tree, sourcePath);
                    tree.hideFlags = HideFlags.HideInHierarchy;
                }
            }
        }
    }
}
