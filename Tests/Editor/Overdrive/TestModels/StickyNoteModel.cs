namespace UnityEditor.GraphToolsFoundation.Overdrive.Tests.TestModels
{
    public class StickyNoteModel : BasicModel.StickyNoteModel
    {
        IGraphModel m_GraphModel;
        public override IGraphModel GraphModel => m_GraphModel;

        public StickyNoteModel(IGraphModel graphModel)
        {
            m_GraphModel = graphModel;
        }
    }
}