using Xunit;
using Xunit.Abstractions;

namespace StreamAssist.Tests;



public class EngineTests(ITestOutputHelper output)
{
    // runs every engine check, each PASS/FAIL line goes to the test output, and the failure message lists the failed checks
    [Fact]
    public async Task AllEngineChecksPass()
    {
        var failed = await EngineChecks.RunAsync(output.WriteLine);
        Assert.True(failed.Count == 0, $"{failed.Count} engine checks failed:\n" + string.Join("\n", failed));
    }
}
