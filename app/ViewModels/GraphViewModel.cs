using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Tessera.Core.Agents;
using Tessera.Core.Models;
using Tessera.Models.Graph;
using Tessera.Utils;
using Tessera.Utils.GraphLayout;

namespace Tessera.ViewModels;

public class GraphViewModel : WorkspaceViewModel
{
    private readonly DataSyncAgent _dataSyncAgent;
    private readonly JsonAgent _jsonAgent;
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

    public GraphViewModel(DataSyncAgent dataSyncAgent, JsonAgent? jsonAgent = null)
    {
        _dataSyncAgent = dataSyncAgent;
        _jsonAgent = jsonAgent ?? new JsonAgent();
        
        _dataSyncAgent.TableChanged += LoadGraph;
        
        // Initial load
        LoadGraph();
    }
    
    public override string Title => "Graph View";
    public override string IconName => "mdi-sitemap";
    public override string Subtitle => "Visualize JSON dependencies";

    private void LoadGraph()
    {
        Nodes.Clear();
        Edges.Clear();

        try 
        {
            var jsonString = _jsonAgent.Serialize(_dataSyncAgent.Json);
            
            if (string.IsNullOrWhiteSpace(jsonString) || jsonString == "{}") 
            {
                 // Handle empty
                 return;
            }

            // 1. Convert
            var root = JsonGraphConverter.Convert(jsonString);
            if (root == null) return;

            // 2. Layout
            _layout.CalculateLayout(root);

            // 3. Flatten and Populate
            var flattenedNodes = new List<GraphNode>();
            var flatEdges = new List<GraphEdge>();
            
            TraverseAndCollect(root, flattenedNodes, flatEdges);

            foreach (var node in flattenedNodes) Nodes.Add(node);
            foreach (var edge in flatEdges) Edges.Add(edge);

            // 4. Update Canvas Size
            if (flattenedNodes.Any())
            {
                CanvasWidth = flattenedNodes.Max(n => n.X + n.Width) + 100;
                CanvasHeight = flattenedNodes.Max(n => n.Y + n.Height) + 100;
            }
        }
        catch (Exception ex)
        {
            // Fail silently or log?
            Console.WriteLine($"Error loading graph: {ex.Message}");
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
