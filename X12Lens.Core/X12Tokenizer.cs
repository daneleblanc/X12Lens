namespace X12Lens;

/// <summary>
/// Splits a raw interchange into <see cref="Segment"/> objects. Structure is somebody
/// else's job: this layer knows nothing about envelopes, only about characters.
/// </summary>
public static class X12Tokenizer
{
    /// <summary>
    /// Splits <paramref name="document"/> on the segment terminator, then splits each
    /// segment on the element separator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The awkward part is line breaks. X12 is not a line oriented format, but half of the
    /// files in circulation are pretty printed with one segment per line, so a CR or LF
    /// sitting between the terminator and the next segment id is noise that has to go. The
    /// other half arrive as a single 40,000 character line. Both must parse identically.
    /// </para>
    /// <para>
    /// It cannot simply strip all CR and LF, because a real minority of partners use LF
    /// itself as the segment terminator. So a line break character is only stripped when
    /// the interchange has not claimed it as its terminator, and only from the ends of a
    /// segment. Interior whitespace is left exactly as sent: the ISA segment is fixed width
    /// and its padding spaces are meaningful.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Segment> Tokenize(string document, X12Delimiters delimiters)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(delimiters);

        var trimmable = LineBreakCharactersOtherThan(delimiters.Segment);
        var segments = new List<Segment>();
        var position = 0;

        foreach (var raw in document.Split(delimiters.Segment))
        {
            var text = trimmable.Length == 0 ? raw : raw.Trim(trimmable);
            if (text.Length == 0)
            {
                continue;
            }

            var parts = text.Split(delimiters.Element);
            var id = parts[0];
            var elements = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

            segments.Add(new Segment(id, elements, ++position, delimiters));
        }

        return segments;
    }

    private static char[] LineBreakCharactersOtherThan(char terminator) =>
        new[] { '\r', '\n' }.Where(c => c != terminator).ToArray();
}
