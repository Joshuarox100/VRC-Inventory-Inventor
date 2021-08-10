using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace InventoryInventor
{
    public class ParameterManager
    {
        private static Dictionary<VRCExpressionParameters.ValueType, AnimatorControllerParameterType>
            valueTypesToParameterTypes =
                new Dictionary<VRCExpressionParameters.ValueType, AnimatorControllerParameterType>
                {
                    [VRCExpressionParameters.ValueType.Bool] = AnimatorControllerParameterType.Bool,
                    [VRCExpressionParameters.ValueType.Int] = AnimatorControllerParameterType.Int,
                    [VRCExpressionParameters.ValueType.Float] = AnimatorControllerParameterType.Float,
                };

        private static int CalculateCosts(List<VRCExpressionParameters.Parameter> parameters)
        {
            return parameters.Aggregate(
                0,
                (total, parameter) => total + VRCExpressionParameters.TypeCost(parameter.valueType)
            );
        }

        private List<VRCExpressionParameters.Parameter> _requiredParameters =
            new List<VRCExpressionParameters.Parameter>();

        public void AddParameter(VRCExpressionParameters.Parameter parameter)
        {
            _requiredParameters.Add(parameter);
        }

        public int CalculateTotalCost()
        {
            return CalculateCosts(_requiredParameters);
        }

        public bool CanApplyToExpressions(VRCExpressionParameters expressionParameters)
        {
            var baseParameters = expressionParameters.parameters
                .Where((parameter) => _requiredParameters.FindIndex((e) => e.name == parameter.name) < 0)
                .ToList();

            // Cost requirements
            var baseCost = CalculateCosts(baseParameters);
            var requiredCost = CalculateTotalCost();

            if (baseCost + requiredCost > VRCExpressionParameters.MAX_PARAMETER_COST)
            {
                // ToDo: Error Message
                return false;
            }

            // Type Matching requirements
            var typeMismatchParameters = expressionParameters.parameters
                .Where((parameter) =>
                {
                    var reqParameter = _requiredParameters.Find((e) => e.name == parameter.name);
                    return reqParameter.valueType != parameter.valueType;
                })
                .ToList();

            if (typeMismatchParameters.Count > 0)
            {
                // ToDo: Error Message
                return false;
            }

            return true;
        }

        public bool ApplyToExpressions(VRCExpressionParameters expressionParameters)
        {
            var baseParameters = expressionParameters.parameters
                .Where((parameter) => _requiredParameters.FindIndex((e) => e.name == parameter.name) < 0)
                .ToList();

            baseParameters.AddRange(_requiredParameters);
            expressionParameters.parameters = baseParameters.ToArray();
            EditorUtility.SetDirty(expressionParameters);

            return true;
        }

        public bool RemoveFromExpressions(VRCExpressionParameters expressionParameters)
        {
            throw new NotImplementedException();
            return false;
        }

        public bool ApplyToAnimator(AnimatorController animator)
        {
            var baseParameters = animator.parameters
                .Where((parameter) => _requiredParameters.FindIndex((e) => e.name == parameter.name) < 0)
                .ToList();

            var requiredAnimatorParameters = _requiredParameters
                .Select((parameter) => new AnimatorControllerParameter
                {
                    name = animator.MakeUniqueParameterName(parameter.name),
                    type = valueTypesToParameterTypes[parameter.valueType],
                })
                .ToList();

            baseParameters.AddRange(requiredAnimatorParameters);

            animator.parameters = baseParameters.ToArray();
            
            return true;
        }

        public bool RemoveFromAnimator(AnimatorController animator)
        {
            throw new NotImplementedException();
            return false;
        }
    }
}