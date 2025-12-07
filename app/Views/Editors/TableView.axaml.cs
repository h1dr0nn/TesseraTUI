using Avalonia.Controls;

namespace Tessera.Views.Editors;

/// <summary>
/// TableView now uses custom SpreadsheetGrid for cell selection.
/// Most logic has moved to SpreadsheetGrid control.
/// </summary>
public partial class TableView : UserControl
{
    public TableView()
    {
        InitializeComponent();
    }
}
