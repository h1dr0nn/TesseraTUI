using System.IO;
using Tessera.Core.IO;
using Xunit;

namespace Tessera.Tests.Core;

public class CsvLoaderTests
{
    [Fact]
    public void ParsesCsvWithAutoDetectedDelimiter()
    {
        var content = "Name;Age;Active\nAlice;30;true\nBob;25;false\n\n";
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);

        var loader = new CsvLoader();
        var result = loader.Load(path);

        Assert.Equal(2, result.Table.Rows.Count);
        Assert.Equal("Alice", result.Table.Rows[0].Cells[0]);
        Assert.Equal("false", result.Table.Rows[1].Cells[2]);
    }
}
