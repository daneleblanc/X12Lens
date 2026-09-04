namespace X12Lens;

/// <summary>
/// One X12 segment: an identifier plus its elements.
/// </summary>
/// <remarks>
/// Elements are indexed 1-based, so <c>segment[3]</c> is BEG03. That is deliberate. EDI
/// documentation, partner specifications and support tickets all name elements by their
/// 1-based ordinal, and a parser that silently shifts by one turns every conversation
/// with a trading partner into an off-by-one argument.
/// </remarks>
public sealed class Segment
{
    private readonly string[] _elements;
    private readonly X12Delimiters _delimiters;

    internal Segment(string id, string[] elements, int position, X12Delimiters delimiters)
    {
        Id = id;
        _elements = elements;
        Position = position;
        _delimiters = delimiters;
    }

    /// <summary>Segment identifier, for example "ISA", "GS", "PO1".</summary>
    public string Id { get; }

    /// <summary>1-based position of this segment in the document it was read from.</summary>
    public int Position { get; }

    /// <summary>Number of elements after the identifier.</summary>
    public int ElementCount => _elements.Length;

    /// <summary>All elements after the identifier, in order.</summary>
    public IReadOnlyList<string> Elements => _elements;

    /// <summary>
    /// The element at the given 1-based ordinal, or an empty string when it is absent.
    /// </summary>
    /// <remarks>
    /// Absent returns empty rather than throwing because X12 lets a sender omit trailing
    /// elements entirely. A PO1 segment with 6 elements and a PO1 segment with 25 are both
    /// valid, so "element 12 is not there" is ordinary data, not an error.
    /// </remarks>
    public string this[int ordinal] =>
        ordinal >= 1 && ordinal <= _elements.Length ? _elements[ordinal - 1] : string.Empty;

    /// <summary>
    /// Splits a composite element into its components using the interchange's own component
    /// separator. Returns an empty list when the element is absent or empty.
    /// </summary>
    public IReadOnlyList<string> Components(int ordinal)
    {
        var value = this[ordinal];
        return value.Length == 0
            ? Array.Empty<string>()
            : value.Split(_delimiters.Component);
    }

    /// <summary>
    /// Splits a repeating element using the interchange's repetition separator. When the
    /// interchange declares no repetition separator (version 00401 and earlier) the element
    /// is returned as a single value, because repetition is not legal there.
    /// </summary>
    public IReadOnlyList<string> Repetitions(int ordinal)
    {
        var value = this[ordinal];
        if (value.Length == 0)
        {
            return Array.Empty<string>();
        }

        return _delimiters.Repetition is { } separator
            ? value.Split(separator)
            : new[] { value };
    }

    /// <summary>Rebuilds the segment as it appeared in the document, without the terminator.</summary>
    public override string ToString() =>
        _elements.Length == 0 ? Id : Id + _delimiters.Element + string.Join(_delimiters.Element, _elements);
}
