using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NUnit.Framework;
using UnityEditor.GraphToolsFoundation.Overdrive.GraphElements;
using UnityEditor.GraphToolsFoundation.Overdrive.VisualScripting;
using UnityEditor.GraphToolsFoundation.Overdrive.VisualScripting.Compilation;
using UnityEditor.GraphToolsFoundation.Overdrive.VisualScripting.GraphViewModel;
using UnityEditor.GraphToolsFoundation.Overdrive.VisualScripting.SmartSearch;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEditor.GraphToolsFoundation.Overdrive.Tests.UI
{
    class DependencyTests : BaseUIFixture
    {
        [UsedImplicitly]
        [Serializable]
        class EntryPointNodeModel : NodeModel, IFakeNode
        {
            public PortModel ExecOut0 { get; private set; }

            protected override void OnDefineNode()
            {
                ExecOut0 = AddExecutionOutputPort("execOut0");
            }
        }

        [UsedImplicitly]
        [Serializable]
        class ExecNodeModel : NodeModel, IFakeNode
        {
            public PortModel ExecIn0 { get; private set; }
            public PortModel DataIn0 { get; private set; }

            protected override void OnDefineNode()
            {
                ExecIn0 = AddExecutionInputPort("execIn0");
                DataIn0 = AddDataInputPort<int>("dataIn0");
            }
        }

        [UsedImplicitly]
        [Serializable]
        class DataNodeModel : NodeModel, IFakeNode
        {
            public PortModel DataOut0 { get; private set; }

            protected override void OnDefineNode()
            {
                DataOut0 = AddDataOutputPort<int>("dataOut0");
            }
        }


        [Serializable]
        class TestStencil : Stencil
        {
            public override ISearcherDatabaseProvider GetSearcherDatabaseProvider()
            {
                return new ClassSearcherDatabaseProvider(this);
            }

            public override IBuilder Builder => null;

            public override IEnumerable<INodeModel> GetEntryPoints(VSGraphModel vsGraphModel)
            {
                return vsGraphModel.NodeModels.OfType<EntryPointNodeModel>();
            }

            // Lifted almost verbatim from DotsStencil
            public override bool CreateDependencyFromEdge(IEdgeModel model, out LinkedNodesDependency linkedNodesDependency, out INodeModel parent)
            {
                var outputNode = model.OutputPortModel.NodeModel;
                var inputNode = model.InputPortModel.NodeModel;
                bool outputIsData = IsDataNode(outputNode);
                bool inputIsData = IsDataNode(inputNode);
                if (outputIsData)
                {
                    parent = inputNode;
                    linkedNodesDependency = new LinkedNodesDependency
                    {
                        count = 1,
                        DependentPort = model.OutputPortModel,
                        ParentPort = model.InputPortModel,
                    };
                    return true;
                }
                if (!inputIsData)
                {
                    parent = outputNode;
                    linkedNodesDependency = new LinkedNodesDependency
                    {
                        count = 1,
                        DependentPort = model.InputPortModel,
                        ParentPort = model.OutputPortModel,
                    };
                    return true;
                }

                linkedNodesDependency = default;
                parent = default;
                return false;
            }

            // Lifted verbatim from DotsStencil
            public override IEnumerable<IEdgePortalModel> GetPortalDependencies(IEdgePortalModel model)
            {
                switch (model)
                {
                    case ExecutionEdgePortalEntryModel _:
                        return ((VariableDeclarationModel)model.DeclarationModel).FindReferencesInGraph()
                            .OfType<IEdgePortalExitModel>();
                    case DataEdgePortalExitModel _:
                        return ((VariableDeclarationModel)model.DeclarationModel).FindReferencesInGraph()
                            .OfType<IEdgePortalEntryModel>();
                    default:
                        return Enumerable.Empty<IEdgePortalModel>();
                }
            }

            // Lifted almost verbatim from DotsModelExtensions
            static bool IsDataNode(INodeModel nodeModel)
            {
                switch (nodeModel)
                {
                    case EntryPointNodeModel _:
                    case ExecNodeModel _:
                        return false;
                    case DataNodeModel _:
                        return true;
                    case DataEdgePortalEntryModel _:
                    case DataEdgePortalExitModel _:
                        return true;
                    case ExecutionEdgePortalEntryModel _:
                    case ExecutionEdgePortalExitModel _:
                        return false;
                    default:
                        throw new ArgumentException("Unknown node model");
                }
            }
        }

        protected override bool CreateGraphOnStartup => true;
        protected override Type CreatedGraphType => typeof(TestStencil);

        void TestEntryDependencies(IEdgePortalEntryModel entryModel, ExecutionEdgePortalExitModel[] exitModels)
        {
            Assert.AreEqual(exitModels.Length, GraphView.PositionDependenciesManagers.GetPortalDependencies(entryModel).Count);
            var dependencyModels = GraphView.PositionDependenciesManagers.GetPortalDependencies(entryModel)
                .Select(d => d.DependentNode).ToList();
            foreach (var exitModel in exitModels)
            {
                Assert.AreEqual(0, GraphView.PositionDependenciesManagers.GetPortalDependencies(exitModel).Count);
                Assert.IsTrue(dependencyModels.Contains(exitModel));
            }
        }

        [UnityTest]
        public IEnumerator AddingPortalToGraphAddsToDependencyManager()
        {
            var portalDecl = GraphModel.CreateGraphPortalDeclaration("Portal");

            // Create a entry portal
            var portalEntry = GraphModel.CreateNode<ExecutionEdgePortalEntryModel>("Portal", Vector2.zero);
            portalEntry.DeclarationModel = portalDecl;
            Store.Dispatch(new RefreshUIAction(UpdateFlags.All));
            yield return null;
            TestEntryDependencies(portalEntry, new ExecutionEdgePortalExitModel[0]);

            // Create a first exit portal connected to the entry
            var portalExit = (ExecutionEdgePortalExitModel)GraphModel.CreateOppositePortal(portalEntry);
            Store.Dispatch(new RefreshUIAction(UpdateFlags.All));
            yield return null;
            TestEntryDependencies(portalEntry, new[] {portalExit});

            // Create a second exit portal connected to the entry
            var portalExit2 = (ExecutionEdgePortalExitModel)GraphModel.CreateOppositePortal(portalEntry);
            Store.Dispatch(new RefreshUIAction(UpdateFlags.All));
            yield return null;
            TestEntryDependencies(portalEntry, new[] {portalExit, portalExit2});

            // Create a second entry for the existing exits
            var portalEntry2 = (ExecutionEdgePortalEntryModel)GraphModel.CreateOppositePortal(portalExit);
            Store.Dispatch(new RefreshUIAction(UpdateFlags.All));
            yield return null;
            TestEntryDependencies(portalEntry, new[] {portalExit, portalExit2});
            TestEntryDependencies(portalEntry2, new[] {portalExit, portalExit2});
        }

        [UnityTest]
        public IEnumerator RemovingPortalFromGraphRemovesFromDependencyManager()
        {
            var portalDecl = GraphModel.CreateGraphPortalDeclaration("Portal");

            // Create our portals as we know they work from AddingPortalToGraphAddsToDependencyManager
            var portalEntry = GraphModel.CreateNode<ExecutionEdgePortalEntryModel>("Portal", Vector2.zero);
            portalEntry.DeclarationModel = portalDecl;
            var portalExit = (ExecutionEdgePortalExitModel)GraphModel.CreateOppositePortal(portalEntry);
            var portalExit2 = (ExecutionEdgePortalExitModel)GraphModel.CreateOppositePortal(portalEntry);
            var portalEntry2 = (ExecutionEdgePortalEntryModel)GraphModel.CreateOppositePortal(portalExit);
            Store.Dispatch(new RefreshUIAction(UpdateFlags.All));
            yield return null;
            TestEntryDependencies(portalEntry, new[] {portalExit, portalExit2});
            TestEntryDependencies(portalEntry2, new[] {portalExit, portalExit2});

            // Delete the second entry portal. Attempting to get its dependencies should return null
            GraphModel.DeleteNode(portalEntry2, VisualScripting.GraphViewModel.GraphModel.DeleteConnections.True);
            Store.Dispatch(new RefreshUIAction(UpdateFlags.All));
            yield return null;
            TestEntryDependencies(portalEntry, new[] {portalExit, portalExit2});
            Assert.IsNull(GraphView.PositionDependenciesManagers.GetPortalDependencies(portalEntry2));

            // Delete the second exit.
            GraphModel.DeleteNode(portalExit2, VisualScripting.GraphViewModel.GraphModel.DeleteConnections.True);
            Store.Dispatch(new RefreshUIAction(UpdateFlags.All));
            yield return null;
            TestEntryDependencies(portalEntry, new[] {portalExit});
            Assert.IsNull(GraphView.PositionDependenciesManagers.GetPortalDependencies(portalEntry2));

            // Delete the first exit. There should be no dependencies to the remaining entry
            GraphModel.DeleteNode(portalExit, VisualScripting.GraphViewModel.GraphModel.DeleteConnections.True);
            Store.Dispatch(new RefreshUIAction(UpdateFlags.All));
            yield return null;
            TestEntryDependencies(portalEntry, new ExecutionEdgePortalExitModel[0]);
            Assert.IsNull(GraphView.PositionDependenciesManagers.GetPortalDependencies(portalEntry2));

            // Delete the first entry. There should be no more dependencies registered in the manager.
            GraphModel.DeleteNode(portalEntry, VisualScripting.GraphViewModel.GraphModel.DeleteConnections.True);
            Store.Dispatch(new RefreshUIAction(UpdateFlags.All));
            yield return null;
            Assert.IsNull(GraphView.PositionDependenciesManagers.GetPortalDependencies(portalEntry));
            Assert.IsNull(GraphView.PositionDependenciesManagers.GetPortalDependencies(portalEntry2));
        }

        [UnityTest]
        public IEnumerator PortalsAreHandledInGraphDependencyTraversal()
        {
            // The setup:
            //
            // +--------+   +------------/    /----------+   +-------+
            // | Entry  #---# ExePortal /    / ExePortal #---#       |
            // +--------+   +----------/    /------------+   |       |
            //                                               | Node0 |
            // +------+   +-------------/    /-----------+   |       |
            // | Data o---o DataPortal /    / DataPortal o---o       |
            // +------+   +-----------/    /-------------+   +-------+
            //

            var exePortalDecl = GraphModel.CreateGraphPortalDeclaration("Exe Portal");
            var dataPortalDecl = GraphModel.CreateGraphPortalDeclaration("Data Portal");

            var entryNode = GraphModel.CreateNode<EntryPointNodeModel>("Entry", Vector2.zero);
            var dataNode = GraphModel.CreateNode<DataNodeModel>("Data");
            var node0 = GraphModel.CreateNode<ExecNodeModel>("Node0", Vector2.zero);

            var exePortalEntry = GraphModel.CreateNode<ExecutionEdgePortalEntryModel>("Trigger Portal Entry", Vector2.zero);
            exePortalEntry.DeclarationModel = exePortalDecl;
            var exePortalExit = (ExecutionEdgePortalExitModel)GraphModel.CreateOppositePortal(exePortalEntry);
            exePortalExit.Title = "Trigger Portal Exit";

            var dataPortalEntry = GraphModel.CreateNode<DataEdgePortalEntryModel>("Data Portal Entry", Vector2.zero);
            dataPortalEntry.DeclarationModel = dataPortalDecl;
            var dataPortalExit = (DataEdgePortalExitModel)GraphModel.CreateOppositePortal(dataPortalEntry);
            dataPortalExit.Title = "Data Portal Exit";

            GraphModel.CreateEdge(exePortalEntry.InputPort, entryNode.ExecOut0);
            GraphModel.CreateEdge(node0.ExecIn0, exePortalExit.OutputPort);

            GraphModel.CreateEdge(dataPortalEntry.InputPort, dataNode.DataOut0);
            GraphModel.CreateEdge(node0.DataIn0, dataPortalExit.OutputPort);

            Store.Dispatch(new RefreshUIAction(UpdateFlags.All));

            yield return null;

            var modelToNodes = GraphView.UIController.ModelsToNodeMapping;

            GraphView.PositionDependenciesManagers.UpdateNodeState(modelToNodes);

            NodeUIState UIState(IGraphElementModel model) => ((INodeState)modelToNodes[model]).UIState;

            Assert.AreEqual(NodeUIState.Enabled, UIState(entryNode), "Graph entry point node should be marked as enabled.");
            Assert.AreEqual(NodeUIState.Enabled, UIState(exePortalEntry), "Trigger entry portal should be marked as enabled.");
            Assert.AreEqual(NodeUIState.Enabled, UIState(exePortalExit), "Trigger exit portal should be marked as enabled.");
            Assert.AreEqual(NodeUIState.Enabled, UIState(node0), "Exec node should be marked as enabled.");
            Assert.AreEqual(NodeUIState.Enabled, UIState(dataPortalEntry), "Data entry portal should be marked as enabled.");
            Assert.AreEqual(NodeUIState.Enabled, UIState(dataPortalExit), "Data exit portal should be marked as enabled.");
            Assert.AreEqual(NodeUIState.Enabled, UIState(dataNode), "Data node should be marked as enabled.");
        }
    }
}