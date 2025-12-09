using System;
using System.Collections.Generic;
using Avalonia;

namespace Tessera.Models.Graph;

public enum GraphNodeType
{
    Object,
    Array,
    Primitive
}

public class GraphNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public GraphNodeType Type { get; set; }
    public string Key { get; set; } = "";
    public string? Value { get; set; }
    public List<GraphNode> Children { get; set; } = new();

    // Layout properties
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    
    // Helper to get center point for edges
    public Point CenterRight => new Point(X + Width, Y + (Height / 2));
    public Point CenterLeft => new Point(X, Y + (Height / 2));
    
    public bool IsExpanded { get; set; } = true;
}
