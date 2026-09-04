namespace X12Lens.Cli;

/// <summary>
/// Implements <c>x12lens validate</c>: runs the six control-number and count balance checks
/// in <see cref="X12Validator"/> against one interchange and reports what does not add up.
/// </summary>
/// <remarks>
/// This asks a different question than <see cref="InspectCommand"/> does. Inspect shows you
/// what is in the file. Validate asks whether the file agrees with itself: every trailer in
/// X12 repeats a number or a count its header already declared, and nothing in the format
/// stops a sender from getting the repeat wrong. A green <c>inspect</c> and a red
/// <c>validate</c> on the same file is a real, common outcome, not a contradiction, and it
/// is exactly the arithmetic a trading partner's own EDI translator runs before anything
/// else.
/// </remarks>
public sealed class ValidateCommand
{
    private readonly X12Parser _parser;
    private readonly TextWriter _output;

    public ValidateCommand(X12Parser parser, TextWriter output)
    {
        _parser = parser;
        _output = output;
    }

    /// <summary>Parses and validates a file. Returns true when every balance check passes.</summary>
    public async Task<bool> RunAsync(string path, CancellationToken cancellationToken = default)
    {
        var interchange = await _parser.ParseFileAsync(path, cancellationToken).ConfigureAwait(false);
        return Write(interchange, Path.GetFileName(path));
    }

    /// <summary>Validates an already parsed interchange and prints the result. Split out so tests can call it directly.</summary>
    public bool Write(Interchange interchange, string label)
    {
        var result = X12Validator.Validate(interchange);

        if (result.IsValid)
        {
            _output.WriteLine(
                $"{label}: balanced. ISA/IEA, every GS/GE and every ST/SE control number and count match.");
            return true;
        }

        _output.WriteLine($"{label}: {Plural(result.Issues.Count)}.");
        foreach (var issue in result.Issues)
        {
            _output.WriteLine($"  {issue}");
        }

        return false;
    }

    private static string Plural(int count) => count == 1 ? "1 balance issue" : $"{count} balance issues";
}
