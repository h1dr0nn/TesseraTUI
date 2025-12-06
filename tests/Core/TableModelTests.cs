using System.Collections.Generic;
using Tessera.Core.Models;
using Xunit;

namespace Tessera.Tests.Core;

public class TableModelTests
{
    [Fact]
    public void StoresColumnsAndRows()
    {
        var columns = new List<ColumnModel> { new("First"), new("Second") };
        var rows = new List<RowModel>
        {
            new(new List<string?> { "A", "B" }),
            new(new List<string?> { "C", "D" })
        };

        var table = new TableModel(columns, rows);

        Assert.Equal(2, table.Columns.Count);
        Assert.Equal("First", table.Columns[0].Name);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("D", table.Rows[1].Cells[1]);
    }
}
