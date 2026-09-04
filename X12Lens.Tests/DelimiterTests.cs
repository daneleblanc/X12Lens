using Xunit;

namespace X12Lens.Tests;

/// <summary>
/// The delimiters are the part of X12 that a first attempt always gets wrong, so they get
/// the densest tests. Everything downstream is meaningless if these four characters are
/// guessed instead of read.
/// </summary>
public sealed class DelimiterTests
{
    [Fact]
    public void Detect_ReadsElementSeparatorFromPositionFour()
    {
        var delimiters = X12Delimiters.Detect(Envelope.Isa(element: '|'));

        Assert.Equal('|', delimiters.Element);
    }

    [Fact]
    public void Detect_ReadsComponentSeparatorFromIsa16()
    {
        var delimiters = X12Delimiters.Detect(Envelope.Isa(component: '@'));

        Assert.Equal('@', delimiters.Component);
    }

    [Fact]
    public void Detect_ReadsSegmentTerminatorFromPosition106()
    {
        var delimiters = X12Delimiters.Detect(Envelope.Isa(terminator: '#'));

        Assert.Equal('#', delimiters.Segment);
    }

    [Fact]
    public void Detect_ReadsRepetitionSeparator_WhenVersionIs00501OrLater()
    {
        var delimiters = X12Delimiters.Detect(Envelope.Isa(version: "00501", isa11: '^'));

        Assert.Equal('^', delimiters.Repetition);
    }

    [Fact]
    public void Detect_FindsNoRepetitionSeparator_WhenVersionIs00401()
    {
        // Before 00501 that position held the standards identifier, always the letter U.
        var delimiters = X12Delimiters.Detect(Envelope.Isa(version: "00401", isa11: 'U'));

        Assert.Null(delimiters.Repetition);
    }

    [Fact]
    public void Detect_FindsNoRepetitionSeparator_WhenA5010PartnerStillSendsLegacyU()
    {
        var delimiters = X12Delimiters.Detect(Envelope.Isa(version: "00501", isa11: 'U'));

        Assert.Null(delimiters.Repetition);
    }

    [Fact]
    public void Detect_HandlesACompletelyNonDefaultDelimiterSet()
    {
        var document = Samples.Read(Samples.CustomDelimiters);

        var delimiters = X12Delimiters.Detect(document);

        Assert.Equal('|', delimiters.Element);
        Assert.Equal('^', delimiters.Component);
        Assert.Equal('~', delimiters.Segment);
        Assert.Equal('!', delimiters.Repetition);
    }

    [Fact]
    public void Detect_SkipsAByteOrderMarkAndLeadingWhitespace()
    {
        // Files that arrive by FTP or as an email attachment routinely pick up both.
        var document = "\uFEFF\r\n  " + Envelope.Isa();

        var delimiters = X12Delimiters.Detect(document);

        Assert.Equal('*', delimiters.Element);
        Assert.Equal('~', delimiters.Segment);
    }

    [Fact]
    public void Detect_Throws_WhenTheDocumentIsShorterThanAnIsaSegment()
    {
        var truncated = Envelope.Isa()[..80];

        var error = Assert.Throws<X12ParseException>(() => X12Delimiters.Detect(truncated));

        Assert.Contains("106 characters", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Detect_Throws_WhenTheDocumentDoesNotOpenWithIsa()
    {
        var notAnInterchange = new string('x', 200);

        var error = Assert.Throws<X12ParseException>(() => X12Delimiters.Detect(notAnInterchange));

        Assert.Contains("does not begin with an ISA", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Detect_Throws_WhenTheDeclaredDelimitersAreNotDistinct()
    {
        // A sender that puts the element separator in ISA16 has produced a file that
        // cannot be split unambiguously. Better to say so than to guess.
        var document = Envelope.Isa(element: '*', component: '*');

        var error = Assert.Throws<X12ParseException>(() => X12Delimiters.Detect(document));

        Assert.Contains("not distinct", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_NamesEveryDelimiterIncludingTheAbsenceOfRepetition()
    {
        var fiveThousandTen = X12Delimiters.Detect(Envelope.Isa(version: "00501", isa11: '^'));
        var fourThousandTen = X12Delimiters.Detect(Envelope.Isa(version: "00401", isa11: 'U'));

        Assert.Contains("repetition '^'", fiveThousandTen.Describe(), StringComparison.Ordinal);
        Assert.Contains("repetition (none)", fourThousandTen.Describe(), StringComparison.Ordinal);
    }
}
