using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Tessera.ViewModels;

namespace Tessera.Views;

public partial class TableView : UserControl
{
    public TableView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is TableViewModel vm && this.FindControl<DataGrid>("TableGrid") is { } grid)
        {
            ConfigureGridColumns(grid, vm);
        }
    }

    private void ConfigureGridColumns(DataGrid grid, TableViewModel vm)
    {
        grid.Columns.Clear();
        foreach (var column in vm.Columns)
        {
            var binding = new Binding($"Cells[{column.Index}].Value")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            var textColumn = new DataGridTextColumn
            {
                Header = column.Header,
                Binding = binding,
                IsReadOnly = false,
                ElementStyle = BuildDisplayStyle(column.Index),
                EditingElementStyle = BuildEditingStyle(column.Index)
            };

            grid.Columns.Add(textColumn);
        }
    }

    private static Style BuildDisplayStyle(int columnIndex)
    {
        var style = new Style(x => x.OfType<TextBlock>());
        style.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(6, 0, 6, 0)));
        style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, Avalonia.Layout.VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextPrimaryBrush")));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        style.Setters.Add(new Setter(ToolTip.TipProperty, new Binding($"Cells[{columnIndex}].ErrorMessage")));

        var errorTrigger = new DataTrigger
        {
            Binding = new Binding($"Cells[{columnIndex}].HasError"),
            Value = true
        };
        errorTrigger.Setters.Add(new Setter(TextBlock.BackgroundProperty, new DynamicResourceExtension("ErrorBrush")));
        errorTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.White));

        style.Add(errorTrigger);
        return style;
    }

    private static Style BuildEditingStyle(int columnIndex)
    {
        var style = new Style(x => x.OfType<TextBox>());
        style.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(6, 0, 6, 0)));
        style.Setters.Add(new Setter(TextBox.ForegroundProperty, new DynamicResourceExtension("TextPrimaryBrush")));
        style.Setters.Add(new Setter(ToolTip.TipProperty, new Binding($"Cells[{columnIndex}].ErrorMessage")));

        var errorTrigger = new DataTrigger
        {
            Binding = new Binding($"Cells[{columnIndex}].HasError"),
            Value = true
        };
        errorTrigger.Setters.Add(new Setter(TextBox.BorderBrushProperty, new DynamicResourceExtension("ErrorBrush")));
        errorTrigger.Setters.Add(new Setter(TextBox.BackgroundProperty, new SolidColorBrush(Color.Parse("#33E45858"))));

        style.Add(errorTrigger);
        return style;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not TableViewModel vm || sender is not DataGrid grid)
        {
            return;
        }

        var selectedCell = grid.SelectedCells.FirstOrDefault();
        if (selectedCell != null)
        {
            var rowIndex = selectedCell.Item is TableRowViewModel rowVm
                ? rowVm.Index
                : grid.SelectedIndex;
            var columnIndex = selectedCell.Column?.DisplayIndex ?? 0;
            vm.UpdateSelection(rowIndex, columnIndex);
        }
    }
}
