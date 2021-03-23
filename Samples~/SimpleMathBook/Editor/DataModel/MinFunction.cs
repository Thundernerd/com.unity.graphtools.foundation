using System;
using UnityEngine;

namespace UnityEditor.GraphToolsFoundation.Overdrive.Samples.MathBook
{
    [Serializable]
    public class MinFunction : MathFunction
    {
        public MinFunction()
        {
            Title = "Min";

            if (m_ParameterNames.Length == 0)
            {
                m_ParameterNames = new string[] { "a", "b" };
            }
        }

        public override float Evaluate()
        {
            return Mathf.Min(GetParameterValue(0), GetParameterValue(1));
        }
    }
}
