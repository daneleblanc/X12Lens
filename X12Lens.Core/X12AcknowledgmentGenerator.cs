namespace X12Lens;

/// <summary>
/// Builds a 997 Functional Acknowledgment that reports back to the sender of an interchange.
/// </summary>
/// <remarks>
/// <para>
/// A 997 acknowledges functional groups, not whole interchanges: one <c>ST*997</c>
/// transaction set per functional group in the source, all carried inside a single new
/// interchange addressed back to whoever sent the original one (sender and receiver swap).
/// That is the shape a real EDI translator produces, and it is why one call to
/// <see cref="Generate"/> can hand back several <c>ST</c> loops from one source interchange.
/// </para>
/// <para>
/// Accept or reject is decided from exactly the balances <see cref="X12Validator"/> already
/// checks, and this reuses that same result rather than re-deriving it: a rejected
/// transaction set is one whose ST02/SE02 or SE01 issue is tagged at that transaction set's
/// own SE segment, and a rejected group is one whose GS06/GE02 or GE01 issue is tagged at
/// that group's own GE segment.
/// </para>
/// <para>
/// Scope, stated plainly. AK3 and AK4 segment level and element level error detail are not
/// implemented, so a transaction set can only come back whole (<c>AK5*A</c>) or rejected
/// (<c>AK5*R*5</c>, "one or more segments in error"), never "accepted with errors"
/// (<c>E</c>), which is a real and common outcome left out here on purpose. Interchange
/// level problems, ISA13/IEA02 and IEA01, are a TA1 interchange acknowledgment's job in real
/// EDI, not a 997's, so they play no part in this generator's accept or reject decision.
/// </para>
/// </remarks>
public static class X12AcknowledgmentGenerator
{
    private const string TransactionSetCode = "997";

    /// <summary>AK5 rejection reason code for "One or More Segments in Error", the only rejection reason this generator produces.</summary>
    private const string SegmentInErrorReasonCode = "5";

    /// <summary>
    /// Generates the text of a 997 interchange acknowledging every functional group in
    /// <paramref name="interchange"/>.
    /// </summary>
    /// <param name="interchange">The parsed interchange to acknowledge.</param>
    /// <param name="interchangeControlNumber">
    /// ISA13/IEA02 for the generated interchange. 1 to 9 digits. Defaults to "1"; callers
    /// tracking their own outbound control numbers should supply the next one.
    /// </param>
    /// <param name="groupControlNumber">GS06/GE02 for the generated functional group. 1 to 9 digits.</param>
    /// <param name="generatedAt">
    /// The timestamp stamped into ISA09/10 and GS04/05. Defaults to <see cref="DateTime.UtcNow"/>.
    /// Callers that want a reproducible output, such as a test, should pass a fixed value.
    /// </param>
    /// <returns>The generated interchange as text, one segment per line, ISA through IEA.</returns>
    public static string Generate(
        Interchange interchange,
        string interchangeControlNumber = "1",
        string groupControlNumber = "1",
        DateTime? generatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(interchange);
        RequireControlNumber(interchangeControlNumber, nameof(interchangeControlNumber));
        RequireControlNumber(groupControlNumber, nameof(groupControlNumber));

        var delimiters = interchange.Delimiters;
        var now = generatedAt ?? DateTime.UtcNow;
        var validation = X12Validator.Validate(interchange);

        var rejectedTransactionSetPositions = PositionsFor(validation, "ST02_SE02", "SE01_SEGMENT_COUNT");
        var unbalancedGroupPositions = PositionsFor(validation, "GS06_GE02", "GE01_TRANSACTION_SET_COUNT");

        var segments = new List<string>
        {
            BuildIsa(delimiters, interchange, interchangeControlNumber, now),
            BuildGs(interchange, groupControlNumber, now),
        };

        var acknowledgedGroupCount = 0;

        foreach (var group in interchange.Groups)
        {
            acknowledgedGroupCount++;
            segments.AddRange(BuildAcknowledgmentTransactionSet(
                delimiters, group, acknowledgedGroupCount,
                rejectedTransactionSetPositions, unbalancedGroupPositions));
        }

        segments.Add(Seg(delimiters, "GE", acknowledgedGroupCount.ToString(), groupControlNumber));
        segments.Add(Seg(delimiters, "IEA", "1", interchangeControlNumber.PadLeft(9, '0')));

        return string.Join("\n", segments) + "\n";
    }

    private static List<string> BuildAcknowledgmentTransactionSet(
        X12Delimiters delimiters,
        FunctionalGroup group,
        int sequenceNumber,
        IReadOnlySet<int> rejectedTransactionSetPositions,
        IReadOnlySet<int> unbalancedGroupPositions)
    {
        var st02 = sequenceNumber.ToString("D4");
        var body = new List<string>
        {
            Seg(delimiters, "ST", TransactionSetCode, st02),
            Seg(delimiters, "AK1", group.FunctionalIdCode, group.ControlNumber),
        };

        var acceptedCount = 0;

        foreach (var transactionSet in group.TransactionSets)
        {
            body.Add(Seg(delimiters, "AK2", transactionSet.Code, transactionSet.ControlNumber));

            if (rejectedTransactionSetPositions.Contains(transactionSet.Se.Position))
            {
                body.Add(Seg(delimiters, "AK5", "R", SegmentInErrorReasonCode));
            }
            else
            {
                acceptedCount++;
                body.Add(Seg(delimiters, "AK5", "A"));
            }
        }

        var total = group.TransactionSets.Count;
        var groupUnbalanced = unbalancedGroupPositions.Contains(group.Ge.Position);
        var ackCode = GroupAcknowledgmentCode(groupUnbalanced, acceptedCount, total);

        body.Add(Seg(delimiters, "AK9", ackCode, total.ToString(), total.ToString(), acceptedCount.ToString()));
        body.Add(Seg(delimiters, "SE", (body.Count + 1).ToString(), st02));

        return body;
    }

    /// <summary>
    /// AK9-01: <c>A</c> accepted, <c>P</c> partially accepted, <c>R</c> rejected. Groups with
    /// no transaction sets in them are an edge case, not a rejection: there is nothing to
    /// have rejected, so an otherwise balanced empty group is reported accepted.
    /// </summary>
    private static string GroupAcknowledgmentCode(bool groupUnbalanced, int accepted, int total)
    {
        if (total == 0)
        {
            return groupUnbalanced ? "R" : "A";
        }

        if (groupUnbalanced)
        {
            return "R";
        }

        if (accepted == total)
        {
            return "A";
        }

        return accepted == 0 ? "R" : "P";
    }

    private static HashSet<int> PositionsFor(ValidationResult result, params string[] codes) =>
        result.Issues
            .Where(issue => codes.Contains(issue.Code, StringComparer.Ordinal))
            .Select(issue => issue.SegmentPosition)
            .ToHashSet();

    /// <summary>
    /// Builds the 106 character ISA segment for the generated interchange. Sender and
    /// receiver swap relative to the source: this interchange is going back the way the
    /// source one came from.
    /// </summary>
    private static string BuildIsa(X12Delimiters d, Interchange source, string controlNumber, DateTime now)
    {
        string[] fields =
        [
            "00",
            new string(' ', 10),
            "00",
            new string(' ', 10),
            source.ReceiverQualifier.PadRight(2),
            source.ReceiverId.PadRight(15),
            source.SenderQualifier.PadRight(2),
            source.SenderId.PadRight(15),
            now.ToString("yyMMdd"),
            now.ToString("HHmm"),
            (d.Repetition ?? 'U').ToString(),
            source.VersionId,
            controlNumber.PadLeft(9, '0'),
            "0",
            source.UsageIndicator,
            d.Component.ToString(),
        ];

        return "ISA" + d.Element + string.Join(d.Element, fields) + d.Segment;
    }

    /// <summary>
    /// Builds the GS segment for the generated FA functional group. GS08 mirrors the
    /// source's ISA12 the way every sample in this repo does it: "00501" in, "005010" out.
    /// </summary>
    private static string BuildGs(Interchange source, string groupControlNumber, DateTime now)
    {
        var gs08 = source.VersionId.Length == 5 ? source.VersionId + "0" : source.VersionId;

        return Seg(
            source.Delimiters, "GS", "FA",
            source.ReceiverId.Trim(), source.SenderId.Trim(),
            now.ToString("yyyyMMdd"), now.ToString("HHmm"),
            groupControlNumber, "X", gs08);
    }

    private static string Seg(X12Delimiters d, params string[] elements) =>
        string.Join(d.Element, elements) + d.Segment;

    private static void RequireControlNumber(string value, string paramName)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 9 || !value.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Control numbers must be 1 to 9 digits.", paramName);
        }
    }
}
