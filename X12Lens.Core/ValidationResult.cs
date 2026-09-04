namespace X12Lens;

/// <summary>
/// The outcome of running <see cref="X12Validator.Validate"/> against one interchange.
/// </summary>
public sealed class ValidationResult
{
    internal ValidationResult(IReadOnlyList<ValidationIssue> issues)
    {
        Issues = issues;
    }

    /// <summary>Every balance mismatch found, in document order. Empty when the interchange agrees with itself.</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; }

    /// <summary>True when every control number and every declared count matches what the document actually contains.</summary>
    public bool IsValid => Issues.Count == 0;
}
