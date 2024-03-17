using AngleSharp.Dom;
using KristofferStrube.Blazor.SVGEditor;
using Microsoft.AspNetCore.Components.Web;
using System.Xml.Linq;

namespace KristofferStrube.Blazor.GraphEditor;

public class Edge<TNodeData, TEdgeData> : Line where TNodeData : IEquatable<TNodeData>
{
    public Edge(IElement element, SVGEditor.SVGEditor svg) : base(element, svg)
    {
        UpdateLine();
    }

    public override Type Presenter => typeof(EdgeEditor<TNodeData, TEdgeData>);

    public GraphEditor<TNodeData, TEdgeData> GraphEditor { get; set; }

    public TEdgeData Data { get; set; }

    public Node<TNodeData, TEdgeData> From { get; set; }

    public Node<TNodeData, TEdgeData> To { get; set; }

    public new string StrokeWidth => GraphEditor.EdgeWidthMapper(Data).AsString();

    public new string Stroke => "black";

    public override void HandlePointerMove(PointerEventArgs eventArgs)
    {
        if (SVG.EditMode is EditMode.Add)
        {
            (X2, Y2) = SVG.LocalDetransform((eventArgs.OffsetX, eventArgs.OffsetY));
            SetStart((X2, Y2));
        }
    }

    public override void HandlePointerUp(PointerEventArgs eventArgs)
    {
        if (SVG.EditMode is EditMode.Add
            && SVG.SelectedShapes.FirstOrDefault(s => s is Node<TNodeData, TEdgeData> node && node != From) is Node<TNodeData, TEdgeData> { } to)
        {
            if (to.Edges.Any(c => c.To == From || c.From == From))
            {
                Complete();
            }
            else
            {
                To = to;
                SVG.EditMode = EditMode.None;
                UpdateLine();
            }
        }
    }

    public override void Complete()
    {
        if (To is null)
        {
            SVG.RemoveElement(this);
            Changed?.Invoke(this);
        }
    }

    public void SetStart((double x, double y) towards)
    {
        double differenceX = towards.x - From!.NodeElement.Cx;
        double differenceY = towards.y - From!.NodeElement.Cy;
        double distance = Math.Sqrt((differenceX * differenceX) + (differenceY * differenceY));

        if (distance > 0)
        {
            X1 = From!.NodeElement.Cx + (differenceX / distance * From.NodeElement.Radius);
            Y1 = From!.NodeElement.Cy + (differenceY / distance * From.NodeElement.Radius);
        }
    }

    public void UpdateLine()
    {
        if (From is null || To is null)
        {
            (X1, Y1) = (X2, Y2);
            return;
        }

        double differenceX = To.NodeElement.Cx - From.NodeElement.Cx;
        double differenceY = To.NodeElement.Cy - From.NodeElement.Cy;
        double distance = Math.Sqrt((differenceX * differenceX) + (differenceY * differenceY));

        if (distance < To.NodeElement.Radius + From.NodeElement.Radius + GraphEditor.EdgeWidthMapper(Data) * 3)
        {
            (X1, Y1) = (X2, Y2);
        }
        else
        {
            SetStart((To.NodeElement.Cx, To.NodeElement.Cy));
            X2 = To.NodeElement.Cx - (differenceX / distance * (To.NodeElement.Radius + GraphEditor.EdgeWidthMapper(Data) * 3));
            Y2 = To.NodeElement.Cy - (differenceY / distance * (To.NodeElement.Radius + GraphEditor.EdgeWidthMapper(Data) * 3));
        }
    }

    public static Edge<TNodeData, TEdgeData> AddNew(
        SVGEditor.SVGEditor SVG,
        GraphEditor<TNodeData, TEdgeData> graphEditor,
        TEdgeData data,
        Node<TNodeData, TEdgeData> from,
        Node<TNodeData, TEdgeData> to)
    {
        IElement element = SVG.Document.CreateElement("LINE");
        element.SetAttribute("data-elementtype", "edge");

        Edge<TNodeData, TEdgeData> edge = new(element, SVG)
        {
            Id = graphEditor.NodeIdMapper(from.Data) + "-" + graphEditor.NodeIdMapper(to.Data),
            Changed = SVG.UpdateInput,
            GraphEditor = graphEditor,
            Data = data,
            From = from,
            To = to
        };
        from.Edges.Add(edge);
        to.Edges.Add(edge);
        from.NeighborNodes[to] = edge;
        to.NeighborNodes[from] = edge;

        SVG.Elements.Add(edge);
        return edge;
    }

    public void Remove()
    {
        SVG.RemoveElement(this);
    }
}