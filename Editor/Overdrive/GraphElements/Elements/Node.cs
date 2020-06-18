using System.Collections.Generic;
using System.Linq;
using UnityEditor.GraphToolsFoundation.Overdrive.Model;
using UnityEngine.UIElements;

namespace UnityEditor.GraphToolsFoundation.Overdrive.GraphElements
{
    public class Node : GraphElement, IMovableGraphElement
    {
        public IGTFNodeModel NodeModel => Model as IGTFNodeModel;

        protected ContextualMenuManipulator m_ContextualMenuManipulator;

        public new static readonly string k_UssClassName = "ge-node";
        public static readonly string k_NotConnectedModifierUssClassName = k_UssClassName.WithUssModifier("not-connected");
        public static readonly string k_EmptyModifierUssClassName = k_UssClassName.WithUssModifier("empty");

        public static readonly string k_TitleContainerPartName = "title-container";
        public static readonly string k_PortContainerPartName = "port-top-container";

        public Node()
        {
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            m_ContextualMenuManipulator = new ContextualMenuManipulator(BuildContextualMenu);
            this.AddManipulator(m_ContextualMenuManipulator);
        }

        protected override void BuildPartList()
        {
            PartList.AppendPart(EditableTitlePart.Create(k_TitleContainerPartName, Model, this, k_UssClassName));
            PartList.AppendPart(PortContainerPart.Create(k_PortContainerPartName, Model, this, k_UssClassName));
        }

        protected override void PostBuildUI()
        {
            base.PostBuildUI();

            usageHints = UsageHints.DynamicTransform;
            AddToClassList(k_UssClassName);
            this.AddStylesheet("Node.uss");
        }

        protected override void UpdateElementFromModel()
        {
            base.UpdateElementFromModel();

            var newPos = NodeModel.Position;
            style.left = newPos.x;
            style.top = newPos.y;

            EnableInClassList(k_EmptyModifierUssClassName, childCount == 0);

            if (NodeModel is IHasPorts portHolder && portHolder.Ports != null)
            {
                bool noPortConnected = portHolder.Ports.All(port => !port.IsConnected);
                EnableInClassList(k_NotConnectedModifierUssClassName, noPortConnected);
            }
        }

        protected virtual void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target == this)
            {
                evt.menu.AppendAction("Disconnect all", DisconnectAll, DisconnectAllStatus);
                evt.menu.AppendSeparator();
            }
        }

        internal virtual void UpdateEdges()
        {
            if (NodeModel is IHasPorts portContainer && portContainer.Ports != null)
            {
                foreach (var portModel in portContainer.Ports)
                {
                    foreach (var edgeModel in portModel.ConnectedEdges)
                    {
                        var edge = edgeModel.GetUI<Edge>(GraphView);
                        edge?.UpdateFromModel();
                    }
                }
            }
        }

        void OnGeometryChanged(GeometryChangedEvent e)
        {
            if (e.target == this)
                UpdateEdges();
        }

        static void AddConnectionsToDeleteSet(IEnumerable<IGTFPortModel> ports, ref HashSet<IGTFGraphElementModel> toDelete)
        {
            if (ports == null)
                return;

            foreach (var port in ports.Where(p => p.IsConnected))
            {
                foreach (var c in port.ConnectedEdges.Where(c => c.IsDeletable))
                {
                    toDelete.Add(c);
                }
            }
        }

        void DisconnectAll(DropdownMenuAction a)
        {
            if (NodeModel is IHasPorts portHolder)
            {
                HashSet<IGTFGraphElementModel> toDeleteModels = new HashSet<IGTFGraphElementModel>();
                AddConnectionsToDeleteSet(portHolder.Ports, ref toDeleteModels);
                Store.Dispatch(new DeleteElementsAction(toDeleteModels.ToArray()));
            }
        }

        DropdownMenuAction.Status DisconnectAllStatus(DropdownMenuAction a)
        {
            if (NodeModel is IHasPorts portHolder && portHolder.Ports != null && portHolder.Ports.Any(port => port.IsConnected))
            {
                return DropdownMenuAction.Status.Normal;
            }

            return DropdownMenuAction.Status.Disabled;
        }

        public virtual void UpdatePinning()
        {
        }

        public virtual bool IsMovable => true;

        // TODO JOCE: This is required until we have a dirtying mechanism (see ShowConnectedExecutionEdgesOrder in NodeModel.cs)
        internal void UpdateOutgoingExecutionEdges()
        {
            foreach (var edge in ((IHasPorts)NodeModel).ConnectedPortsWithReorderableEdges().SelectMany(p => p.ConnectedEdges))
                edge.GetUI<Edge>(GraphView)?.UpdateFromModel();
        }

        public override void OnSelected()
        {
            base.OnSelected();
            if (!(NodeModel is IHasPorts hasPorts))
                return;
            hasPorts.RevealReorderableEdgesOrder(true);
            UpdateOutgoingExecutionEdges();
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            if (!(NodeModel is IHasPorts hasPorts))
                return;
            hasPorts.RevealReorderableEdgesOrder(false);
            UpdateOutgoingExecutionEdges();
        }
    }
}