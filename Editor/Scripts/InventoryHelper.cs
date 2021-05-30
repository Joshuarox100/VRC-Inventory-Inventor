using UnityEngine;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace InventoryInventor
{
    public class Helper
    {
        // Helper method for getting the path of a game object.
        public static string GetGameObjectPath(Transform transform)
        {
            if (transform == null)
                return "";
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        // Helper methods for modifying transitions.
        public static void ChangeTransition(AnimatorStateTransition transition, int value, AnimatorState state)
        {
            transition.destinationState = state;
            transition.conditions = new AnimatorCondition[0];
            transition.AddCondition(AnimatorConditionMode.Equals, value, "Inventory");
        }

        public static void ChangeTransition(AnimatorStateTransition transition, string item, bool value, AnimatorState state)
        {
            transition.destinationState = state;
            transition.conditions = new AnimatorCondition[0];
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Inventory " + item);
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

        // Helper methods for modifying states.
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
    }

}
