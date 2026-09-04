namespace X12Lens;

/// <summary>
/// Names for the transaction set and functional group codes this parser is likely to meet.
/// </summary>
/// <remarks>
/// Deliberately partial. These are public ASC X12 code list values, not a trading partner
/// specification, and nothing here encodes anyone's business rules. Unknown codes return
/// null and the caller prints the raw code, which is the correct behaviour for a tool whose
/// job is to show you what is in the file rather than to have an opinion about it.
/// </remarks>
public static class X12Catalog
{
    private static readonly IReadOnlyDictionary<string, string> TransactionSets =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["204"] = "Motor Carrier Load Tender",
            ["210"] = "Motor Carrier Freight Details and Invoice",
            ["214"] = "Transportation Carrier Shipment Status Message",
            ["810"] = "Invoice",
            ["816"] = "Organizational Relationships",
            ["820"] = "Payment Order / Remittance Advice",
            ["824"] = "Application Advice",
            ["830"] = "Planning Schedule with Release Capability",
            ["832"] = "Price / Sales Catalog",
            ["846"] = "Inventory Inquiry / Advice",
            ["850"] = "Purchase Order",
            ["852"] = "Product Activity Data",
            ["855"] = "Purchase Order Acknowledgment",
            ["856"] = "Ship Notice / Manifest",
            ["860"] = "Purchase Order Change Request",
            ["864"] = "Text Message",
            ["865"] = "Purchase Order Change Acknowledgment",
            ["869"] = "Order Status Inquiry",
            ["870"] = "Order Status Report",
            ["940"] = "Warehouse Shipping Order",
            ["943"] = "Warehouse Stock Transfer Shipment Advice",
            ["944"] = "Warehouse Stock Transfer Receipt Advice",
            ["945"] = "Warehouse Shipping Advice",
            ["947"] = "Warehouse Inventory Adjustment Advice",
            ["997"] = "Functional Acknowledgment",
            ["999"] = "Implementation Acknowledgment",
        };

    private static readonly IReadOnlyDictionary<string, string> FunctionalGroups =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AG"] = "Application Advice",
            ["CT"] = "Price / Sales Catalog",
            ["FA"] = "Functional Acknowledgment",
            ["IB"] = "Inventory Inquiry / Advice",
            ["IN"] = "Invoice",
            ["OW"] = "Warehouse Shipping Order",
            ["PC"] = "Purchase Order Change Request",
            ["PD"] = "Product Activity Data",
            ["PO"] = "Purchase Order",
            ["PR"] = "Purchase Order Acknowledgment",
            ["QM"] = "Transportation Carrier Shipment Status Message",
            ["RA"] = "Payment Order / Remittance Advice",
            ["RS"] = "Order Status",
            ["SH"] = "Ship Notice / Manifest",
            ["SW"] = "Warehouse Shipping Advice",
        };

    /// <summary>Plain English name for an ST01 code, or null when it is not in the catalog.</summary>
    public static string? DescribeTransactionSet(string? code) =>
        code is not null && TransactionSets.TryGetValue(code, out var name) ? name : null;

    /// <summary>Plain English name for a GS01 code, or null when it is not in the catalog.</summary>
    public static string? DescribeFunctionalGroup(string? code) =>
        code is not null && FunctionalGroups.TryGetValue(code, out var name) ? name : null;
}
