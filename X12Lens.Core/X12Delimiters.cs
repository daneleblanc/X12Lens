namespace X12Lens;

/// <summary>
/// The characters that give an X12 interchange its structure.
/// </summary>
/// <remarks>
/// <para>
/// These are NOT configuration. Every interchange declares its own delimiters inside its
/// own ISA segment, and a parser that hardcodes <c>*</c> and <c>~</c> will fail the first
/// time a trading partner sends something else. The ISA segment is the one fixed-width
/// segment in X12 precisely so that a reader can find the delimiters before it knows how
/// to split anything.
/// </para>
/// <para>
/// Layout of the 106 character ISA segment, 1-based positions:
/// </para>
/// <list type="bullet">
///   <item><description>1 to 3: the literal "ISA".</description></item>
///   <item><description>4: the element separator. Everything after this point is separated by it.</description></item>
///   <item><description>83: ISA11.</description></item>
///   <item><description>85 to 89: ISA12, the interchange control version, for example "00401" or "00501".</description></item>
///   <item><description>105: ISA16, the component element separator.</description></item>
///   <item><description>106: the segment terminator.</description></item>
/// </list>
/// <para>
/// ISA11 is the subtle one. Through version 00401 it was the interchange control standards
/// identifier and its value was always "U". From version 00501 the same position was
/// repurposed as the repetition separator. So the meaning of position 83 depends on the
/// value at positions 85 to 89, which is why <see cref="Detect"/> reads the version first.
/// </para>
/// </remarks>
/// <param name="Element">Separates elements within a segment. ISA position 4.</param>
/// <param name="Component">Separates components within a composite element. ISA16.</param>
/// <param name="Segment">Terminates a segment. ISA position 106.</param>
/// <param name="Repetition">Separates repetitions of a repeating element. ISA11, version 00501 and later only.</param>
public sealed record X12Delimiters(char Element, char Component, char Segment, char? Repetition)
{
    /// <summary>Total length of an ISA segment including its terminator. Fixed by the standard.</summary>
    public const int IsaSegmentLength = 106;

    /// <summary>First version in which ISA11 carries the repetition separator instead of the literal "U".</summary>
    public const string FirstRepetitionSeparatorVersion = "00501";

    private const int ElementSeparatorIndex = 3;      // position 4
    private const int Isa11Index = 82;                // position 83
    private const int Isa12Index = 84;                // positions 85 to 89
    private const int Isa12Length = 5;
    private const int ComponentSeparatorIndex = 104;  // position 105, ISA16
    private const int SegmentTerminatorIndex = 105;   // position 106

    /// <summary>
    /// The delimiter set most trading partners use. Provided for building documents and for
    /// tests, never used as a parsing fallback: reading a real document always derives its
    /// delimiters from its own ISA segment.
    /// </summary>
    public static X12Delimiters Default { get; } = new('*', ':', '~', '^');

    /// <summary>
    /// Reads the delimiters out of the ISA segment at the start of <paramref name="document"/>.
    /// </summary>
    /// <exception cref="X12ParseException">
    /// The document does not begin with a well formed ISA segment, or the declared delimiters
    /// are not distinct from one another.
    /// </exception>
    public static X12Delimiters Detect(string document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var start = FindIsaStart(document);

        if (document.Length - start < IsaSegmentLength)
        {
            throw new X12ParseException(
                $"Not an X12 interchange: an ISA segment is {IsaSegmentLength} characters, " +
                $"the document has {document.Length - start}.");
        }

        if (!document.AsSpan(start, 3).Equals("ISA", StringComparison.Ordinal))
        {
            throw new X12ParseException(
                "Not an X12 interchange: the document does not begin with an ISA segment.");
        }

        var element = document[start + ElementSeparatorIndex];
        var component = document[start + ComponentSeparatorIndex];
        var terminator = document[start + SegmentTerminatorIndex];
        var version = document.Substring(start + Isa12Index, Isa12Length);
        var isa11 = document[start + Isa11Index];

        var repetition = ReadRepetitionSeparator(version, isa11);
        var delimiters = new X12Delimiters(element, component, terminator, repetition);
        delimiters.Validate();
        return delimiters;
    }

    /// <summary>
    /// ISA11 is a repetition separator only from version 00501 onward. Some partners running
    /// 00501 still send the legacy "U" there, so "U" is treated as "no repetition separator"
    /// rather than as a separator character. Rejecting those files would be technically
    /// correct and operationally useless.
    /// </summary>
    private static char? ReadRepetitionSeparator(string version, char isa11)
    {
        var supportsRepetition =
            string.CompareOrdinal(version, FirstRepetitionSeparatorVersion) >= 0;

        if (!supportsRepetition || isa11 == 'U')
        {
            return null;
        }

        return isa11;
    }

    /// <summary>
    /// Skips a UTF-8 byte order mark and any leading whitespace. Files that arrive over
    /// FTP or out of an email attachment routinely carry one or both, and position 4 is
    /// only the element separator if you counted from the right place.
    /// </summary>
    private static int FindIsaStart(string document)
    {
        const char ByteOrderMark = '\uFEFF';

        var i = 0;
        while (i < document.Length && (document[i] == ByteOrderMark || char.IsWhiteSpace(document[i])))
        {
            i++;
        }

        return i;
    }

    private void Validate()
    {
        var seen = new List<char> { Element, Component, Segment };
        if (Repetition is { } repetition)
        {
            seen.Add(repetition);
        }

        if (seen.Distinct().Count() != seen.Count)
        {
            throw new X12ParseException(
                $"The ISA segment declares delimiters that are not distinct: " +
                $"element '{Element}', component '{Component}', segment '{Segment}'" +
                (Repetition is null ? "." : $", repetition '{Repetition}'."));
        }
    }

    /// <summary>Human readable form used by the CLI and by test assertions.</summary>
    public string Describe() =>
        $"element '{Element}'   component '{Component}'   " +
        $"repetition {(Repetition is null ? "(none)" : $"'{Repetition}'")}   " +
        $"segment '{Segment}'";
}
