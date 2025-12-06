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

    [Fact]
    public void ClearsRedoStackWhenRecording()
    {
        var history = new HistoryAgent();
        history.Record(new CellChange(0, 0, "a", "b"));
        history.Undo();

        history.Record(new CellChange(1, 1, "c", "d"));

        Assert.Null(history.Redo());
    }

    [Fact]
    public void CancelUndoRestoresStack()
    {
        var history = new HistoryAgent();
        var change = new CellChange(0, 0, "a", "b");
        history.Record(change);

        var undo = history.Undo();
        Assert.Equal(change, undo);

        history.CancelUndo(change);

        Assert.NotNull(history.Undo());
    }

    [Fact]
    public void CancelRedoRestoresStack()
    {
        var history = new HistoryAgent();
        var change = new CellChange(0, 0, "a", "b");
        history.Record(change);
        history.Undo();

        var redo = history.Redo();
        Assert.Equal(change, redo);

        history.CancelRedo(change);

        Assert.NotNull(history.Redo());
    }

    [Fact]
    public void HandlesEmptyStacks()
    {
        var history = new HistoryAgent();

        Assert.Null(history.Undo());
        Assert.Null(history.Redo());
    }
}
