using AngleSharp.Dom;

namespace KristofferStrube.Blazor.GraphEditor;

public class Node<TNodeData, TEdgeData> where TNodeData : IEquatable<TNodeData>
{
    public Node()
    {
        GraphEditor = default!;
        Data = default!;
        NodeElement = default!;
    }


    public GraphEditor<TNodeData, TEdgeData> GraphEditor { get; set; }

    public TNodeData Data { get; set; }


    public HashSet<Edge<TNodeData, TEdgeData>> Edges { get; } = new();

    public Dictionary<Node<TNodeData, TEdgeData>, Edge<TNodeData, TEdgeData>> NeighborNodes { get; } = new();

    public string? Id { get; set; }

    public INodeElement<TNodeData, TEdgeData> NodeElement { get; set; }

    public string ColorMapper => GraphEditor.NodeColorMapper(Data);
    
    public double RadiusMapper => GraphEditor.NodeRadiusMapper(Data);


    public void UpdateEdges()
    {
        foreach (Edge<TNodeData, TEdgeData> edge in Edges)
        {
            edge.UpdateLine();
        }
    }

    public void RemoveEdges()
    {
        foreach (Edge<TNodeData, TEdgeData> edge in Edges)
        {
            edge.Remove();
        }

    }

    public static Node<TNodeData, TEdgeData> AddNew(SVGEditor.SVGEditor SVG, GraphEditor<TNodeData, TEdgeData> graphEditor, TNodeData data)
    {

        var node = CreateNew(SVG, graphEditor, data);

        SVG.Elements.Add(node.NodeElement);
        SVG.Document.GetElementsByTagName("BODY")[0].AppendElement(node.NodeElement.Element);
        return node;
    }

    public static Node<TNodeData, TEdgeData> CreateNew(SVGEditor.SVGEditor SVG, GraphEditor<TNodeData, TEdgeData> graphEditor, TNodeData data)
    {
        IElement element = SVG.Document.CreateElement("CIRCLE");
        element.SetAttribute("data-elementtype", "node");

        Node<TNodeData, TEdgeData> node = new()
        {
            Id = graphEditor.NodeIdMapper(data),
            GraphEditor = graphEditor,
            Data = data
        };

        node.NodeElement = new CircleNodeElement<TNodeData, TEdgeData>(element, SVG, node) {
            Changed = null
        };

        return node;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        return obj is Node<TNodeData, TEdgeData> node && Equals(node);
    }

    public bool Equals(Node<TNodeData, TEdgeData> obj)
    {
        return obj.Id?.Equals(Id) ?? false;
    }
}