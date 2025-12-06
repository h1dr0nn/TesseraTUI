using System.Collections.Generic;

namespace Tessera.Core.Models;

public class JsonModel
{
    public JsonModel(List<Dictionary<string, object?>> records)
    {
        Records = records;
    }

    public List<Dictionary<string, object?>> Records { get; }
}
