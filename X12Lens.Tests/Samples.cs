namespace X12Lens.Tests;

/// <summary>
/// Locates the sample interchanges that ship in <c>samples/</c>. The project file copies
/// them next to the test assembly, so the tests read the same files a user reads.
/// </summary>
internal static class Samples
{
    public const string PurchaseOrder = "850-sample.edi";
    public const string FunctionalAck = "997-sample.edi";
    public const string MultiGroup = "multi-group-sample.edi";
    public const string CustomDelimiters = "custom-delimiters-sample.edi";

    public static string PathTo(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "samples", fileName);

    public static string Read(string fileName) => File.ReadAllText(PathTo(fileName));
}
