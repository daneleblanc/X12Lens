using X12Lens.Cli;
using Xunit;

namespace X12Lens.Tests;

public sealed class ValidateCommandTests
{
    private readonly X12Parser _parser = new();

    private static (ValidateCommand Command, StringWriter Output) Build()
    {
        var output = new StringWriter();
        return (new ValidateCommand(new X12Parser(), output), output);
    }

    [Fact]
    public async Task Validate_ReportsBalanced_ForACleanSample()
    {
        var (command, output) = Build();

        var isValid = await command.RunAsync(Samples.PathTo(Samples.PurchaseOrder));

        Assert.True(isValid);
        Assert.Contains("balanced", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ReturnsFalseAndListsEveryIssue_ForAnUnbalancedInterchange()
    {
        var (command, output) = Build();
        var document = Envelope.Document(
            Envelope.Isa(controlNumber: "000000102"), Envelope.Gs, Envelope.St, Envelope.Beg,
            Envelope.SeWithCount("99"), Envelope.Ge, Envelope.Iea);
        var interchange = _parser.Parse(document);

        var isValid = command.Write(interchange, "unbalanced.edi");
        var text = output.ToString();

        Assert.False(isValid);
        Assert.Contains("2 balance issues", text, StringComparison.Ordinal);
        Assert.Contains("[ISA13_IEA02]", text, StringComparison.Ordinal);
        Assert.Contains("[SE01_SEGMENT_COUNT]", text, StringComparison.Ordinal);
    }
}
