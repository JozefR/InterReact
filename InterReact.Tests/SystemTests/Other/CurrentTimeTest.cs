using System.Reactive.Linq;
using NodaTime;
using Stringification;

namespace SystemTests.Other;

public class CurrentTimes(ITestOutputHelper output, TestFixture fixture) : CollectionTestBase(output, fixture)
{
    [Fact]
    public async Task CurrentTimeTest()
    {
        Instant time = await Client
            .Service
            .CreateCurrentTimeObservable()
            .Timeout(TimeSpan.FromSeconds(1))
            .FirstAsync();

        Write(time.Stringify());
    }
}
