using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.GraphToolsFoundation.Overdrive
{
    /// <summary>
    /// Handles Drag and Drop of Blackboard Elements.
    /// Create a variable based on the Blackboard Field dragged
    /// </summary>
    public class BlackboardDragAndDropHandler : DragAndDropHandler
    {
        const float DragDropSpacer = 25f;

        public Stencil Stencil => Dispatcher.GraphToolState.WindowState.GraphModel.Stencil;
        protected IDragSource DragSource { get; }
        protected CommandDispatcher Dispatcher { get; }

        public BlackboardDragAndDropHandler(GraphView graphView)
            : this(graphView, graphView.CommandDispatcher)
        {
        }

        public BlackboardDragAndDropHandler(Blackboard blackboard)
            : this(blackboard, blackboard.CommandDispatcher)
        {
        }

        [PublicAPI]
        public BlackboardDragAndDropHandler(IDragSource dragSource, CommandDispatcher dispatcher)
        {
            DragSource = dragSource;
            Dispatcher = dispatcher;
        }

        public override void OnDragUpdated(DragUpdatedEvent e)
        {
            DragAndDrop.visualMode = e.ctrlKey ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Move;
        }

        public override void OnDragPerform(DragPerformEvent e)
        {
            var dropElements = DragSource.GetSelection();

            var contentViewContainer = (e.target as GraphView)?.ContentViewContainer ?? e.target as VisualElement;

            var variablesToCreate = dropElements
                .OfType<IVariableDeclarationModel>()
                .Select((e1, i) => (
                    e1,
                    (SerializableGUID)GUID.Generate(),
                    contentViewContainer.WorldToLocal(e.mousePosition) + i * DragDropSpacer * Vector2.down))
                .ToList();

            var droppedNodes = dropElements.OfType<INodeModel>();

            if (droppedNodes.Any(e2 => !(e2 is IVariableNodeModel)) && variablesToCreate.Any())
            {
                // no way to handle this ATM
                throw new ArgumentException("Unhandled case, dropping blackboard/variables fields and nodes at the same time");
            }

            if (variablesToCreate.Any())
                Stencil.OnDragAndDropVariableDeclarations(Dispatcher, variablesToCreate);
        }
    }
}
