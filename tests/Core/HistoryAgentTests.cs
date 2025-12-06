using Tessera.Core.Agents;
using Xunit;

namespace Tessera.Tests.Core;

public class HistoryAgentTests
{
    [Fact]
    public void RecordsUndoAndRedo()
    {
        var history = new HistoryAgent();
        var change = new CellChange(0, 1, "old", "new");
        history.Record(change);

        var undo = history.Undo();
        Assert.Equal(change, undo);

        var redo = history.Redo();
        Assert.Equal(change, redo);
    }
}
