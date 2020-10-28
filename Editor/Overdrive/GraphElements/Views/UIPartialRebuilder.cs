using System;
using System.Collections.Generic;
using UnityEditor.GraphToolsFoundation.Overdrive.BasicModel;
using UnityEngine;

namespace UnityEditor.GraphToolsFoundation.Overdrive
{
    sealed class UIPartialRebuilder : IDisposable
    {
        int m_NumDeleted, m_NumCreated;

        HashSet<INodeModel> m_NodesToRebuild;
        HashSet<IEdgeModel> m_EdgesToRebuild;
        HashSet<IEdgeModel> m_EdgesToDelete;
        HashSet<IGraphElementModel> m_OtherElementsToRebuild;
        HashSet<GraphElement> m_GraphElementsToDelete;

        readonly State m_CurrentState;
        readonly Func<IGraphElementModel, GraphElement> m_CreateElement;
        readonly Action<GraphElement> m_DeleteElement;

        public string DebugOutput => $"Graph UI: -{m_NumDeleted}/+{m_NumCreated} elements" + (BlackboardChanged ? " (+full blackboard)" : "");
        public bool AnyChangeMade => m_NumDeleted > 0 || m_NumCreated > 0;
        public bool BlackboardChanged { get; set; }

        public UIPartialRebuilder(State currentState, Func<IGraphElementModel, GraphElement> createElement, Action<GraphElement> deleteElement)
        {
            m_NumDeleted = 0;
            m_NumCreated = 0;
            BlackboardChanged = false;

            m_NodesToRebuild = new HashSet<INodeModel>();
            m_EdgesToRebuild = new HashSet<IEdgeModel>();
            m_EdgesToDelete = new HashSet<IEdgeModel>();
            m_OtherElementsToRebuild = new HashSet<IGraphElementModel>();
            m_GraphElementsToDelete = new HashSet<GraphElement>();

            m_CurrentState = currentState;
            m_CreateElement = createElement;
            m_DeleteElement = deleteElement;
        }

        public void Dispose()
        {
            m_NodesToRebuild.Clear();
            m_EdgesToRebuild.Clear();
            m_EdgesToDelete.Clear();
            m_OtherElementsToRebuild.Clear();
            m_GraphElementsToDelete.Clear();
        }

        public void ComputeChanges(GraphChangeList graphChangeList, Dictionary<IGraphElementModel, GraphElement> existingElements)
        {
            BlackboardChanged = graphChangeList.BlackBoardChanged;

            GetChangesFromChangelist(graphChangeList);

            GatherDeletedElements(existingElements, graphChangeList);

            UpdateEdgesToRebuildFromNodesToRebuild();

            RemoveDeletedModelsFromRebuildLists(existingElements);

            MarkEdgesToBeRebuiltToDelete(existingElements);
        }

        void MarkEdgesToBeRebuiltToDelete(Dictionary<IGraphElementModel, GraphElement> existingElements)
        {
            foreach (var edgeModel in m_EdgesToRebuild)
            {
                if (existingElements.ContainsKey(edgeModel))
                {
                    m_GraphElementsToDelete.Add(existingElements[edgeModel]);
                }
            }
        }

        void GetChangesFromChangelist(GraphChangeList graphChanges)
        {
            foreach (var model in graphChanges.ChangedElements)
            {
                if (model is IEdgeModel edgeModel)
                {
                    if (graphChanges.DeletedEdges.Contains(edgeModel))
                        m_EdgesToDelete.Add(edgeModel);
                    else
                        m_EdgesToRebuild.Add(edgeModel);
                }
                else if (model is INodeModel nodeModel)
                {
                    m_NodesToRebuild.Add(nodeModel);
                    if (nodeModel is IVariableNodeModel variableModel && variableModel.VariableDeclarationModel == null)
                    {
                        // In particular, ThisNodeModel sometimes requires an update of the blackboard
                        BlackboardChanged = true;
                    }
                }
                else if (model is IVariableDeclarationModel)
                {
                    BlackboardChanged = true;
                }
                else if (model is IStickyNoteModel)
                {
                    m_OtherElementsToRebuild.Add(model);
                }
                else if (model is IPlacematModel)
                {
                    m_OtherElementsToRebuild.Add(model);
                }
                else
                {
                    Debug.LogWarning($"Unexpected model to rebuild: {model.GetType().Name}, make sure it is correctly supported by UI Partial Rebuild.");
                    m_OtherElementsToRebuild.Add(model);
                }
            }
        }

        void GatherDeletedElements(Dictionary<IGraphElementModel, GraphElement> existingElements, GraphChangeList graphChangeList)
        {
            foreach (var elementModel in existingElements.Keys)
            {
                if ((elementModel as IDestroyable)?.Destroyed ?? false)
                {
                    var graphElement = existingElements[elementModel];
                    if (elementModel is INodeModel node)
                    {
                        m_EdgesToDelete.AddRange(node.GetConnectedEdges());
                    }
                    m_GraphElementsToDelete.Add(graphElement);
                }
            }

            foreach (var edge in graphChangeList.DeletedEdges)
            {
                if (existingElements.TryGetValue(edge, out GraphElement edgeGraphElement))
                    m_GraphElementsToDelete.Add(edgeGraphElement);
            }
        }

        public void DeleteEdgeModels()
        {
            var graphModel = m_CurrentState?.CurrentGraphModel;
            if (graphModel != null)
            {
                foreach (var edgeModel1 in m_EdgesToDelete)
                {
                    var edgeModel = (EdgeModel)edgeModel1;
                    graphModel.DeleteEdge(edgeModel);
                }
            }
        }

        void UpdateEdgesToRebuildFromNodesToRebuild()
        {
            foreach (INodeModel nodeModel in m_NodesToRebuild)
            {
                if (!nodeModel.Destroyed)
                {
                    foreach (var edgeModel in nodeModel.GetConnectedEdges())
                    {
                        m_EdgesToRebuild.Add(edgeModel);
                    }
                }
            }
        }

        void RemoveDeletedModelsFromRebuildLists(Dictionary<IGraphElementModel, GraphElement> existingElements)
        {
            m_EdgesToRebuild.RemoveWhere(e => existingElements.ContainsKey(e) && m_GraphElementsToDelete.Contains(existingElements[e]));
            m_NodesToRebuild.RemoveWhere(n => existingElements.ContainsKey(n) && m_GraphElementsToDelete.Contains(existingElements[n]));
        }

        public void DeleteGraphElements()
        {
            foreach (GraphElement graphElement in m_GraphElementsToDelete)
            {
                m_DeleteElement(graphElement);
                m_NumDeleted++;
            }
        }

        public void RebuildNodes(Dictionary<IGraphElementModel, GraphElement> existingElements)
        {
            foreach (var nodeModel in m_NodesToRebuild)
            {
                // delete node if existing
                if (existingElements.TryGetValue(nodeModel, out var oldElement))
                {
                    m_DeleteElement(oldElement);
                    m_NumDeleted++;
                }

                // create node
                GraphElement element = m_CreateElement(nodeModel);
                existingElements[nodeModel] = element;
                if (element != null)
                {
                    m_NumCreated++;
                }
            }

            foreach (var elementModel in m_OtherElementsToRebuild)
            {
                // delete node if existing
                if (existingElements.TryGetValue(elementModel, out var oldElement))
                {
                    m_DeleteElement(oldElement);
                    m_NumDeleted++;
                }

                // create node
                if (m_CreateElement(elementModel) != null)
                {
                    m_NumCreated++;
                }
            }
        }

        public void RebuildEdges(Action<IEdgeModel> rebuildEdge)
        {
            foreach (IEdgeModel edgeModel in m_EdgesToRebuild)
            {
                rebuildEdge(edgeModel);
                m_NumCreated++;
            }
        }
    }
}