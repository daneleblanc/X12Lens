namespace X12Lens;

/// <summary>
/// Thrown when a document cannot be read as a valid X12 interchange.
/// </summary>
/// <remarks>
/// Every message is written to answer the question an EDI analyst actually has at 7am:
/// which segment is wrong, and what was expected there. A bare "invalid format" is
/// useless when the file is 4,000 segments long, so the offending segment id and its
/// 1-based position in the document are carried on the exception itself.
/// </remarks>
public sealed class X12ParseException : Exception
{
    /// <summary>1-based position of the offending segment, or 0 when the failure happened before tokenizing.</summary>
    public int SegmentPosition { get; }

    /// <summary>Segment id (for example "GS") of the offending segment, when one is known.</summary>
    public string? SegmentId { get; }

    /// <summary>A failure that cannot be tied to a particular segment.</summary>
    public X12ParseException(string message)
        : base(message)
    {
    }

    /// <summary>A failure at a known segment, identified by its 1-based position and id.</summary>
    public X12ParseException(string message, int segmentPosition, string? segmentId)
        : base(segmentPosition > 0
            ? $"{message} (segment {segmentPosition}{(segmentId is null ? "" : ", " + segmentId)})"
            : message)
    {
        SegmentPosition = segmentPosition;
        SegmentId = segmentId;
    }

    /// <summary>A failure at the given segment.</summary>
    public X12ParseException(string message, Segment segment)
        : this(message, segment.Position, segment.Id)
    {
    }
}
