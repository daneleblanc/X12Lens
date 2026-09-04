using Xunit;

namespace X12Lens.Tests;

public sealed class ParserTests
{
    private readonly X12Parser _parser = new();

    [Fact]
    public void Parse_PurchaseOrderSample_HasOneGroupHoldingTwoTransactionSets()
    {
        var interchange = _parser.Parse(Samples.Read(Samples.PurchaseOrder));

        var group = Assert.Single(interchange.Groups);
        Assert.Equal("PO", group.FunctionalIdCode);
        Assert.Equal(2, group.TransactionSets.Count);
        Assert.All(group.TransactionSets, t => Assert.Equal("850", t.Code));
    }

    [Fact]
    public void Parse_PurchaseOrderSample_ReadsTheInterchangeHeader()
    {
        var interchange = _parser.Parse(Samples.Read(Samples.PurchaseOrder));

        Assert.Equal("ZZ", interchange.SenderQualifier);
        Assert.Equal("DEMOSENDER01", interchange.SenderId);
        Assert.Equal("ZZ", interchange.ReceiverQualifier);
        Assert.Equal("DEMORECEIVER01", interchange.ReceiverId);
        Assert.Equal("000000101", interchange.ControlNumber);
        Assert.Equal("00501", interchange.VersionId);
        Assert.True(interchange.IsProduction);
        Assert.False(interchange.AcknowledgmentRequested);
    }

    [Fact]
    public void Parse_TransactionSetSegments_RunFromStThroughSeInclusive()
    {
        // SE01 counts the ST and the SE themselves, so the model keeps them in the list.
        // Getting this wrong by one is the classic cause of a partner rejecting a file.
        var interchange = _parser.Parse(Samples.Read(Samples.PurchaseOrder));

        foreach (var transactionSet in interchange.Groups.SelectMany(g => g.TransactionSets))
        {
            Assert.Equal("ST", transactionSet.Segments[0].Id);
            Assert.Equal("SE", transactionSet.Segments[^1].Id);
            Assert.Equal(
                transactionSet.DeclaredSegmentCount,
                transactionSet.Segments.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    [Fact]
    public void Parse_Body_ExcludesTheStAndSeSegments()
    {
        var interchange = _parser.Parse(Samples.Read(Samples.PurchaseOrder));
        var first = interchange.Groups[0].TransactionSets[0];

        Assert.Equal(first.Segments.Count - 2, first.Body.Count());
        Assert.DoesNotContain(first.Body, s => s.Id is "ST" or "SE");
    }

    [Fact]
    public void Parse_ControlNumbers_MatchBetweenHeaderAndTrailerInTheSamples()
    {
        var interchange = _parser.Parse(Samples.Read(Samples.PurchaseOrder));

        Assert.Equal(interchange.ControlNumber, interchange.TrailerControlNumber.PadLeft(9, '0'));
        Assert.Equal(
            interchange.Groups.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            interchange.DeclaredGroupCount);

        foreach (var group in interchange.Groups)
        {
            Assert.Equal(group.ControlNumber, group.TrailerControlNumber);
            Assert.Equal(
                group.TransactionSets.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                group.DeclaredTransactionSetCount);
        }
    }

    [Fact]
    public void Parse_MultiGroupSample_KeepsTheTwoGroupsSeparate()
    {
        var interchange = _parser.Parse(Samples.Read(Samples.MultiGroup));

        Assert.Equal(2, interchange.Groups.Count);
        Assert.Equal(new[] { "PO", "FA" }, interchange.Groups.Select(g => g.FunctionalIdCode));
        Assert.Equal(new[] { "850", "997" }, interchange.Groups.Select(g => g.TransactionSets[0].Code));
        Assert.Equal("2", interchange.DeclaredGroupCount);
    }

    [Fact]
    public void Parse_FunctionalAckSample_ExposesTheAcknowledgmentDetail()
    {
        var interchange = _parser.Parse(Samples.Read(Samples.FunctionalAck));
        var ack = interchange.Groups[0].TransactionSets[0];

        Assert.Equal("997", ack.Code);
        Assert.Equal("Functional Acknowledgment", ack.Name);

        // AK2 opens one block per acknowledged transaction set; AK9 summarises the group.
        Assert.Equal(2, ack.SegmentsWithId("AK2").Count());

        var ak9 = Assert.Single(ack.SegmentsWithId("AK9"));
        Assert.Equal("P", ak9[1]);   // partially accepted
        Assert.Equal("2", ak9[2]);   // transaction sets included
        Assert.Equal("1", ak9[4]);   // transaction sets accepted

        var rejected = ack.SegmentsWithId("AK5").Single(s => s[1] == "R");
        Assert.Equal("5", rejected[2]);
    }

    [Fact]
    public void Parse_FunctionalAckSample_IsA4010InterchangeWithNoRepetitionSeparator()
    {
        var interchange = _parser.Parse(Samples.Read(Samples.FunctionalAck));

        Assert.Equal("00401", interchange.VersionId);
        Assert.Null(interchange.Delimiters.Repetition);
        Assert.Equal(':', interchange.Delimiters.Component);
    }

    [Fact]
    public void Parse_CustomDelimiterSample_ProducesTheSameShapeAsADefaultOne()
    {
        var interchange = _parser.Parse(Samples.Read(Samples.CustomDelimiters));
        var transactionSet = interchange.Groups[0].TransactionSets[0];

        Assert.Equal("850", transactionSet.Code);
        Assert.Equal("Purchase Order", transactionSet.Name);
        Assert.Equal("PO10041", transactionSet.Segments[1][3]);
        Assert.Equal("WIDGET-100", transactionSet.SegmentsWithId("PO1").Single()[7]);
    }

    [Fact]
    public void Parse_CustomDelimiterSample_IsFlaggedAsTestNotProduction()
    {
        var interchange = _parser.Parse(Samples.Read(Samples.CustomDelimiters));

        Assert.Equal("T", interchange.UsageIndicator);
        Assert.False(interchange.IsProduction);
    }

    [Fact]
    public void Parse_AllSegments_SupportsLinqAcrossTheWholeInterchange()
    {
        var interchange = _parser.Parse(Samples.Read(Samples.PurchaseOrder));

        var lineItems = interchange.AllSegments.Where(s => s.Id == "PO1").ToArray();

        Assert.Equal(3, lineItems.Length);
        Assert.Equal(new[] { "24", "6", "100" }, lineItems.Select(s => s[2]));
    }

    [Fact]
    public async Task ParseFileAsync_ReadsAnInterchangeFromDisk()
    {
        var interchange = await _parser.ParseFileAsync(Samples.PathTo(Samples.PurchaseOrder));

        Assert.Equal("000000101", interchange.ControlNumber);
        Assert.Equal(30, interchange.SegmentCount);
    }
}
