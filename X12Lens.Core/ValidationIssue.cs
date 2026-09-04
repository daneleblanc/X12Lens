namespace X12Lens;

/// <summary>
/// One control-number or count mismatch found by <see cref="X12Validator"/>.
/// </summary>
/// <remarks>
/// Carries enough context to print without re-deriving anything: which check failed, a
/// human message naming both values, and the id and 1-based position of the trailer
/// segment where the mismatch shows up. That is deliberately the trailer, not the header,
/// because the trailer is the segment an analyst is staring at when a partner's own
/// acknowledgment comes back rejected.
/// </remarks>
public sealed record ValidationIssue(string Code, string Message, string SegmentId, int SegmentPosition)
{
    /// <summary>Renders as <c>[CODE] message (segment N, ID)</c>, the form the CLI prints one issue per line as.</summary>
    public override string ToString() => $"[{Code}] {Message} (segment {SegmentPosition}, {SegmentId})";
}
