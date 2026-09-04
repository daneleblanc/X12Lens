namespace X12Lens;

/// <summary>
/// One transaction set: a single business document such as a purchase order, bounded by
/// its ST and SE segments.
/// </summary>
public sealed class TransactionSet
{
    private readonly IReadOnlyList<Segment> _segments;

    internal TransactionSet(IReadOnlyList<Segment> segments)
    {
        _segments = segments;
    }

    /// <summary>
    /// Every segment from ST through SE, both included.
    /// </summary>
    /// <remarks>
    /// SE01 declares the number of segments in the transaction set counting the ST and the
    /// SE themselves, so keeping them in this list means <c>Segments.Count</c> compares
    /// directly against <see cref="DeclaredSegmentCount"/> with no arithmetic in between.
    /// That comparison is the single most common real world X12 failure, so the model is
    /// shaped to make it trivial. Use <see cref="Body"/> when you want the content only.
    /// </remarks>
    public IReadOnlyList<Segment> Segments => _segments;

    /// <summary>The segments between ST and SE, excluding both.</summary>
    public IEnumerable<Segment> Body => _segments.Skip(1).Take(_segments.Count - 2);

    /// <summary>The ST segment that opens the transaction set.</summary>
    public Segment St => _segments[0];

    /// <summary>The SE segment that closes the transaction set.</summary>
    public Segment Se => _segments[^1];

    /// <summary>ST01. The transaction set identifier code, for example "850".</summary>
    public string Code => St[1];

    /// <summary>ST02. The transaction set control number, matched by SE02.</summary>
    public string ControlNumber => St[2];

    /// <summary>ST03. Implementation convention reference, present in 5010 and later.</summary>
    public string ImplementationReference => St[3];

    /// <summary>SE01. The segment count the sender claims, including ST and SE.</summary>
    public string DeclaredSegmentCount => Se[1];

    /// <summary>SE02. The control number the sender repeats in the trailer. Should equal ST02.</summary>
    public string TrailerControlNumber => Se[2];

    /// <summary>Plain English name of <see cref="Code"/>, or null when the code is not in the catalog.</summary>
    public string? Name => X12Catalog.DescribeTransactionSet(Code);

    /// <summary>Every segment in the transaction set carrying the given id, in document order.</summary>
    public IEnumerable<Segment> SegmentsWithId(string id) =>
        _segments.Where(s => string.Equals(s.Id, id, StringComparison.Ordinal));

    /// <summary>Short one line summary, for logs and for the debugger.</summary>
    public override string ToString() =>
        $"{Code}{(Name is null ? "" : " " + Name)} control {ControlNumber}, {_segments.Count} segments";
}
