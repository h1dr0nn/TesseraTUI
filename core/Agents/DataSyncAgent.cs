using Tessera.Core.Models;

namespace Tessera.Core.Agents;

public class DataSyncAgent
{
    public DataSyncAgent(TableModel table, SchemaModel schema, JsonModel json)
    {
        Table = table;
        Schema = schema;
        Json = json;
    }

    public TableModel Table { get; private set; }

    public SchemaModel Schema { get; private set; }

    public JsonModel Json { get; private set; }

    public void ApplyTableEdit(TableModel updatedTable)
    {
        Table = updatedTable;
    }

    public void ApplySchemaEdit(SchemaModel updatedSchema)
    {
        Schema = updatedSchema;
    }

    public void ApplyJsonEdit(JsonModel updatedJson)
    {
        Json = updatedJson;
    }
}
