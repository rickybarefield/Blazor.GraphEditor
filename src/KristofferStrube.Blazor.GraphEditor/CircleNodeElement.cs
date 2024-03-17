using AngleSharp.Dom;
using KristofferStrube.Blazor.SVGEditor;
using Microsoft.AspNetCore.Components.Web;

namespace KristofferStrube.Blazor.GraphEditor;

public class CircleNodeElement<TNodeData, TEdgeData> : Circle, INodeElement<TNodeData, TEdgeData> 
    where TNodeData : IEquatable<TNodeData> {

    public Node<TNodeData, TEdgeData> Node { get; init; }

    public string? Id { get; set; }


    public CircleNodeElement(IElement element, SVGEditor.SVGEditor svg, Node<TNodeData, TEdgeData> node) : base(element, svg) {

        Node = node;
    }

    public override string Fill
    {
        get
        {
            int[] parts = Stroke[1..].Chunk(2).Select(part => int.Parse(part, System.Globalization.NumberStyles.HexNumber)).ToArray();
            return "#" + string.Join("", parts.Select(part => Math.Min(255, part + 50).ToString("X2")));
        }
    }

    public override string Stroke => Node.ColorMapper;

    public override string StateRepresentation => base.StateRepresentation + Stroke;

    private double r;
    public double Radius { 
        get {
            var currentRadius = Node.RadiusMapper;
            if (currentRadius != r)
            {
                R = currentRadius;
                r = currentRadius;
            }
            return currentRadius;
        }
    }

    public override void HandlePointerMove(PointerEventArgs eventArgs)
    {
        base.HandlePointerMove(eventArgs);
        if (SVG.EditMode is EditMode.Move)
        {
            Node.UpdateEdges();            
        }
    }

    public override void BeforeBeingRemoved()
    {
        Node.RemoveEdges();
    }

    public override Type Presenter => typeof(NodeEditor<TNodeData, TEdgeData>);

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        return obj is CircleNodeElement<TNodeData, TEdgeData> node && Equals(node);
    }

    public bool Equals(CircleNodeElement<TNodeData, TEdgeData> obj)
    {
        return obj.Id?.Equals(Id) ?? false;
    }

}