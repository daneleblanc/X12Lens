using Xunit;

namespace X12Lens.Tests;

/// <summary>
/// A tool that only reads clean files is not much use, because clean files are not the ones
/// anybody opens a parser for. Each test here pins the message an analyst would see.
/// </summary>
public sealed class MalformedDocumentTests
{
    private readonly X12Parser _parser = new();

    [Fact]
    public void Parse_Throws_WhenTheInterchangeHasNoIea()
    {
        var document = Envelope.Document(
            Envelope.Isa(), Envelope.Gs, Envelope.St, Envelope.Beg, Envelope.Se, Envelope.Ge);

        var error = Assert.Throws<X12ParseException>(() => _parser.Parse(document));

        Assert.Contains("is not terminated: no IEA", error.Message, StringComparison.Ordinal);
        Assert.Contains("000000101", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Throws_WhenAFunctionalGroupHasNoGe()
    {
        var document = Envelope.Document(
            Envelope.Isa(), Envelope.Gs, Envelope.St, Envelope.Beg, Envelope.Se, Envelope.Iea);

        var error = Assert.Throws<X12ParseException>(() => _parser.Parse(document));

        Assert.Contains("expected GE, found IEA", error.Message, StringComparison.Ordinal);
        Assert.Contains("control number 101", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Throws_WhenAFunctionalGroupRunsOffTheEndOfTheFile()
    {
        var document = Envelope.Document(Envelope.Isa(), Envelope.Gs, Envelope.St, Envelope.Beg, Envelope.Se);

        var error = Assert.Throws<X12ParseException>(() => _parser.Parse(document));

        Assert.Contains("no GE segment", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Throws_WhenATransactionSetHasNoSe()
    {
        var document = Envelope.Document(
            Envelope.Isa(), Envelope.Gs, Envelope.St, Envelope.Beg, Envelope.Ge, Envelope.Iea);

        var error = Assert.Throws<X12ParseException>(() => _parser.Parse(document));

        Assert.Contains("expected SE, found GE", error.Message, StringComparison.Ordinal);
        Assert.Contains("850 transaction set", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Throws_WhenATransactionSetRunsOffTheEndOfTheFile()
    {
        var document = Envelope.Document(Envelope.Isa(), Envelope.Gs, Envelope.St, Envelope.Beg);

        var error = Assert.Throws<X12ParseException>(() => _parser.Parse(document));

        Assert.Contains("no SE segment", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Throws_WhenABusinessSegmentSitsDirectlyInsideTheInterchange()
    {
        var document = Envelope.Document(Envelope.Isa(), "REF*DP*042~", Envelope.Iea);

        var error = Assert.Throws<X12ParseException>(() => _parser.Parse(document));

        Assert.Contains("Only GS and IEA are allowed", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, error.SegmentPosition);
        Assert.Equal("REF", error.SegmentId);
    }

    [Fact]
    public void Parse_Throws_WhenABusinessSegmentSitsDirectlyInsideAFunctionalGroup()
    {
        var document = Envelope.Document(
            Envelope.Isa(), Envelope.Gs, "REF*DP*042~", Envelope.Ge, Envelope.Iea);

        var error = Assert.Throws<X12ParseException>(() => _parser.Parse(document));

        Assert.Contains("Only ST and GE are allowed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Throws_WhenASegmentFollowsTheIea()
    {
        var document = Envelope.Valid() + "REF*DP*042~\n";

        var error = Assert.Throws<X12ParseException>(() => _parser.Parse(document));

        Assert.Contains("appears after the IEA", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Throws_WithASpecificMessage_WhenTheFileBatchesSeveralInterchanges()
    {
        // Batched files are real and are simply not supported yet. Say that, rather than
        // parsing the first interchange and silently dropping the rest.
        var document = Envelope.Valid() + Envelope.Valid();

        var error = Assert.Throws<X12ParseException>(() => _parser.Parse(document));

        Assert.Contains("one interchange per document", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Throws_WhenASecondIsaOpensBeforeTheFirstInterchangeCloses()
    {
        var document = Envelope.Document(Envelope.Isa(), Envelope.Isa());

        var error = Assert.Throws<X12ParseException>(() => _parser.Parse(document));

        Assert.Contains("second interchange begins here", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Throws_WhenTheFileIsNotAnInterchangeAtAll()
    {
        var error = Assert.Throws<X12ParseException>(
            () => _parser.Parse("Dear supplier, please find the attached order. Thanks."));

        Assert.Contains("Not an X12 interchange", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseException_CarriesThePositionOfTheOffendingSegment()
    {
        var document = Envelope.Document(
            Envelope.Isa(), Envelope.Gs, Envelope.St, Envelope.Beg, Envelope.Ge, Envelope.Iea);

        var error = Assert.Throws<X12ParseException>(() => _parser.Parse(document));

        Assert.Equal(5, error.SegmentPosition);
        Assert.Equal("GE", error.SegmentId);
    }
}
