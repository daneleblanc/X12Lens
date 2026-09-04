using Xunit;

namespace X12Lens.Tests;

/// <summary>
/// The parser already refuses a document with the wrong structure. These tests are about
/// documents that parse cleanly and still disagree with themselves.
/// </summary>
public sealed class ValidatorTests
{
    private readonly X12Parser _parser = new();

    [Fact]
    public void Validate_ReturnsNoIssues_ForABalancedInterchange()
    {
        var interchange = _parser.Parse(Envelope.Valid());

        var result = X12Validator.Validate(interchange);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Theory]
    [InlineData(Samples.PurchaseOrder)]
    [InlineData(Samples.FunctionalAck)]
    [InlineData(Samples.MultiGroup)]
    [InlineData(Samples.CustomDelimiters)]
    public void Validate_ReturnsNoIssues_ForEveryShippedSample(string fileName)
    {
        // The samples exist to be trusted examples. If any of them failed to balance, the
        // README's own claims about them would be wrong.
        var interchange = _parser.Parse(Samples.Read(fileName));

        var result = X12Validator.Validate(interchange);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Flags_WhenIsa13DoesNotMatchIea02()
    {
        var document = Envelope.Document(
            Envelope.Isa(controlNumber: "000000102"), Envelope.Gs, Envelope.St, Envelope.Beg,
            Envelope.Se, Envelope.Ge, Envelope.Iea);
        var interchange = _parser.Parse(document);

        var issue = Assert.Single(X12Validator.Validate(interchange).Issues);

        Assert.Equal("ISA13_IEA02", issue.Code);
        Assert.Equal("IEA", issue.SegmentId);
        Assert.Contains("000000102", issue.Message, StringComparison.Ordinal);
        Assert.Contains("000000101", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Flags_WhenGs06DoesNotMatchGe02()
    {
        var document = Envelope.Document(
            Envelope.Isa(), Envelope.GsWithControlNumber("101"), Envelope.St, Envelope.Beg,
            Envelope.Se, Envelope.GeWithCount("1", "102"), Envelope.Iea);
        var interchange = _parser.Parse(document);

        var issue = Assert.Single(X12Validator.Validate(interchange).Issues);

        Assert.Equal("GS06_GE02", issue.Code);
        Assert.Equal("GE", issue.SegmentId);
    }

    [Fact]
    public void Validate_Flags_WhenSt02DoesNotMatchSe02()
    {
        var document = Envelope.Document(
            Envelope.Isa(), Envelope.Gs, Envelope.StWithControlNumber("0001"), Envelope.Beg,
            Envelope.SeWithCount("3", "0002"), Envelope.Ge, Envelope.Iea);
        var interchange = _parser.Parse(document);

        var issue = Assert.Single(X12Validator.Validate(interchange).Issues);

        Assert.Equal("ST02_SE02", issue.Code);
        Assert.Equal("SE", issue.SegmentId);
    }

    [Fact]
    public void Validate_Flags_WhenSe01DoesNotMatchTheActualSegmentCount()
    {
        var document = Envelope.Document(
            Envelope.Isa(), Envelope.Gs, Envelope.St, Envelope.Beg,
            Envelope.SeWithCount("99"), Envelope.Ge, Envelope.Iea);
        var interchange = _parser.Parse(document);

        var issue = Assert.Single(X12Validator.Validate(interchange).Issues);

        Assert.Equal("SE01_SEGMENT_COUNT", issue.Code);
        Assert.Contains("declares 99", issue.Message, StringComparison.Ordinal);
        Assert.Contains("has 3", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Flags_WhenIea01DoesNotMatchTheActualGroupCount()
    {
        var document = Envelope.Document(
            Envelope.Isa(), Envelope.Gs, Envelope.St, Envelope.Beg,
            Envelope.Se, Envelope.Ge, Envelope.IeaWithCount("2"));
        var interchange = _parser.Parse(document);

        var issue = Assert.Single(X12Validator.Validate(interchange).Issues);

        Assert.Equal("IEA01_GROUP_COUNT", issue.Code);
    }

    [Fact]
    public void Validate_Flags_WhenGe01DoesNotMatchTheActualTransactionSetCount()
    {
        var document = Envelope.Document(
            Envelope.Isa(), Envelope.Gs, Envelope.St, Envelope.Beg,
            Envelope.Se, Envelope.GeWithCount("2"), Envelope.Iea);
        var interchange = _parser.Parse(document);

        var issue = Assert.Single(X12Validator.Validate(interchange).Issues);

        Assert.Equal("GE01_TRANSACTION_SET_COUNT", issue.Code);
    }

    [Fact]
    public void Validate_Flags_WhenADeclaredCountIsNotAWholeNumber()
    {
        var document = Envelope.Document(
            Envelope.Isa(), Envelope.Gs, Envelope.St, Envelope.Beg,
            Envelope.SeWithCount("many"), Envelope.Ge, Envelope.Iea);
        var interchange = _parser.Parse(document);

        var issue = Assert.Single(X12Validator.Validate(interchange).Issues);

        Assert.Equal("SE01_SEGMENT_COUNT", issue.Code);
        Assert.Contains("not a whole number", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ReportsEveryIssue_WhenMultipleLevelsDisagree()
    {
        var document = Envelope.Document(
            Envelope.Isa(controlNumber: "000000999"), Envelope.Gs, Envelope.St, Envelope.Beg,
            Envelope.SeWithCount("99"), Envelope.Ge, Envelope.Iea);
        var interchange = _parser.Parse(document);

        var issues = X12Validator.Validate(interchange).Issues;

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.Code == "ISA13_IEA02");
        Assert.Contains(issues, i => i.Code == "SE01_SEGMENT_COUNT");
    }

    [Fact]
    public void ValidationIssue_ToString_NamesTheCodeTheMessageAndTheSegment()
    {
        var document = Envelope.Document(
            Envelope.Isa(controlNumber: "000000102"), Envelope.Gs, Envelope.St, Envelope.Beg,
            Envelope.Se, Envelope.Ge, Envelope.Iea);
        var interchange = _parser.Parse(document);
        var issue = Assert.Single(X12Validator.Validate(interchange).Issues);

        var text = issue.ToString();

        Assert.Contains("[ISA13_IEA02]", text, StringComparison.Ordinal);
        Assert.Contains("segment", text, StringComparison.Ordinal);
        Assert.Contains("IEA", text, StringComparison.Ordinal);
    }
}
