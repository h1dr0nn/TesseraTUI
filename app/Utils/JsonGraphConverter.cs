using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Linq;
using Tessera.Models.Graph;

namespace Tessera.Utils;

public static class JsonGraphConverter
{
    public static GraphNode? Convert(string json)
    {
        try 
        {
            var node = JsonNode.Parse(json);
            return ConvertNode(node, "root");
        }
        catch 
        {
            return null;
        }
    }

    private static GraphNode ConvertNode(JsonNode? node, string key)
    {
        var graphNode = new GraphNode { Key = key };

        if (node is JsonObject obj)
        {
            graphNode.Type = GraphNodeType.Object;
            foreach (var property in obj)
            {
                var child = ConvertNode(property.Value, property.Key);
                graphNode.Children.Add(child);
            }
        }
        else if (node is JsonArray arr)
        {
            graphNode.Type = GraphNodeType.Array;
            int index = 0;
            foreach (var item in arr)
            {
                var child = ConvertNode(item, $"[{index}]");
                graphNode.Children.Add(child);
                index++;
            }
        }
        else if (node is JsonValue val)
        {
             graphNode.Type = GraphNodeType.Primitive;
             graphNode.Value = val.ToString();
        }
        else
        {
            graphNode.Type = GraphNodeType.Primitive;
            graphNode.Value = "null";
        }

        // Basic size estimation (can be refined with actual text measurement later)
        // Estimate width based on key checks + value length
        double textLength = (graphNode.Key.Length + (graphNode.Value?.Length ?? 0)) * 8; // approx 8px per char
        graphNode.Width = System.Math.Max(120, textLength + 40); 
        graphNode.Height = 40; // Fixed height for now

        return graphNode;
    }
}
