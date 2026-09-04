namespace X12Lens;

/// <summary>
/// Checks the control-number and count balances an X12 interchange declares about itself.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="X12Parser"/> already rejects a document whose envelope structure is wrong: a
/// missing SE, a segment sitting where it should not. Validation is a different, narrower
/// question about a document that parses cleanly: does it agree with itself? X12 gives a
/// sender no mechanism to enforce that a trailer repeats what its header declared, so a
/// perfectly well formed interchange can still be wrong. A trading partner's own EDI
/// translator runs exactly this arithmetic before anything else, and it is the same
/// arithmetic behind a real 997 rejection.
/// </para>
/// <para>Six checks, three per envelope level:</para>
/// <list type="bullet">
///   <item><description>ISA13 (the interchange control number) against IEA02, and IEA01
///   (the functional group count the sender declares) against the groups actually
///   present.</description></item>
///   <item><description>GS06 (the functional group control number) against GE02, and GE01
///   (the transaction set count the sender declares for that group) against the transaction
///   sets actually present in it.</description></item>
///   <item><description>ST02 (the transaction set control number) against SE02, and SE01
///   (the segment count the sender declares) against <c>TransactionSet.Segments.Count</c>,
///   which already includes the ST and SE segments themselves, so no arithmetic sits
///   between the two numbers being compared.</description></item>
/// </list>
/// </remarks>
public static class X12Validator
{
    /// <summary>Runs every balance check against one already parsed interchange.</summary>
    public static ValidationResult Validate(Interchange interchange)
    {
        ArgumentNullException.ThrowIfNull(interchange);

        var issues = new List<ValidationIssue>();

        CheckControlNumberMatch(
            issues, "ISA13_IEA02",
            interchange.Isa, interchange.ControlNumber,
            interchange.Iea, interchange.TrailerControlNumber,
            "interchange control number");

        CheckCount(
            issues, "IEA01_GROUP_COUNT",
            interchange.Iea, interchange.DeclaredGroupCount, interchange.Groups.Count,
            "functional group");

        foreach (var group in interchange.Groups)
        {
            CheckControlNumberMatch(
                issues, "GS06_GE02",
                group.Gs, group.ControlNumber,
                group.Ge, group.TrailerControlNumber,
                "functional group control number");

            CheckCount(
                issues, "GE01_TRANSACTION_SET_COUNT",
                group.Ge, group.DeclaredTransactionSetCount, group.TransactionSets.Count,
                "transaction set");

            foreach (var transactionSet in group.TransactionSets)
            {
                CheckControlNumberMatch(
                    issues, "ST02_SE02",
                    transactionSet.St, transactionSet.ControlNumber,
                    transactionSet.Se, transactionSet.TrailerControlNumber,
                    "transaction set control number");

                CheckCount(
                    issues, "SE01_SEGMENT_COUNT",
                    transactionSet.Se, transactionSet.DeclaredSegmentCount, transactionSet.Segments.Count,
                    "segment");
            }
        }

        return new ValidationResult(issues);
    }

    private static void CheckControlNumberMatch(
        List<ValidationIssue> issues,
        string code,
        Segment header, string headerValue,
        Segment trailer, string trailerValue,
        string label)
    {
        if (!string.Equals(headerValue, trailerValue, StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue(
                code,
                $"{header.Id} declares {label} '{headerValue}' but {trailer.Id} repeats '{trailerValue}'.",
                trailer.Id,
                trailer.Position));
        }
    }

    private static void CheckCount(
        List<ValidationIssue> issues,
        string code,
        Segment trailer, string declaredValue, int actual,
        string noun)
    {
        if (!int.TryParse(declaredValue, out var declared))
        {
            issues.Add(new ValidationIssue(
                code,
                $"{trailer.Id} declares a {noun} count of '{declaredValue}', which is not a whole number.",
                trailer.Id,
                trailer.Position));
            return;
        }

        if (declared != actual)
        {
            issues.Add(new ValidationIssue(
                code,
                $"{trailer.Id} declares {declared} {Plural(noun, declared)} but the interchange has {actual}.",
                trailer.Id,
                trailer.Position));
        }
    }

    private static string Plural(string noun, int count) => count == 1 ? noun : noun + "s";
}
