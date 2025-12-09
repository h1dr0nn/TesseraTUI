using System;
using System.Collections.Generic;
using System.Linq;
using Tessera.Models.Graph;

namespace Tessera.Utils.GraphLayout;

public class SimpleTreeLayout
{
    private const double HorizontalSpacing = 100;
    private const double VerticalSpacing = 20;

    public void CalculateLayout(GraphNode root)
    {
        if (root == null) return;
        
        // Reset positions
        ResetNode(root);

        // First pass: Calculate subtree heights
        CalculateSubtreeDimension(root);

        // Second pass: operational layout (assign Y coordinates)
        // Root starts at 0, 0 (or centered if we prefer)
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
            return node.Height;
        }

        double totalHeight = 0;
        foreach (var child in node.Children)
        {
            totalHeight += CalculateSubtreeDimension(child);
        }
        
        // Add spacing between children
        totalHeight += (node.Children.Count - 1) * VerticalSpacing;
        
        return Math.Max(node.Height, totalHeight);
    }

    private void AssignCoordinates(GraphNode node, double x, double y)
    {
        node.X = x;

        // Center parent relative to its children block
        // However, standard tree layouts usually align parent with the 'center' of its children's vertical span
        
        double currentY = y;
        
        // If leaf, just set Y (passed from parent logic)
        // But wait, for the recursive logic, we need to know the 'span' of this node to center it?
        // Actually, let's do this: 
        // The parent is passed a 'startX, startY'.
        // It positions its children starting at 'startX + Width + Space, startY'.
        // It positions ITSELF at 'startX, startY + (ChildrenSpan / 2) - (Height / 2)'.
        
        if (node.Children.Count == 0)
        {
             node.Y = y;
             return;
        }

        double childX = x + node.Width + HorizontalSpacing;
        double childYCursor = y;
        
        // We need to calculate the total span again to center the parent
        // (Optimizable, but fine for now to re-calculate simple sums)
        // Or we rely on the fact that we are passing 'y' as the top of the bounding box for this subtree
        
        var firstChildY = childYCursor;
        var lastChildY = childYCursor;

        for (int i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            
            // Calc subtree height again for this child to know how much to advance cursor
            // (In a real optimized version we'd cache this from the first pass)
            double childSubtreeHeight = GetCachedSubtreeHeight(child); 
            
            // We want the child to be centered in ITS allocated vertical space
            // childYCursor is the TOP of the space allocated for this child's subtree
            
            // Recursively layout child
            AssignCoordinates(child, childX, childYCursor);
            
            // The child itself ends up at child.Y. 
            // We want to track the Y of the first and last child Node (not subtree top/bottom) to center parent
            if (i == 0) firstChildY = child.Y;
            if (i == node.Children.Count - 1) lastChildY = child.Y;

            childYCursor += childSubtreeHeight + VerticalSpacing;
        }

        // Center parent between first and last child
        node.Y = firstChildY + (lastChildY - firstChildY) / 2;
    }

    private double GetCachedSubtreeHeight(GraphNode node)
    {
        if (node.Children.Count == 0) return node.Height;
        double h = 0;
        foreach(var c in node.Children) h += GetCachedSubtreeHeight(c);
        h += (node.Children.Count - 1) * VerticalSpacing;
        return Math.Max(node.Height, h);
    }
}
