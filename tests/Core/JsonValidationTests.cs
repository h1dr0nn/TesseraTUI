using System.Collections.Generic;
using Tessera.Core.Agents;
using Tessera.Core.Models;
using Xunit;

namespace Tessera.Tests.Core;

public class JsonValidationTests
{
    [Fact]
    public void AcceptsValidJsonAgainstSchema()
    {
        var schema = new SchemaModel(new List<ColumnSchema>
        {
            new("Name", DataType.String, false),
            new("Active", DataType.Bool, false),
            new("Score", DataType.Int, false),
        });

        const string json = "[{\"Name\":\"Alice\",\"Active\":true,\"Score\":10}]";
        var validator = new ValidationAgent();

        var result = validator.ValidateJsonText(json, schema);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Model);
        Assert.Single(result.Model!.Records);
    }

    [Fact]
    public void DetectsTypeMismatch()
    {
        var schema = new SchemaModel(new List<ColumnSchema>
        {
            new("Score", DataType.Int, false),
        });

        const string json = "[{\"Score\":1.5}]";
        var validator = new ValidationAgent();

        var result = validator.ValidateJsonText(json, schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Type == JsonValidationErrorType.TypeMismatch);
    }

    [Fact]
    public void DetectsMissingAndExtraKeys()
    {
        var schema = new SchemaModel(new List<ColumnSchema>
        {
            new("Name", DataType.String, false),
            new("Age", DataType.Int, false),
        });

        const string json = "[{\"Name\":\"Bob\",\"Nickname\":\"B\"}]";
        var validator = new ValidationAgent();

        var result = validator.ValidateJsonText(json, schema);

        Assert.False(result.IsValid);
        var missingKeyError = Assert.Single(result.Errors, e => e.Type == JsonValidationErrorType.MissingKey);
        Assert.Equal("Age", missingKeyError.Key);

        var unknownKeyError = Assert.Single(result.Errors, e => e.Type == JsonValidationErrorType.UnknownKey);
        Assert.Equal("Nickname", unknownKeyError.Key);
    }

    [Fact]
    public void BuildsDiffForAddedRemovedAndModifiedRows()
    {
        var schema = new SchemaModel(new List<ColumnSchema>
        {
            new("Name", DataType.String, false),
            new("Age", DataType.Int, false),
        });

        var agent = new JsonAgent();
        var current = new JsonModel(new List<Dictionary<string, object?>>
        {
            new()
            {
                {"Name", "Alice"},
                {"Age", 20}
            }
        });

        var updated = new JsonModel(new List<Dictionary<string, object?>>
        {
            new()
            {
                {"Name", "Alice"},
                {"Age", 21}
            },
            new()
            {
                {"Name", "Bob"},
                {"Age", 31}
            }
        });

        var diff = agent.BuildDiff(current, updated, schema);

        Assert.Contains(0, diff.ModifiedRows);
        Assert.Contains(1, diff.AddedRows);
        Assert.Empty(diff.RemovedRows);
    }
}
