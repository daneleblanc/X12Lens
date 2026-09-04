namespace X12Lens.Tests;

/// <summary>
/// Builds X12 envelopes one piece at a time so a test can break exactly one thing.
/// </summary>
/// <remarks>
/// An ISA segment is 106 characters of fixed width padding, which is unreadable when
/// pasted into a test and impossible to review when it is subtly wrong. Building it from
/// named parts means a malformed-document test says what it is testing.
/// </remarks>
internal static class Envelope
{
    public const string Gs = "GS*PO*DEMOSENDER01*DEMORECEIVER01*20260731*1422*101*X*005010~";
    public const string St = "ST*850*0001~";
    public const string Beg = "BEG*00*SA*PO10041**20260731~";
    public const string Se = "SE*3*0001~";
    public const string Ge = "GE*1*101~";
    public const string Iea = "IEA*1*000000101~";

    /// <summary>
    /// Assembles a 106 character ISA segment. Defaults are a 5010 production interchange
    /// using the delimiters most partners send.
    /// </summary>
    public static string Isa(
        char element = '*',
        char component = '>',
        char terminator = '~',
        char isa11 = '^',
        string version = "00501",
        string controlNumber = "000000101",
        char usage = 'P')
    {
        string[] fields =
        [
            "00",                                   // ISA01 authorization qualifier
            new string(' ', 10),                    // ISA02 authorization information
            "00",                                   // ISA03 security qualifier
            new string(' ', 10),                    // ISA04 security information
            "ZZ",                                   // ISA05 sender qualifier
            "DEMOSENDER01".PadRight(15),            // ISA06 sender id
            "ZZ",                                   // ISA07 receiver qualifier
            "DEMORECEIVER01".PadRight(15),          // ISA08 receiver id
            "260731",                               // ISA09 date
            "1422",                                 // ISA10 time
            isa11.ToString(),                       // ISA11 repetition separator from 00501
            version,                                // ISA12 control version
            controlNumber.PadLeft(9, '0'),          // ISA13 control number
            "0",                                    // ISA14 acknowledgment requested
            usage.ToString(),                       // ISA15 usage indicator
            component.ToString(),                   // ISA16 component separator
        ];

        var isa = "ISA" + element + string.Join(element, fields) + terminator;

        if (isa.Length != X12Delimiters.IsaSegmentLength)
        {
            throw new InvalidOperationException(
                $"Test helper built a {isa.Length} character ISA. It must be " +
                $"{X12Delimiters.IsaSegmentLength}.");
        }

        return isa;
    }

    /// <summary>A minimal but complete interchange: one group, one transaction set.</summary>
    public static string Valid() => Document(Isa(), Gs, St, Beg, Se, Ge, Iea);

    /// <summary>
    /// A GS segment with an overridable control number, for balance tests that need GS06 to
    /// disagree with GE02 without touching anything else about the group.
    /// </summary>
    public static string GsWithControlNumber(string controlNumber) =>
        $"GS*PO*DEMOSENDER01*DEMORECEIVER01*20260731*1422*{controlNumber}*X*005010~";

    /// <summary>An ST segment with an overridable transaction set code and control number.</summary>
    public static string StWithControlNumber(string controlNumber, string code = "850") =>
        $"ST*{code}*{controlNumber}~";

    /// <summary>An SE segment with an overridable declared segment count and control number.</summary>
    public static string SeWithCount(string declaredSegmentCount, string controlNumber = "0001") =>
        $"SE*{declaredSegmentCount}*{controlNumber}~";

    /// <summary>A GE segment with an overridable declared transaction set count and control number.</summary>
    public static string GeWithCount(string declaredTransactionSetCount, string controlNumber = "101") =>
        $"GE*{declaredTransactionSetCount}*{controlNumber}~";

    /// <summary>An IEA segment with an overridable declared functional group count and control number.</summary>
    public static string IeaWithCount(string declaredGroupCount, string controlNumber = "000000101") =>
        $"IEA*{declaredGroupCount}*{controlNumber}~";

    /// <summary>Joins segments with line breaks, the way a pretty printed file arrives.</summary>
    public static string Document(params string[] segments) => string.Join("\n", segments) + "\n";
}
