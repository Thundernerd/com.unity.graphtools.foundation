using System;

namespace UnityEditor.GraphToolsFoundation.Overdrive
{
    public static class NodeDataCreationExtensions
    {
        /// <summary>
        /// Creates a new node in the graph referenced in <paramref name="data"/>.
        /// </summary>
        /// <param name="data">Data containing some of the required information to create a node.</param>
        /// <param name="nodeTypeToCreate">The type of the new node to create.</param>
        /// <param name="nodeName">The name of the node to create.</param>
        /// <param name="initializationCallback">An initialization method to be called right after the node is created.</param>
        /// <returns>The newly created node.</returns>
        public static INodeModel CreateNode(this IGraphNodeCreationData data, Type nodeTypeToCreate, string nodeName = null, Action<INodeModel> initializationCallback = null)
        {
            return data.GraphModel.CreateNode(nodeTypeToCreate, nodeName, data.Position, data.Guid, initializationCallback, data.SpawnFlags);
        }

        /// <summary>
        /// Creates a new node in the graph referenced in <paramref name="data"/>.
        /// </summary>
        /// <param name="data">Data containing some of the required information to create a node.</param>
        /// <param name="nodeName">The name of the node to create.</param>
        /// <param name="initializationCallback">An initialization method to be called right after the node is created.</param>
        /// <typeparam name="T">The type of the new node to create.</typeparam>
        /// <returns>The newly created node.</returns>
        public static T CreateNode<T>(this IGraphNodeCreationData data, string nodeName = null, Action<T> initializationCallback = null) where T : class, INodeModel
        {
            return data.GraphModel?.CreateNode(nodeName, data.Position, data.Guid, initializationCallback, data.SpawnFlags);
        }

        public static INodeModel CreateVariableNode(this IGraphNodeCreationData data, IVariableDeclarationModel declarationModel)
        {
            return data.GraphModel.CreateVariableNode(declarationModel, data.Position, data.Guid, data.SpawnFlags);
        }

        public static INodeModel CreateConstantNode(this IGraphNodeCreationData data, string constantName, TypeHandle typeHandle)
        {
            return data.GraphModel.CreateConstantNode(typeHandle, constantName, data.Position, data.Guid, spawnFlags: data.SpawnFlags);
        }
    }
}
