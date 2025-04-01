using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    /// <summary>
    /// A UI component that can receive raycast events but doesn't display any image
    /// </summary>
    public class BlankImage : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            // Clear all vertices to ensure no mesh is generated
            vh.Clear();
        }
    }
}