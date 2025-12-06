using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Tessera.Agents;
using Tessera.Core.Agents;
using Tessera.Core.Models;
using Tessera.Utils;

namespace Tessera.ViewModels;

public class SchemaViewModel : WorkspaceViewModel
{
    private readonly UIToastAgent _toastAgent = new();
    private readonly SchemaViewAgent _schemaAgent;

    public SchemaViewModel(DataSyncAgent? dataSyncAgent = null)
    {
        var (table, schema, json, validator, jsonAgent) = SampleDataFactory.CreateWorkspace();
        var dataSync = dataSyncAgent ?? new DataSyncAgent(table, schema, json, validator, jsonAgent);
        _schemaAgent = new SchemaViewAgent(dataSync, _toastAgent);

        Columns = new ObservableCollection<SchemaColumnViewModel>(
            _schemaAgent.Schema.Columns.Select((c, i) => new SchemaColumnViewModel(i, c, _schemaAgent, _toastAgent)));

        RefreshCommand = new DelegateCommand(_ => RefreshFromSchema());
    }

    public override string Title => "Schema View";

    public override string Subtitle => "Define column rules and validation";

    public static IReadOnlyList<DataType> AvailableTypes { get; } = Enum.GetValues<DataType>().ToList();

    public ObservableCollection<SchemaColumnViewModel> Columns { get; }

    public ICommand RefreshCommand { get; }

    public UIToastAgent ToastAgent => _toastAgent;

    private void RefreshFromSchema()
    {
        Columns.Clear();
        foreach (var (column, index) in _schemaAgent.Schema.Columns.Select((c, i) => (c, i)))
        {
            Columns.Add(new SchemaColumnViewModel(index, column, _schemaAgent, _toastAgent));
        }
    }

}

public class SchemaColumnViewModel : ViewModelBase
{
    private readonly int _index;
    private readonly SchemaViewAgent _schemaAgent;
    private readonly UIToastAgent _toastAgent;
    private string _name;
    private DataType _type;
    private bool _isNullable;
    private double? _min;
    private double? _max;
    private bool _isEdited;

    public SchemaColumnViewModel(int index, ColumnSchema schema, SchemaViewAgent schemaAgent, UIToastAgent toastAgent)
    {
        _index = index;
        _schemaAgent = schemaAgent;
        _toastAgent = toastAgent;
        _name = schema.Name;
        _type = schema.Type;
        _isNullable = schema.IsNullable;
        _min = schema.Min;
        _max = schema.Max;
        DistinctCount = schema.DistinctCount;
        SampleValues = string.Join(", ", schema.SampleValues.Where(v => v != null));
    }

    public string Name
    {
        get => _name;
        set
        {
            var previous = _name;
            if (!SetProperty(ref _name, value))
            {
                return;
            }

            if (!_schemaAgent.TryRename(_index, value))
            {
                SetProperty(ref _name, previous);
            }
            else
            {
                FlagEdited();
            }
        }
    }

    public DataType Type
    {
        get => _type;
        set
        {
            var previous = _type;
            if (!SetProperty(ref _type, value))
            {
                return;
            }

            TryCommitSchemaChange(previousType: previous);
        }
    }

    public bool IsNullable
    {
        get => _isNullable;
        set
        {
            var previous = _isNullable;
            if (!SetProperty(ref _isNullable, value))
            {
                return;
            }

            TryCommitSchemaChange(previousNullable: previous);
        }
    }

    public double? Min
    {
        get => _min;
        set
        {
            var previous = _min;
            if (!SetProperty(ref _min, value))
            {
                return;
            }

            TryCommitSchemaChange(previousMin: previous);
        }
    }

    public double? Max
    {
        get => _max;
        set
        {
            var previous = _max;
            if (!SetProperty(ref _max, value))
            {
                return;
            }

            TryCommitSchemaChange(previousMax: previous);
        }
    }

    public int DistinctCount { get; }

    public string SampleValues { get; }

    public bool IsNumeric => Type is DataType.Int or DataType.Float;

    public bool IsEdited
    {
        get => _isEdited;
        private set => SetProperty(ref _isEdited, value);
    }

    private ColumnSchema BuildSchemaSnapshot()
    {
        return new ColumnSchema(Name, Type, IsNullable, Min, Max, DistinctCount, SampleValues.Split(',').Select(s => s.Trim()).ToList());
    }

    private void TryCommitSchemaChange(DataType? previousType = null, bool? previousNullable = null, double? previousMin = null, double? previousMax = null)
    {
        var snapshot = BuildSchemaSnapshot();
        if (_schemaAgent.TryUpdateSchema(_index, snapshot))
        {
            FlagEdited();
            return;
        }

        if (previousType.HasValue)
        {
            _type = previousType.Value;
            RaisePropertyChanged(nameof(Type));
        }

        if (previousNullable.HasValue)
        {
            _isNullable = previousNullable.Value;
            RaisePropertyChanged(nameof(IsNullable));
        }

        if (previousMin.HasValue || previousMax.HasValue)
        {
            _min = previousMin;
            _max = previousMax;
            RaisePropertyChanged(nameof(Min));
            RaisePropertyChanged(nameof(Max));
        }
    }

    private async void FlagEdited()
    {
        IsEdited = true;
        await Task.Delay(1200);
        IsEdited = false;
    }
}
