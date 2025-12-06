using System.Collections.Generic;

namespace Tessera.Core.Models;

public class TableModel
{
    public TableModel(List<ColumnModel> columns, List<RowModel> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public List<ColumnModel> Columns { get; }

    public List<RowModel> Rows { get; }
}

public class ColumnModel
{
    public ColumnModel(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

public class RowModel
{
    public RowModel(List<string?> cells)
    {
        Cells = cells;
    }

    public List<string?> Cells { get; }
}
