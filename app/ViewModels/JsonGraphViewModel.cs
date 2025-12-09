using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Tessera.Models.Graph;
using Tessera.Utils;
using Tessera.Utils.GraphLayout;

namespace Tessera.ViewModels;

public class JsonGraphViewModel : ViewModelBase
{
    private readonly SimpleTreeLayout _layout = new();
    
    public ObservableCollection<GraphNode> Nodes { get; } = new();
    public ObservableCollection<GraphEdge> Edges { get; } = new();

    private double _canvasWidth;
    public double CanvasWidth
    {
        get => _canvasWidth;
        set => SetProperty(ref _canvasWidth, value);
    }
    
    private double _canvasHeight;
    public double CanvasHeight
    {
        get => _canvasHeight;
        set => SetProperty(ref _canvasHeight, value);
    }

    public void LoadJson(string json)
    {
        Console.WriteLine($"[Graph] Loading JSON: {json.Substring(0, Math.Min(json.Length, 50))}...");
        Nodes.Clear();
        Edges.Clear();

        if (string.IsNullOrWhiteSpace(json)) 
        {
             Console.WriteLine("[Graph] JSON is empty/null");
             return;
        }

        // 1. Convert
        var root = JsonGraphConverter.Convert(json);
        if (root == null) 
        {
             Console.WriteLine("[Graph] Conversion failed (root is null)");
             return;
        }
        Console.WriteLine($"[Graph] Root created: {root.Type} | Children: {root.Children.Count}");

        // 2. Layout
        _layout.CalculateLayout(root);

        // 3. Flatten and Populate
        var flattenedNodes = new List<GraphNode>();
        var flatEdges = new List<GraphEdge>();
        
        TraverseAndCollect(root, flattenedNodes, flatEdges);
        Console.WriteLine($"[Graph] Flattened: {flattenedNodes.Count} nodes, {flatEdges.Count} edges");

        foreach (var node in flattenedNodes) Nodes.Add(node);
        foreach (var edge in flatEdges) Edges.Add(edge);

        // 4. Update Canvas Size
        if (flattenedNodes.Any())
        {
            CanvasWidth = flattenedNodes.Max(n => n.X + n.Width) + 100;
            CanvasHeight = flattenedNodes.Max(n => n.Y + n.Height) + 100;
             Console.WriteLine($"[Graph] Canvas Size: {CanvasWidth}x{CanvasHeight}");
        }
    }

    private void TraverseAndCollect(GraphNode node, List<GraphNode> nodes, List<GraphEdge> edges)
    {
        nodes.Add(node);
        foreach (var child in node.Children)
        {
            edges.Add(new GraphEdge(node, child));
            TraverseAndCollect(child, nodes, edges);
        }
    }
}
