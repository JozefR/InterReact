using System.Reactive.Linq;
using InterReact.Consts;
using InterReact.Interfaces;
using InterReact.Messages;
using InterReact.Messages.Contract;
using Stringification;

namespace SystemTests.MarketData;

public class MarketDataSnapshot(ITestOutputHelper output, TestFixture fixture) : CollectionTestBase(output, fixture)
{
    [Fact]
    public async Task MarketDataSnapshotTest()
    {
        Contract contract = new()
        {
            SecurityType = ContractSecurityType.Stock,
            Symbol = "NVDA",
            Currency = "USD",
            Exchange = "SMART"
        };

        IObservable<IHasRequestId> observable = Client.Service.CreateMarketDataSnapshotObservable(contract);

        IHasRequestId[] ticks = await observable
            //.ThrowAlertMessage()
            //.OfType<PriceTick>()
            //.OfTickClass(x => x.PriceTick)
            .ToArray();

        Assert.NotEmpty(ticks);

        foreach (var tick in ticks)
            Write(tick.Stringify());
    }

    [Fact]
    public async Task MarketDataSnapshotErrorTest()
    {
        Contract contract = new()
        {
            SecurityType = ContractSecurityType.Stock,
            Symbol = "InvalidSymbol",
            Currency = "USD",
            Exchange = "SMART"
        };

        IObservable<IHasRequestId> observable = Client.Service.CreateMarketDataSnapshotObservable(contract);

        var ex = await Assert.ThrowsAsync<AlertException>(async () => await observable.FirstAsync());
        Assert.StartsWith("Alert: No security definition has been found", ex.Message);
    }
}
