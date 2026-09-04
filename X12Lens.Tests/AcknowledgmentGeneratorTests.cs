using Xunit;

namespace X12Lens.Tests;

public sealed class AcknowledgmentGeneratorTests
{
    private readonly X12Parser _parser = new();

    private static readonly DateTime FixedTimestamp = new(2026, 8, 1, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Generate_ProducesAnInterchangeThatParsesCleanly()
    {
        var source = _parser.Parse(Samples.Read(Samples.PurchaseOrder));

        var text = X12AcknowledgmentGenerator.Generate(source, "500", "500", FixedTimestamp);
        var ack = _parser.Parse(text);

        Assert.Equal("000000500", ack.ControlNumber);
        Assert.Equal(ack.ControlNumber, ack.TrailerControlNumber);
        Assert.Equal("00501", ack.VersionId);
        Assert.Equal("P", ack.UsageIndicator);
        Assert.Single(ack.Groups);
        Assert.Equal("FA", ack.Groups[0].FunctionalIdCode);
        Assert.Equal("005010", ack.Groups[0].VersionCode);
    }

    [Fact]
    public void Generate_SwapsSenderAndReceiverRelativeToTheSource()
    {
        var source = _parser.Parse(Samples.Read(Samples.PurchaseOrder));

        var ack = _parser.Parse(X12AcknowledgmentGenerator.Generate(source, generatedAt: FixedTimestamp));

        Assert.Equal(source.ReceiverId, ack.SenderId);
        Assert.Equal(source.ReceiverQualifier, ack.SenderQualifier);
        Assert.Equal(source.SenderId, ack.ReceiverId);
        Assert.Equal(source.SenderQualifier, ack.ReceiverQualifier);
    }

    [Fact]
    public void Generate_AcceptsEveryTransactionSet_WhenTheSourceIsBalanced()
    {
        var source = _parser.Parse(Samples.Read(Samples.PurchaseOrder));

        var ack = _parser.Parse(X12AcknowledgmentGenerator.Generate(source, generatedAt: FixedTimestamp));
        var acknowledgment = ack.Groups[0].TransactionSets[0];

        Assert.Equal("997", acknowledgment.Code);
        Assert.Equal("PO", Assert.Single(acknowledgment.SegmentsWithId("AK1"))[1]);
        Assert.Equal(2, acknowledgment.SegmentsWithId("AK2").Count());
        Assert.All(acknowledgment.SegmentsWithId("AK5"), s => Assert.Equal("A", s[1]));

        var ak9 = Assert.Single(acknowledgment.SegmentsWithId("AK9"));
        Assert.Equal("A", ak9[1]);
        Assert.Equal("2", ak9[2]);
        Assert.Equal("2", ak9[3]);
        Assert.Equal("2", ak9[4]);
    }

    [Fact]
    public void Generate_AcknowledgesEveryFunctionalGroupInTheSource_OneStPerGroup()
    {
        var source = _parser.Parse(Samples.Read(Samples.MultiGroup));

        var ack = _parser.Parse(X12AcknowledgmentGenerator.Generate(source, generatedAt: FixedTimestamp));

        Assert.Equal(2, ack.Groups[0].TransactionSets.Count);
        Assert.Equal(new[] { "PO", "FA" },
            ack.Groups[0].TransactionSets.Select(t => t.SegmentsWithId("AK1").Single()[1]));
    }

    [Fact]
    public void Generate_RejectsATransactionSetThatFailsItsOwnSegmentCountCheck()
    {
        var document = Envelope.Document(
            Envelope.Isa(), Envelope.Gs, Envelope.St, Envelope.Beg,
            Envelope.SeWithCount("99"), Envelope.Ge, Envelope.Iea);
        var source = _parser.Parse(document);

        var ack = _parser.Parse(X12AcknowledgmentGenerator.Generate(source, generatedAt: FixedTimestamp));
        var acknowledgment = ack.Groups[0].TransactionSets[0];

        var ak5 = Assert.Single(acknowledgment.SegmentsWithId("AK5"));
        Assert.Equal("R", ak5[1]);
        Assert.Equal("5", ak5[2]);

        var ak9 = Assert.Single(acknowledgment.SegmentsWithId("AK9"));
        Assert.Equal("R", ak9[1]);
        Assert.Equal("0", ak9[4]);
    }

    [Fact]
    public void Generate_RejectsTheWholeGroup_WhenTheGroupItselfIsUnbalanced_EvenIfEachTransactionSetIsFine()
    {
        var document = Envelope.Document(
            Envelope.Isa(), Envelope.Gs, Envelope.St, Envelope.Beg,
            Envelope.Se, Envelope.GeWithCount("2"), Envelope.Iea);
        var source = _parser.Parse(document);

        var ack = _parser.Parse(X12AcknowledgmentGenerator.Generate(source, generatedAt: FixedTimestamp));
        var acknowledgment = ack.Groups[0].TransactionSets[0];

        var ak5 = Assert.Single(acknowledgment.SegmentsWithId("AK5"));
        Assert.Equal("A", ak5[1]);

        var ak9 = Assert.Single(acknowledgment.SegmentsWithId("AK9"));
        Assert.Equal("R", ak9[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1234567890")]
    public void Generate_RejectsAnInvalidInterchangeControlNumber(string controlNumber)
    {
        var source = _parser.Parse(Samples.Read(Samples.PurchaseOrder));

        Assert.Throws<ArgumentException>(() => X12AcknowledgmentGenerator.Generate(source, controlNumber));
    }

    [Fact]
    public void Generate_UsesTheSourcesOwnDelimiters()
    {
        var source = _parser.Parse(Samples.Read(Samples.CustomDelimiters));

        var text = X12AcknowledgmentGenerator.Generate(source, generatedAt: FixedTimestamp);

        Assert.StartsWith("ISA|", text, StringComparison.Ordinal);
        var ack = _parser.Parse(text);
        Assert.Equal(source.Delimiters, ack.Delimiters);
    }
}
