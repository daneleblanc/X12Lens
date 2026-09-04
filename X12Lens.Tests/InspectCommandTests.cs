using X12Lens.Cli;
using Xunit;

namespace X12Lens.Tests;

/// <summary>
/// The CLI writes through an injected <see cref="TextWriter"/>, which is what makes these
/// possible: the command can be run into a string instead of a console.
/// </summary>
public sealed class InspectCommandTests
{
    private static (InspectCommand Command, StringWriter Output) Build()
    {
        var output = new StringWriter();
        return (new InspectCommand(new X12Parser(), output), output);
    }

    [Fact]
    public async Task Inspect_PrintsTheEnvelopeTree()
    {
        var (command, output) = Build();

        await command.RunAsync(Samples.PathTo(Samples.PurchaseOrder), showSegments: false);
        var text = output.ToString();

        Assert.Contains("Interchange   control 000000101", text, StringComparison.Ordinal);
        Assert.Contains("Sender      ZZ / DEMOSENDER01", text, StringComparison.Ordinal);
        Assert.Contains("Group       control 101   PO (Purchase Order)", text, StringComparison.Ordinal);
        Assert.Contains("850 Purchase Order", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_MatchesTheOutputPublishedInTheReadme()
    {
        // The README shows this exact text. Pinning it here means the README cannot quietly
        // drift away from what the program actually prints.
        var (command, output) = Build();

        await command.RunAsync(Samples.PathTo(Samples.PurchaseOrder), showSegments: false);

        var expected = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "expected-inspect-850.txt"));

        Assert.Equal(Normalize(expected), Normalize(output.ToString()));
    }

    [Fact]
    public async Task Inspect_WithSegments_ListsEverySegmentWithItsElements()
    {
        var (command, output) = Build();

        await command.RunAsync(Samples.PathTo(Samples.PurchaseOrder), showSegments: true);
        var text = output.ToString();

        Assert.Contains("PO1  1 | 24 | EA | 11.5 |  | VN | WIDGET-100", text, StringComparison.Ordinal);
        Assert.Contains("BEG  00 | SA | PO10041 |  | 20260731", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_ReportsTheUsageIndicator_SoATestFileIsObvious()
    {
        var (production, productionOutput) = Build();
        var (test, testOutput) = Build();

        await production.RunAsync(Samples.PathTo(Samples.PurchaseOrder), showSegments: false);
        await test.RunAsync(Samples.PathTo(Samples.CustomDelimiters), showSegments: false);

        Assert.Contains("usage P (production)", productionOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("usage T (test)", testOutput.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inspect_NamesTheDelimitersItDerivedFromTheFile()
    {
        var (command, output) = Build();

        await command.RunAsync(Samples.PathTo(Samples.CustomDelimiters), showSegments: false);

        Assert.Contains(
            "element '|'   component '^'   repetition '!'   segment '~'",
            output.ToString(),
            StringComparison.Ordinal);
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
}
