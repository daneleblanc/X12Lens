namespace X12Lens;

/// <summary>
/// One functional group: a batch of transaction sets that share a document type and a
/// sender and receiver pair, bounded by its GS and GE segments.
/// </summary>
public sealed class FunctionalGroup
{
    internal FunctionalGroup(Segment gs, Segment ge, IReadOnlyList<TransactionSet> transactionSets)
    {
        Gs = gs;
        Ge = ge;
        TransactionSets = transactionSets;
    }

    /// <summary>The GS segment that opens the group.</summary>
    public Segment Gs { get; }

    /// <summary>The GE segment that closes the group.</summary>
    public Segment Ge { get; }

    /// <summary>The transaction sets in this group, in document order.</summary>
    public IReadOnlyList<TransactionSet> TransactionSets { get; }

    /// <summary>GS01. Functional identifier code, for example "PO" for purchase orders.</summary>
    public string FunctionalIdCode => Gs[1];

    /// <summary>Plain English name of <see cref="FunctionalIdCode"/>, or null when not in the catalog.</summary>
    public string? FunctionalIdName => X12Catalog.DescribeFunctionalGroup(FunctionalIdCode);

    /// <summary>GS02. Application sender code. Application level, not the ISA level id.</summary>
    public string ApplicationSenderCode => Gs[2];

    /// <summary>GS03. Application receiver code.</summary>
    public string ApplicationReceiverCode => Gs[3];

    /// <summary>GS04. Group date, CCYYMMDD. Note the century, unlike ISA09.</summary>
    public string Date => Gs[4];

    /// <summary>GS05. Group time, HHMM.</summary>
    public string Time => Gs[5];

    /// <summary>GS06. Group control number, matched by GE02.</summary>
    public string ControlNumber => Gs[6];

    /// <summary>GS07. Responsible agency code, normally "X" for ASC X12.</summary>
    public string ResponsibleAgencyCode => Gs[7];

    /// <summary>GS08. Version, release and industry identifier, for example "005010".</summary>
    public string VersionCode => Gs[8];

    /// <summary>GE01. The number of transaction sets the sender claims are in this group.</summary>
    public string DeclaredTransactionSetCount => Ge[1];

    /// <summary>GE02. The control number the sender repeats in the trailer. Should equal GS06.</summary>
    public string TrailerControlNumber => Ge[2];

    /// <summary>Total segments in the group, counting GS and GE.</summary>
    public int SegmentCount => 2 + TransactionSets.Sum(t => t.Segments.Count);

    /// <summary>Short one line summary, for logs and for the debugger.</summary>
    public override string ToString() =>
        $"{FunctionalIdCode}{(FunctionalIdName is null ? "" : " " + FunctionalIdName)} " +
        $"control {ControlNumber}, {TransactionSets.Count} transaction sets";
}
