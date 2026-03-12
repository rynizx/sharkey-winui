using System.Collections.Specialized;
using SharkeyWinUI.Helpers;

namespace SharkeyWinUI.Tests.Helpers;

[TestClass]
public class BulkObservableCollectionTests
{
    [TestMethod]
    public void AddRange_AddsItems_AndRaisesSingleReset()
    {
        var collection = new BulkObservableCollection<int>();
        var resetEvents = 0;

        collection.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                resetEvents++;
        };

        collection.AddRange(new[] { 1, 2, 3, 4 });

        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, collection.ToList());
        Assert.AreEqual(1, resetEvents, "AddRange should raise one Reset event.");
    }

    [TestMethod]
    public void ReplaceAll_ReplacesItems_AndRaisesSingleReset()
    {
        var collection = new BulkObservableCollection<string>(new[] { "old1", "old2" });
        var resetEvents = 0;

        collection.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                resetEvents++;
        };

        collection.ReplaceAll(new[] { "new1", "new2", "new3" });

        CollectionAssert.AreEqual(new[] { "new1", "new2", "new3" }, collection.ToList());
        Assert.AreEqual(1, resetEvents, "ReplaceAll should raise one Reset event.");
    }
}
