using Xunit;

namespace X12Lens.Tests;

public sealed class TokenizerTests
{
    private static IReadOnlyList<Segment> Tokenize(string document) =>
        X12Tokenizer.Tokenize(document, X12Delimiters.Detect(document));

    [Fact]
    public void Tokenize_ReadsAPrettyPrintedFileAndASingleLineFileIdentically()
    {
        var prettyPrinted = Envelope.Valid();
        var oneLongLine = prettyPrinted.Replace("\n", string.Empty);

        var fromPretty = Tokenize(prettyPrinted).Select(s => s.ToString()).ToArray();
        var fromOneLine = Tokenize(oneLongLine).Select(s => s.ToString()).ToArray();

        Assert.Equal(fromPretty, fromOneLine);
    }

    [Fact]
    public void Tokenize_KeepsSegments_WhenTheInterchangeUsesLineFeedAsItsTerminator()
    {
        // A minority of partners terminate segments with LF itself. Blanket stripping of
        // line breaks would erase every segment boundary in the file.
        var document = Envelope.Isa(terminator: '\n') + "GS*PO*A*B*20260731*1422*1*X*005010\n";

        var segments = Tokenize(document);

        Assert.Equal(2, segments.Count);
        Assert.Equal("ISA", segments[0].Id);
        Assert.Equal("GS", segments[1].Id);
    }

    [Fact]
    public void Tokenize_NumbersSegmentsFromOneInDocumentOrder()
    {
        var segments = Tokenize(Envelope.Valid());

        Assert.Equal(new[] { "ISA", "GS", "ST", "BEG", "SE", "GE", "IEA" }, segments.Select(s => s.Id));
        Assert.Equal(Enumerable.Range(1, 7), segments.Select(s => s.Position));
    }

    [Fact]
    public void Tokenize_PreservesOmittedElementsAsEmptyStrings()
    {
        var document = Envelope.Document(Envelope.Isa(), "BEG*00*SA*PO10041**20260731~");

        var beg = Tokenize(document)[1];

        Assert.Equal("PO10041", beg[3]);
        Assert.Equal(string.Empty, beg[4]);
        Assert.Equal("20260731", beg[5]);
    }

    [Fact]
    public void Segment_ReturnsEmpty_ForAnElementTheSenderOmittedEntirely()
    {
        // X12 lets a sender stop early rather than pad out trailing separators, so a
        // missing element is ordinary data and not an error.
        var document = Envelope.Document(Envelope.Isa(), "CTT*2~");

        var ctt = Tokenize(document)[1];

        Assert.Equal("2", ctt[1]);
        Assert.Equal(string.Empty, ctt[9]);
    }

    [Fact]
    public void Segment_SplitsCompositeElementsOnTheDeclaredComponentSeparator()
    {
        var document = Envelope.Document(Envelope.Isa(component: '>'), "REF*ZZ*A>B>C~");

        var reference = Tokenize(document)[1];

        Assert.Equal(new[] { "A", "B", "C" }, reference.Components(2));
    }

    [Fact]
    public void Segment_SplitsRepeatingElements_WhenTheInterchangeDeclaresARepetitionSeparator()
    {
        var document = Envelope.Document(
            Envelope.Isa(version: "00501", isa11: '^'),
            "DTP*003*D8*20260731^20260801~");

        var dtp = Tokenize(document)[1];

        Assert.Equal(new[] { "20260731", "20260801" }, dtp.Repetitions(3));
    }

    [Fact]
    public void Segment_TreatsARepeatingElementAsOneValue_WhenNoRepetitionSeparatorIsDeclared()
    {
        // Under 00401 there is no repetition separator, so the character is just data.
        var document = Envelope.Document(
            Envelope.Isa(version: "00401", isa11: 'U'),
            "DTP*003*D8*20260731^20260801~");

        var dtp = Tokenize(document)[1];

        Assert.Equal(new[] { "20260731^20260801" }, dtp.Repetitions(3));
    }

    [Fact]
    public void Segment_RoundTripsToItsOriginalText()
    {
        var document = Envelope.Document(Envelope.Isa(), "PO1*1*24*EA*11.5**VN*WIDGET-100~");

        var po1 = Tokenize(document)[1];

        Assert.Equal("PO1*1*24*EA*11.5**VN*WIDGET-100", po1.ToString());
    }
}
