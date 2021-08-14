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
        private static int CalculateCosts(IEnumerable<VRCExpressionParameters.Parameter> parameters)
        {
            return parameters.Aggregate(
                0,
                (total, parameter) => total + VRCExpressionParameters.TypeCost(parameter.valueType)
            );
        }

        private readonly List<VRCExpressionParameters.Parameter> _requiredExpressionParameters =
            new List<VRCExpressionParameters.Parameter>();

        private readonly List<AnimatorControllerParameter> _requiredAnimatorParameters =
            new List<AnimatorControllerParameter>();

        public void AddExpressionParameter(VRCExpressionParameters.Parameter parameter)
        {
            _requiredExpressionParameters.Add(parameter);
        }
        
        public void AddExpressionParameters(IEnumerable<VRCExpressionParameters.Parameter> parameters)
        {
            _requiredExpressionParameters.AddRange(parameters);
        }

        public void AddAnimatorParameter(AnimatorControllerParameter parameter)
        {
            _requiredAnimatorParameters.Add(parameter);
        }
        
        public void AddAnimatorParameters(IEnumerable<AnimatorControllerParameter> parameters)
        {
            _requiredAnimatorParameters.AddRange(parameters);
        }

        public int CalculateTotalCost()
        {
            return CalculateCosts(_requiredExpressionParameters);
        }

        public bool CanApplyToExpressions(VRCExpressionParameters expressionParameters)
        {
            var baseParameters = expressionParameters.parameters
                .Where((parameter) => _requiredExpressionParameters.FindIndex((e) => e.name == parameter.name) < 0)
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
                    var reqParameter = _requiredExpressionParameters.Find((e) => e.name == parameter.name);
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
                .Where((parameter) => _requiredExpressionParameters.FindIndex((e) => e.name == parameter.name) < 0)
                .ToList();

            baseParameters.AddRange(_requiredExpressionParameters);
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
                .Where((parameter) => _requiredAnimatorParameters.FindIndex((e) => e.name == parameter.name) < 0)
                .ToList();

            baseParameters.AddRange(_requiredAnimatorParameters);

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