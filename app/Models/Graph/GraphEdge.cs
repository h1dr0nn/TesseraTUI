using Avalonia;

namespace Tessera.Models.Graph;

public class GraphEdge
{
    public GraphNode Source { get; }
    public GraphNode Target { get; }

    public GraphEdge(GraphNode source, GraphNode target)
    {
        Source = source;
        Target = target;
    }

    public Point StartPoint => Source.CenterRight;
    public Point EndPoint => Target.CenterLeft;
}
