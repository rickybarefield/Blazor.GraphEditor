using AngleSharp.Dom;
using KristofferStrube.Blazor.SVGEditor;

namespace KristofferStrube.Blazor.GraphEditor;

public interface INodeElement<TNodeData, TEdgeData> : ISVGElement where TNodeData : IEquatable<TNodeData> 
{
    public double Cx { get; set; }

    public double Cy { get; set; }

    public double Radius { get; }
}