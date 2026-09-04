namespace X12Lens;

/// <summary>
/// Builds the envelope tree: interchange, then functional groups, then transaction sets.
/// </summary>
/// <remarks>
/// <para>
/// A strict single pass over the segment list. Every unexpected segment is an error with a
/// position and a reason, because the point of this tool is to tell you what is wrong with
/// a file, and a parser that quietly skips what it does not understand cannot do that.
/// </para>
/// <para>
/// Known limitation, deliberate: exactly one interchange per document. Batched files with
/// several ISA and IEA pairs are real and are not supported yet. They are rejected with a
/// specific message rather than half parsed, because the delimiters are declared per
/// interchange and a batch could legally change them partway through the file.
/// </para>
/// </remarks>
public sealed class X12Parser
{
    /// <summary>Reads a file from disk and parses it.</summary>
    public async Task<Interchange> ParseFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Parse(text);
    }

    /// <summary>Parses an interchange already held in memory.</summary>
    /// <exception cref="X12ParseException">The document is not a single well formed interchange.</exception>
    public Interchange Parse(string document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var delimiters = X12Delimiters.Detect(document);
        var segments = X12Tokenizer.Tokenize(document, delimiters);

        if (segments.Count == 0)
        {
            throw new X12ParseException("The document contains no segments.");
        }

        var index = 0;
        var isa = segments[index++];
        if (isa.Id != "ISA")
        {
            throw new X12ParseException("Expected the document to open with an ISA segment.", isa);
        }

        var groups = new List<FunctionalGroup>();
        Segment? iea = null;

        while (index < segments.Count && iea is null)
        {
            var segment = segments[index];
            switch (segment.Id)
            {
                case "GS":
                    groups.Add(ReadFunctionalGroup(segments, ref index));
                    break;

                case "IEA":
                    iea = segment;
                    index++;
                    break;

                case "ISA":
                    throw new X12ParseException(
                        "A second interchange begins here. X12Lens parses one interchange per document.",
                        segment);

                default:
                    throw new X12ParseException(
                        $"Segment {segment.Id} sits directly inside the interchange. " +
                        "Only GS and IEA are allowed between ISA and IEA.",
                        segment);
            }
        }

        if (iea is null)
        {
            throw new X12ParseException(
                $"The interchange with control number {isa[13]} is not terminated: no IEA segment.");
        }

        if (index < segments.Count)
        {
            var trailing = segments[index];
            throw new X12ParseException(
                trailing.Id == "ISA"
                    ? "A second interchange begins after the IEA. X12Lens parses one interchange per document."
                    : $"Segment {trailing.Id} appears after the IEA that closed the interchange.",
                trailing);
        }

        return new Interchange(isa, iea, groups, delimiters, segments);
    }

    private static FunctionalGroup ReadFunctionalGroup(IReadOnlyList<Segment> segments, ref int index)
    {
        var gs = segments[index++];
        var transactionSets = new List<TransactionSet>();
        Segment? ge = null;

        while (index < segments.Count && ge is null)
        {
            var segment = segments[index];
            switch (segment.Id)
            {
                case "ST":
                    transactionSets.Add(ReadTransactionSet(segments, ref index));
                    break;

                case "GE":
                    ge = segment;
                    index++;
                    break;

                case "GS":
                case "IEA":
                case "ISA":
                    throw new X12ParseException(
                        $"The functional group with control number {gs[6]} is not terminated: " +
                        $"expected GE, found {segment.Id}.",
                        segment);

                default:
                    throw new X12ParseException(
                        $"Segment {segment.Id} sits directly inside a functional group. " +
                        "Only ST and GE are allowed between GS and GE.",
                        segment);
            }
        }

        if (ge is null)
        {
            throw new X12ParseException(
                $"The functional group with control number {gs[6]} is not terminated: no GE segment.",
                gs);
        }

        return new FunctionalGroup(gs, ge, transactionSets);
    }

    private static TransactionSet ReadTransactionSet(IReadOnlyList<Segment> segments, ref int index)
    {
        var start = index;
        var st = segments[index++];

        while (index < segments.Count)
        {
            var segment = segments[index];

            if (segment.Id == "SE")
            {
                index++;
                return new TransactionSet(Slice(segments, start, index - start));
            }

            if (segment.Id is "ST" or "GS" or "GE" or "ISA" or "IEA")
            {
                throw new X12ParseException(
                    $"The {st[1]} transaction set with control number {st[2]} is not terminated: " +
                    $"expected SE, found {segment.Id}.",
                    segment);
            }

            index++;
        }

        throw new X12ParseException(
            $"The {st[1]} transaction set with control number {st[2]} is not terminated: no SE segment.",
            st);
    }

    private static IReadOnlyList<Segment> Slice(IReadOnlyList<Segment> source, int start, int count)
    {
        var slice = new Segment[count];
        for (var i = 0; i < count; i++)
        {
            slice[i] = source[start + i];
        }

        return slice;
    }
}
