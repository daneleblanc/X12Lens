namespace X12Lens;

/// <summary>
/// A complete X12 interchange: everything between an ISA segment and its IEA segment,
/// plus the delimiters that interchange declared for itself.
/// </summary>
public sealed class Interchange
{
    internal Interchange(
        Segment isa,
        Segment iea,
        IReadOnlyList<FunctionalGroup> groups,
        X12Delimiters delimiters,
        IReadOnlyList<Segment> allSegments)
    {
        Isa = isa;
        Iea = iea;
        Groups = groups;
        Delimiters = delimiters;
        AllSegments = allSegments;
    }

    /// <summary>The ISA segment that opens the interchange.</summary>
    public Segment Isa { get; }

    /// <summary>The IEA segment that closes the interchange.</summary>
    public Segment Iea { get; }

    /// <summary>The functional groups in this interchange, in document order.</summary>
    public IReadOnlyList<FunctionalGroup> Groups { get; }

    /// <summary>The delimiters read out of this interchange's own ISA segment.</summary>
    public X12Delimiters Delimiters { get; }

    /// <summary>
    /// Every segment in the document in order, ISA through IEA. Handy for LINQ queries
    /// that do not care about envelope structure, such as "show me every REF segment".
    /// </summary>
    public IReadOnlyList<Segment> AllSegments { get; }

    /// <summary>ISA05. Interchange sender id qualifier, for example "ZZ" or "01".</summary>
    public string SenderQualifier => Isa[5].Trim();

    /// <summary>ISA06. Interchange sender id. Space padded to 15 characters on the wire.</summary>
    public string SenderId => Isa[6].Trim();

    /// <summary>ISA07. Interchange receiver id qualifier.</summary>
    public string ReceiverQualifier => Isa[7].Trim();

    /// <summary>ISA08. Interchange receiver id. Space padded to 15 characters on the wire.</summary>
    public string ReceiverId => Isa[8].Trim();

    /// <summary>ISA09. Interchange date, YYMMDD. Two digit year, unlike GS04.</summary>
    public string Date => Isa[9];

    /// <summary>ISA10. Interchange time, HHMM.</summary>
    public string Time => Isa[10];

    /// <summary>ISA12. Interchange control version number, for example "00401" or "00501".</summary>
    public string VersionId => Isa[12];

    /// <summary>ISA13. Interchange control number, matched by IEA02.</summary>
    public string ControlNumber => Isa[13];

    /// <summary>ISA14. "1" when the sender is requesting a TA1 interchange acknowledgment.</summary>
    public bool AcknowledgmentRequested => Isa[14] == "1";

    /// <summary>ISA15. "P" for production, "T" for test.</summary>
    public string UsageIndicator => Isa[15];

    /// <summary>True when ISA15 is "P". A test interchange processed as production is a bad day.</summary>
    public bool IsProduction => UsageIndicator == "P";

    /// <summary>IEA01. The number of functional groups the sender claims are in this interchange.</summary>
    public string DeclaredGroupCount => Iea[1];

    /// <summary>IEA02. The control number the sender repeats in the trailer. Should equal ISA13.</summary>
    public string TrailerControlNumber => Iea[2];

    /// <summary>Total transaction sets across every group.</summary>
    public int TransactionSetCount => Groups.Sum(g => g.TransactionSets.Count);

    /// <summary>Total segments in the interchange, counting ISA and IEA.</summary>
    public int SegmentCount => AllSegments.Count;

    /// <summary>Short one line summary, for logs and for the debugger.</summary>
    public override string ToString() =>
        $"Interchange {ControlNumber}, {Groups.Count} groups, {TransactionSetCount} transaction sets";
}
