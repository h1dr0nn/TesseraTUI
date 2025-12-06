using Tessera.Core.Agents;
using Tessera.Core.Models;

namespace Tessera.Agents;

public class SchemaViewAgent
{
    private readonly DataSyncAgent _dataSyncAgent;
    private readonly UIToastAgent _toastAgent;

    public SchemaViewAgent(DataSyncAgent dataSyncAgent, UIToastAgent toastAgent)
    {
        _dataSyncAgent = dataSyncAgent;
        _toastAgent = toastAgent;
    }

    public SchemaModel Schema => _dataSyncAgent.Schema;

    public bool TryRename(int columnIndex, string newName)
    {
        if (_dataSyncAgent.TryRenameColumn(columnIndex, newName, out var error))
        {
            _toastAgent.ShowToast($"Renamed column to {newName}", ToastLevel.Success);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            _toastAgent.ShowToast(error, ToastLevel.Error);
        }

        return false;
    }

    public bool TryUpdateSchema(int columnIndex, ColumnSchema updated)
    {
        if (_dataSyncAgent.TryUpdateSchema(columnIndex, updated, out var report))
        {
            _toastAgent.ShowToast("Schema updated", ToastLevel.Success);
            return true;
        }

        var message = report.Errors.Count > 0
            ? $"Schema update failed at row {report.Errors[0].RowIndex}: {report.Errors[0].Message}"
            : "Schema update failed.";

        _toastAgent.ShowToast(message, ToastLevel.Error);
        return false;
    }
}
