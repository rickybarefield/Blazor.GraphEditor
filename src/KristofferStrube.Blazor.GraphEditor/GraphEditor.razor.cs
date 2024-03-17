using AngleSharp.Dom;
using KristofferStrube.Blazor.SVGEditor;
using Microsoft.AspNetCore.Components;
using System.Globalization;
using System.Xml.Linq;
using static System.Formats.Asn1.AsnWriter;

namespace KristofferStrube.Blazor.GraphEditor;

public partial class GraphEditor<TNode, TEdge> : ComponentBase where TNode : IEquatable<TNode>
{
    private GraphEditorCallbackContext callbackContext = default!;
    private Node<TNode, TEdge>[] nodeElements = [];
    private string EdgeId(TEdge e)
    {
        return NodeIdMapper(EdgeFromMapper(e)) + "-" + NodeIdMapper(EdgeToMapper(e));
    }

    [Parameter, EditorRequired]
    public required Func<TNode, string> NodeIdMapper { get; set; }

    [Parameter]
    public Func<TNode, string> NodeColorMapper { get; set; } = _ => "#66BB6A";

    [Parameter]
    public Func<TNode, double> NodeRadiusMapper { get; set; } = _ => 50;

    [Parameter]
    public Func<TNode, double> NodeRepulsionMapper { get; set; } = _ => 800;

    [Parameter, EditorRequired]
    public required Func<TEdge, TNode> EdgeFromMapper { get; set; }

    [Parameter, EditorRequired]
    public required Func<TEdge, TNode> EdgeToMapper { get; set; }

    [Parameter]
    public Func<TEdge, double> EdgeWidthMapper { get; set; } = _ => 1;

    [Parameter]
    public Func<TEdge, double> EdgeSpringConstantMapper { get; set; } = _ => 1;

    [Parameter]
    public Func<TEdge, double> EdgeSpringLengthMapper { get; set; } = _ => 200;

    [Parameter]
    public Func<TNode, Task>? NodeSelectionCallback { get; set; }

    public bool IsReadyToLoad => SVGEditor.BBox is not null;

    protected override void OnInitialized()
    {
        callbackContext = new()
        {
            NodeSelectionCallback = async (id) =>
            {
                if (NodeSelectionCallback is not null && Nodes.TryGetValue(id, out TNode? node))
                {
                    await NodeSelectionCallback.Invoke(node);
                }
            }
        };
    }

    public async Task LoadGraph(List<TNode> nodes, List<TEdge> edges)
    {
        if (SVGEditor.BBox is not null)
        {
            SVGEditor.Translate = (SVGEditor.BBox.Width / 2, SVGEditor.BBox.Height / 2);
        }
        else
        {
            SVGEditor.Translate = (200, 200);
        }

        Nodes = nodes.ToDictionary(NodeIdMapper, n => n);
        Edges = edges.ToDictionary(EdgeId, e => e);

        Dictionary<TNode, Node<TNode, TEdge>> nodeElementDictionary = [];

        foreach (TNode node in nodes)
        {
            Node<TNode, TEdge> element = Node<TNode, TEdge>.AddNew(SVGEditor, this, node);
            element.NodeElement.Cx = Random.Shared.NextDouble() * 20;
            element.NodeElement.Cy = Random.Shared.NextDouble() * 20;
            nodeElementDictionary.Add(node, element);
        }
        foreach (TEdge edge in edges)
        {
            Edge<TNode, TEdge>.AddNew(SVGEditor, this, edge, nodeElementDictionary[EdgeFromMapper(edge)], nodeElementDictionary[EdgeToMapper(edge)]);
        }

        SVGEditor.SelectedShapes = SVGEditor.Elements.Where(e => e is Edge<TNode, TEdge>).Select(e => (Shape)e).ToList();
        SVGEditor.MoveToBack();
        SVGEditor.ClearSelectedShapes();

        await Task.Yield();
        StateHasChanged();
        nodeElements = SVGEditor.Elements.Where(e => e is INodeElement<TNode, TEdge>).Select(e => ((INodeElement<TNode, TEdge>)e).Node).ToArray();
    }


    public async Task UpdateGraph(List<TNode> nodes, List<TEdge> edges)
    {
        Dictionary<TNode, Node<TNode, TEdge>> newNodeElementDictionary = [];

        foreach (TNode node in nodes)
        {
            if (!Nodes.ContainsKey(NodeIdMapper(node)))
            {
                Node<TNode, TEdge> element = Node<TNode, TEdge>.CreateNew(SVGEditor, this, node);
                newNodeElementDictionary.Add(node, element);
                Nodes.Add(NodeIdMapper(node), node);
            }
        }
        foreach (TEdge edge in edges)
        {
            if (!Edges.ContainsKey(EdgeId(edge)))
            {
                TNode from = EdgeFromMapper(edge);
                TNode to = EdgeToMapper(edge);
                Node<TNode, TEdge> fromElement = newNodeElementDictionary.TryGetValue(from, out var eFrom) ? eFrom : nodeElements.First(n => n.Data.Equals(from));
                Node<TNode, TEdge> toElement = newNodeElementDictionary.TryGetValue(to, out var eTo) ? eTo : nodeElements.First(n => n.Data.Equals(to));
                Edge<TNode, TEdge>.AddNew(SVGEditor, this, edge, fromElement, toElement);
                Edges.Add(EdgeId(edge), edge);
            }
        }
        foreach (var newNodeElement in newNodeElementDictionary.Values)
        {
            if (newNodeElement.Edges.Count is 0)
            {
                newNodeElement.NodeElement.Cx = Random.Shared.NextDouble() * 20;
                newNodeElement.NodeElement.Cy = Random.Shared.NextDouble() * 20;
            }
            else
            {
                double newX = 0;
                double newY = 0;
                foreach (var neighborEdge in newNodeElement.Edges)
                {
                    var neighborMirroredPosition = MirroredAverageOfNeighborNodes(neighborEdge, newNodeElement);
                    newX += neighborMirroredPosition.x / newNodeElement.Edges.Count();
                    newY += neighborMirroredPosition.y / newNodeElement.Edges.Count();
                }
                newNodeElement.NodeElement.Cx = newX;
                newNodeElement.NodeElement.Cy = newY;
            }
            SVGEditor.AddElement(newNodeElement.NodeElement);
        }
        nodeElements = SVGEditor.Elements.Where(e => e is INodeElement<TNode, TEdge>).Select(e => ((INodeElement<TNode, TEdge>)e).Node).ToArray();

        foreach (Edge<TNode, TEdge> edge in SVGEditor.Elements.Where(e => e is Edge<TNode, TEdge>).Cast<Edge<TNode, TEdge>>())
        {
            edge.UpdateLine();
        }
    }

    private (double x, double y) MirroredAverageOfNeighborNodes(Edge<TNode, TEdge> neighborEdge, Node<TNode, TEdge> rootNode)
    {
        var neighborNode = neighborEdge.From == rootNode ? neighborEdge.To : neighborEdge.From;
        var neighborsNeighbors = neighborNode.Edges.Select(e => e.From == neighborNode ? e.To : e.From).Where(n => n != rootNode).ToArray();

        var edgeLength = EdgeSpringLengthMapper(neighborEdge.Data);

        if (neighborsNeighbors.Length is 0)
        {
            var randomAngle = neighborNode.NodeElement.Cx + Random.Shared.NextDouble() * Math.PI;
            return (
                x: neighborNode.NodeElement.Cx + Math.Sin(randomAngle) * edgeLength,
                y: neighborNode.NodeElement.Cy + Math.Cos(randomAngle) * edgeLength
            );
        }
        else
        {
            double averageXPositionOfNeighborsNeighbors = 0;
            double averageYPositionOfNeighborsNeighbors = 0;

            foreach (var neighborsNeighborNode in neighborsNeighbors)
            {
                averageXPositionOfNeighborsNeighbors += neighborsNeighborNode.NodeElement.Cx / neighborsNeighbors.Length;
                averageYPositionOfNeighborsNeighbors += neighborsNeighborNode.NodeElement.Cy / neighborsNeighbors.Length;
            }
            // TODO: Handle the case where averagePositionOfNeighborsNeighbors==neighborsPosition;
            var differenceBetweenAverageNeighborsNeighborsAndNeighbor = (
                x: averageXPositionOfNeighborsNeighbors - neighborNode.NodeElement.Cx,
                y: averageYPositionOfNeighborsNeighbors - neighborNode.NodeElement.Cy
            );
            var distanceBetweenAverageNeighborsNeighborsAndNeighbor = Math.Sqrt(Math.Pow(differenceBetweenAverageNeighborsNeighborsAndNeighbor.x, 2) + Math.Pow(differenceBetweenAverageNeighborsNeighborsAndNeighbor.y, 2));
            var normalizedVectorBetweenAverageNeighborsNeighborsAndNeighbor = (
                x: differenceBetweenAverageNeighborsNeighborsAndNeighbor.x / distanceBetweenAverageNeighborsNeighborsAndNeighbor,
                y: differenceBetweenAverageNeighborsNeighborsAndNeighbor.y / distanceBetweenAverageNeighborsNeighborsAndNeighbor
            );

            return (
                x: neighborNode.NodeElement.Cx - normalizedVectorBetweenAverageNeighborsNeighborsAndNeighbor.x * edgeLength,
                y: neighborNode.NodeElement.Cy - normalizedVectorBetweenAverageNeighborsNeighborsAndNeighbor.y * edgeLength
            );
        }
    }

    public Task ForceDirectedLayout()
    {
        for (int i = 0; i < nodeElements.Length; i++)
        {
            Node<TNode, TEdge> node1 = nodeElements[i];
            double mx = 0;
            double my = 0;
            for (int j = 0; j < nodeElements.Length; j++)
            {
                if (i == j)
                {
                    continue;
                }

                Node<TNode, TEdge> node2 = nodeElements[j];
                double dx = node1.NodeElement.Cx - node2.NodeElement.Cx;
                double dy = node1.NodeElement.Cy - node2.NodeElement.Cy;
                double d = Math.Sqrt(dx * dx + dy * dy);
                double force;
                if (node1.NeighborNodes.TryGetValue(node2, out var edge))
                {
                    force = EdgeSpringConstantMapper(edge.Data) * Math.Log(d / EdgeSpringLengthMapper(edge.Data));
                }
                else
                {
                    force = -(NodeRepulsionMapper(node1.Data) + NodeRepulsionMapper(node2.Data)) / 2 / (d * d);
                }

                mx -= dx * 0.1 * force;
                my -= dy * 0.1 * force;
            }

            var nodeElementAsShape = node1.NodeElement as Shape;

            if (nodeElementAsShape != null && !SVGEditor.SelectedShapes.Contains(nodeElementAsShape))
            {
                node1.NodeElement.Cx += mx;
                node1.NodeElement.Cy += my;
            }
        }

        foreach (Edge<TNode, TEdge> edge in SVGEditor.Elements.Where(e => e is Edge<TNode, TEdge>).Cast<Edge<TNode, TEdge>>())
        {
            edge.UpdateLine();
        }
        return Task.CompletedTask;
    }

    public void FitToShapes(double delta = 1, double padding = 20)
    {
        if (SVGEditor.BBox is null || SVGEditor.SelectedShapes.Count > 0) return;
        double lowerX = double.MaxValue, lowerY = double.MaxValue;
        double upperX = double.MinValue, upperY = double.MinValue;
        foreach (Shape shape in SVGEditor.Elements.Cast<Shape>())
        {
            foreach ((double x, double y) in shape.SelectionPoints)
            {
                double strokeWidth = double.TryParse(shape.StrokeWidth, CultureInfo.InvariantCulture, out double result) ? result : 0;
                lowerX = Math.Min(x - strokeWidth, lowerX);
                upperX = Math.Max(x + strokeWidth, upperX);
                lowerY = Math.Min(y - strokeWidth, lowerY);
                upperY = Math.Max(y + strokeWidth, upperY);
            }
        }
        double elementsWidth = upperX - lowerX;
        double elementsHeight = upperY - lowerY;
        var newScale = Math.Min((SVGEditor.BBox.Width - (padding * 2)) / elementsWidth, (SVGEditor.BBox.Height - (padding * 2)) / elementsHeight);
        (double x, double y) newTranslate = ((SVGEditor.BBox.Width / 2) - ((lowerX + (elementsWidth / 2)) * newScale), (SVGEditor.BBox.Height / 2) - ((lowerY + (elementsHeight / 2)) * newScale));
        SVGEditor.Scale = (SVGEditor.Scale * (1 - delta) + newScale * delta);
        SVGEditor.Translate = (SVGEditor.Translate.x * (1 - delta) + newTranslate.x * delta, SVGEditor.Translate.y * (1 - delta) + newTranslate.y * delta);
    }

    public void MoveEdgesToBack()
    {
        var prevSelectedShapes = SVGEditor.SelectedShapes.ToList();
        SVGEditor.ClearSelectedShapes();
        foreach (Shape shape in SVGEditor.Elements.Where(e => e is Edge<TNode, TEdge>))
        {
            SVGEditor.SelectShape(shape);
        }
        SVGEditor.MoveToBack();
        SVGEditor.SelectedShapes = prevSelectedShapes;
    }

    protected Dictionary<string, TNode> Nodes { get; set; } = [];

    protected Dictionary<string, TEdge> Edges { get; set; } = [];

    protected SVGEditor.SVGEditor SVGEditor { get; set; } = default!;

    protected string Input { get; set; } = "";

    protected List<SupportedElement> SupportedElements { get; set; } =
    [
        new(typeof(Node<TNode, TEdge>), element => element.TagName is "CIRCLE" && element.GetAttribute("data-elementtype") == "node"),
        new(typeof(Edge<TNode, TEdge>), element => element.TagName is "LINE" && element.GetAttribute("data-elementtype") == "edge"),
    ];
}