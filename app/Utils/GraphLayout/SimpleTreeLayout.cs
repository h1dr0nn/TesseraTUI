using System;
using System.Collections.Generic;
using System.Linq;
using Tessera.Models.Graph;

namespace Tessera.Utils.GraphLayout;

public class SimpleTreeLayout
{
    private const double HorizontalSpacing = 100;
    private const double VerticalSpacing = 20;
    private readonly Dictionary<GraphNode, double> _subtreeHeights = new();

    public void CalculateLayout(GraphNode root)
    {
        if (root == null) return;
        
        _subtreeHeights.Clear();

        // Reset positions
        ResetNode(root);

        // First pass: Calculate and cache subtree heights
        CalculateSubtreeDimension(root);

        // Second pass: operational layout (assign Y coordinates)
        AssignCoordinates(root, 0, 0);
    }

    private void ResetNode(GraphNode node)
    {
        node.X = 0;
        node.Y = 0;
        foreach (var child in node.Children)
        {
            ResetNode(child);
        }
    }

    // Returns total height of the subtree rooted at node
    private double CalculateSubtreeDimension(GraphNode node)
    {
        if (node.Children.Count == 0)
        {
            _subtreeHeights[node] = node.Height;
            return node.Height;
        }

        double totalHeight = 0;
        foreach (var child in node.Children)
        {
            totalHeight += CalculateSubtreeDimension(child);
        }
        
        // Add spacing between children
        totalHeight += (node.Children.Count - 1) * VerticalSpacing;
        
        var result = Math.Max(node.Height, totalHeight);
        _subtreeHeights[node] = result;
        return result;
    }

    private void AssignCoordinates(GraphNode node, double x, double y)
    {
        node.X = x;

        double currentY = y;
        
        if (node.Children.Count == 0)
        {
             node.Y = y;
             return;
        }

        double childX = x + node.Width + HorizontalSpacing;
        double childYCursor = y;
        
        var firstChildY = childYCursor;
        var lastChildY = childYCursor;

        for (int i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            
            // Use cached height
            double childSubtreeHeight = _subtreeHeights.TryGetValue(child, out var h) ? h : child.Height;
            
            // Recursively layout child
            AssignCoordinates(child, childX, childYCursor);
            
            if (i == 0) firstChildY = child.Y;
            if (i == node.Children.Count - 1) lastChildY = child.Y;

            childYCursor += childSubtreeHeight + VerticalSpacing;
        }

        // Center parent between first and last child
        node.Y = firstChildY + (lastChildY - firstChildY) / 2;
    }

    // GetCachedSubtreeHeight removed as it is no longer needed
}
