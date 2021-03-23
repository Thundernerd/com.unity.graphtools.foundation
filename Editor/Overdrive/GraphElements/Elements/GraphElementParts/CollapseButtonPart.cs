using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.GraphToolsFoundation.Overdrive
{
    public class CollapseButtonPart : BaseModelUIPart
    {
        public static CollapseButtonPart Create(string name, IGraphElementModel model, IModelUI modelUI, string parentClassName)
        {
            if (model is ICollapsible)
            {
                return new CollapseButtonPart(name, model, modelUI, parentClassName);
            }

            return null;
        }

        public override VisualElement Root => CollapseButton;

        protected CollapseButton CollapseButton { get; set; }

        protected CollapseButtonPart(string name, IGraphElementModel model, IModelUI ownerElement, string parentClassName)
            : base(name, model, ownerElement, parentClassName) { }

        protected override void BuildPartUI(VisualElement container)
        {
            if (m_Model is ICollapsible)
            {
                CollapseButton = new CollapseButton { name = PartName };
                CollapseButton.AddToClassList(m_ParentClassName.WithUssElement(PartName));
                container.Add(CollapseButton);
            }
        }

        protected override void UpdatePartFromModel()
        {
            if (CollapseButton != null)
            {
                var collapsed = (m_Model as ICollapsible)?.Collapsed ?? false;
                CollapseButton.SetValueWithoutNotify(collapsed);
            }
        }
    }
}
