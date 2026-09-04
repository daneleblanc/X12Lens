# X12Lens

[![build](https://github.com/daneleblanc/X12Lens/actions/workflows/ci.yml/badge.svg)](https://github.com/daneleblanc/X12Lens/actions/workflows/ci.yml)

A small .NET 8 library and command line tool that reads an ASC X12 EDI interchange and shows
you what is actually inside it.

I coordinate EDI for a manufacturer, through managed platforms rather than by hand. When a
trading partner says a file was rejected, the useful question is always the same: what is in
the envelope, and where does it stop making sense. X12Lens is the tool that answers that,
written from the public X12 specification.

## What it looks like

```
$ dotnet run --project X12Lens.Cli -- inspect samples/850-sample.edi

File          850-sample.edi
Delimiters    element '*'   component '>'   repetition '^'   segment '~'

Interchange   control 000000101   version 00501   usage P (production)
  Sender      ZZ / DEMOSENDER01
  Receiver    ZZ / DEMORECEIVER01
  Created     2026-07-31 14:22
  Contents    1 functional group, 2 transaction sets, 30 segments

  Group       control 101   PO (Purchase Order)   version 005010
    Transaction set   control 0001   850 Purchase Order   15 segments
    Transaction set   control 0002   850 Purchase Order   11 segments
```

That output is not pasted from memory. It is pinned by a test
(`InspectCommandTests.Inspect_MatchesTheOutputPublishedInTheReadme`), so if the program's
output changes and this README does not, the build fails.

When a file is broken, the tool says where:

```
$ dotnet run --project X12Lens.Cli -- inspect broken.edi
Could not parse broken.edi: The 850 transaction set with control number 0002 is not
terminated: no SE segment. (segment 18, ST)
```

## Validating control-number balances

A file can be perfectly well formed, in the sense above, and still be wrong: X12 gives a
sender no mechanism to enforce that a trailer repeats the number or count its header already
declared. `validate` runs the six checks a trading partner's own EDI translator runs before
anything else:

| Check | What it compares |
|---|---|
| `ISA13_IEA02` | Interchange control number, header vs trailer |
| `IEA01_GROUP_COUNT` | Functional groups declared vs actually present |
| `GS06_GE02` | Functional group control number, header vs trailer |
| `GE01_TRANSACTION_SET_COUNT` | Transaction sets declared for a group vs actually present |
| `ST02_SE02` | Transaction set control number, header vs trailer |
| `SE01_SEGMENT_COUNT` | Segments declared for a transaction set vs `Segments.Count`, ST and SE included |

```
$ dotnet run --project X12Lens.Cli -- validate samples/850-sample.edi
850-sample.edi: balanced. ISA/IEA, every GS/GE and every ST/SE control number and count match.

$ dotnet run --project X12Lens.Cli -- validate broken-count.edi
broken-count.edi: 1 balance issue.
  [SE01_SEGMENT_COUNT] SE declares 99 segments but the interchange has 15. (segment 17, SE)
```

Exit code `3` means the file parsed but did not balance, distinct from `1` (could not parse
at all). A green `inspect` and a red `validate` on the same file is a real, common outcome,
not a contradiction.

## Generating a 997 Functional Acknowledgment

`ack` builds a 997 that reports back to the sender, deciding accept or reject from exactly
the same six balance checks `validate` runs: a transaction set that fails its own ST02/SE02
or SE01 check comes back `AK5*R*5`; a group whose own GS06/GE02 or GE01 does not balance
comes back `AK9` rejected even if every transaction set inside it was individually fine.

```
$ dotnet run --project X12Lens.Cli -- ack samples/850-sample.edi
ISA*00*          *00*          *ZZ*DEMORECEIVER01 *ZZ*DEMOSENDER01   *260903*2340*^*00501*000000001*0*P*>~
GS*FA*DEMORECEIVER01*DEMOSENDER01*20260903*2340*1*X*005010~
ST*997*0001~
AK1*PO*101~
AK2*850*0001~
AK5*A~
AK2*850*0002~
AK5*A~
AK9*A*2*2*2~
SE*8*0001~
GE*1*1~
IEA*1*000000001~
```

`--out <file>` writes the acknowledgment to a file instead of stdout. Scope, stated plainly:
this reports accept or reject at the whole-transaction-set level only. AK3/AK4 segment level
and element level detail are not implemented, so a transaction set can only come back whole
or rejected, never "accepted with errors". Interchange-level problems (ISA13/IEA02, IEA01)
are a TA1 interchange acknowledgment's job in real EDI, not a 997's, and X12Lens does not
generate a TA1.

## Why X12 is harder than it looks

X12 is a flat text format, so the first instinct is to split on `*` and `~` and get on with
your day. That is wrong, and it is wrong in a way that only shows up once you are in
production with a second trading partner.

**The delimiters are not configuration. Every file declares its own, inside itself.** The ISA
segment is the one fixed width segment in the standard, exactly so that a reader can find the
delimiters before it knows how to split anything. It is 106 characters, and the positions
that matter are:

| Position | What lives there |
|---|---|
| 1 to 3 | The literal `ISA` |
| 4 | The element separator. Everything after this is separated by it |
| 83 | ISA11. See below, this one is a trap |
| 85 to 89 | ISA12, the interchange control version, for example `00401` or `00501` |
| 105 | ISA16, the component element separator |
| 106 | The segment terminator |

**ISA11 is the trap.** Through version 00401 it was the interchange control standards
identifier and its value was always the letter `U`. From version 00501 the same position was
repurposed as the repetition separator. So the meaning of character 83 depends on what is
written at characters 85 to 89, and a parser has to read the version before it can interpret
the delimiter. X12Lens does that in `X12Delimiters.Detect`, and it is tolerant of the real
world case where a partner has moved to 00501 but still sends the legacy `U`.

Two other things that bite:

- **Files arrive in two shapes.** Half are pretty printed with one segment per line; half are
  a single 40,000 character line. Both are valid and must parse identically. So line breaks
  have to be stripped, except that a minority of partners use a line feed as their segment
  terminator, in which case stripping line breaks erases every segment boundary in the file.
  `X12Tokenizer` handles this by only removing a line break character the interchange has not
  claimed for itself.
- **Omitted elements are ordinary.** A sender may simply stop early rather than pad out
  trailing separators, so a `PO1` with 6 elements and a `PO1` with 25 are both correct.
  Asking for an element that is not there returns an empty string rather than throwing.

## Design decisions, and why

These are the choices I would expect to be asked about.

**Elements are indexed from 1.** `segment[3]` is BEG03. Every partner specification, every
implementation guide and every support ticket names elements by their 1-based ordinal, so a
parser that silently shifts by one turns every conversation with a trading partner into an
off-by-one argument.

**A transaction set holds its own ST and SE segments.** SE01 declares the number of segments
in the transaction set counting the ST and the SE themselves. Keeping them inside
`TransactionSet.Segments` means `Segments.Count` compares directly against SE01 with no
arithmetic in between, and that comparison is one of the most common real world X12 failures.
`TransactionSet.Body` gives you the content without the wrapper when that is what you want.

**Errors are loud and specific.** Every unexpected segment raises `X12ParseException` carrying
the segment id and its 1-based position, and the message says what was expected. A parser that
quietly skips what it does not understand cannot tell you what is wrong with a file, which is
the entire reason to open one.

**One interchange per document, on purpose.** Batched files with several ISA and IEA pairs are
real, and they are rejected here with a specific message rather than half parsed. The
delimiters are declared per interchange, so a batch could legally change them partway through
the file. That is a real feature, not a footnote, and it is on the roadmap rather than faked.

## Running it

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/daneleblanc/X12Lens.git
cd X12Lens

dotnet test                                                       # run the suite
dotnet run --project X12Lens.Cli -- inspect  samples/850-sample.edi
dotnet run --project X12Lens.Cli -- inspect  samples/997-sample.edi --segments
dotnet run --project X12Lens.Cli -- validate samples/850-sample.edi
dotnet run --project X12Lens.Cli -- ack      samples/850-sample.edi --out 997-out.edi
```

`--segments` lists every segment inside each transaction set with its elements broken out.

Exit codes: `0` success, `1` the file is not a valid interchange, `2` a usage problem, `3`
(`validate` only) the file parsed but did not balance.

## Using the library

```csharp
var interchange = await new X12Parser().ParseFileAsync("order.edi");

Console.WriteLine(interchange.Delimiters.Describe());
Console.WriteLine(interchange.IsProduction ? "production" : "test");

// The envelope tree.
foreach (var group in interchange.Groups)
foreach (var transactionSet in group.TransactionSets)
{
    Console.WriteLine($"{transactionSet.Code} {transactionSet.Name}");
}

// Or ignore the tree and query the whole file.
var quantities = interchange.AllSegments
    .Where(s => s.Id == "PO1")
    .Select(s => s[2]);
```

## Layout

| Project | What it holds |
|---|---|
| `X12Lens.Core` | Delimiter detection, tokenizer, envelope model, parser. No dependencies |
| `X12Lens.Cli` | The `x12lens` executable. Hosts the parser behind a service container |
| `X12Lens.Tests` | xUnit suite, 86 tests, run on every push by GitHub Actions |
| `samples/` | Four synthetic interchanges used both as examples and as test fixtures |

## The sample files

Every `.edi` file in `samples/` was written by hand from the public X12 specification. None of
them came from an employer, a customer or a trading partner, scrubbed or otherwise. The sender
and receiver ids are `DEMOSENDER01` and `DEMORECEIVER01`, the qualifiers are `ZZ` (mutually
defined), and the phone number is in the 555 range. Scrubbing a real interchange reliably is
harder than writing one, so no real interchange was used at any point.

The four files exist to cover different shapes rather than different business scenarios:

| File | What it exercises |
|---|---|
| `850-sample.edi` | 5010 purchase orders, two transaction sets in one group |
| `997-sample.edi` | 4010 functional acknowledgment, no repetition separator, colon component separator, one accepted and one rejected transaction set |
| `multi-group-sample.edi` | Two functional groups of different types in one interchange |
| `custom-delimiters-sample.edi` | Pipe, caret and bang delimiters, and a test rather than production interchange |

## Not done yet

Stated plainly, because a README that implies more than the code does is worse than no README.

- **No AK3/AK4 segment or element level detail in the generated 997.** A transaction set can
  only come back whole or rejected, never "accepted with errors". Real and common, left out
  on purpose.
- **No TA1 interchange acknowledgment.** ISA13/IEA02 and IEA01 balance problems are `validate`
  findings, not something `ack` acts on, because a real TA1 is a different document from a 997.
- **No transaction set schemas.** X12Lens validates the envelope, not the contents of an 850.
  It will happily parse a purchase order with nonsense in it, because envelope structure and
  business content are different problems.
- **No batched interchanges.** See the design note above.
- **No streaming.** Files are read into memory whole, which is fine at EDI file sizes and
  would not be for a 2 GB archive.

## License

MIT. See [LICENSE](LICENSE).
