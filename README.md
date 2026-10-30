# X12Lens

[![build](https://github.com/daneleblanc/X12Lens/actions/workflows/ci.yml/badge.svg)](https://github.com/daneleblanc/X12Lens/actions/workflows/ci.yml)

A small .NET 8 library and command line tool that reads an ASC X12 EDI interchange and tells
you what's in it.

I coordinate EDI for a manufacturer, through managed platforms rather than by hand. When a
trading partner says a file was rejected, the question is always the same: what's in the
envelope, and where does it stop making sense. I wrote X12Lens to answer that from the public
X12 spec, without a translator in the middle.

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

A test (`InspectCommandTests.Inspect_MatchesTheOutputPublishedInTheReadme`) pins that output,
so if the program changes and this README doesn't, the build fails.

When a file is broken, it says where:

```
$ dotnet run --project X12Lens.Cli -- inspect broken.edi
Could not parse broken.edi: The 850 transaction set with control number 0002 is not
terminated: no SE segment. (segment 18, ST)
```

## Validating control numbers and counts

A file can parse fine and still be wrong. X12 doesn't make a sender prove that a trailer
repeats the control number or count its header declared, so `validate` runs the 6 checks a
trading partner's translator runs before it looks at anything else:

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

Exit code `3` means the file parsed but didn't balance. `1` means it couldn't be parsed at
all. A green `inspect` and a red `validate` on the same file happens a lot in practice.

## Generating a 997

`ack` builds a 997 Functional Acknowledgment back to the sender. Accept or reject comes from the
same 6 checks `validate` runs: a transaction set that fails its own ST02/SE02 or SE01 check
comes back `AK5*R*5`, and a group whose GS06/GE02 or GE01 doesn't balance comes back `AK9`
rejected even if every transaction set inside it was fine on its own.

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

`--out <file>` writes it to a file instead of stdout.

Scope: this is accept/reject at the transaction set level only. There's no AK3/AK4 segment or
element detail, so a set comes back whole or rejected, never "accepted with errors".
Interchange-level problems (ISA13/IEA02, IEA01) belong to a TA1, which is a different document,
and X12Lens doesn't generate one.

## Why X12 is harder than it looks

X12 is flat text, so the first instinct is to split on `*` and `~` and move on. That works
until the second trading partner shows up.

**The delimiters aren't configuration. Every file declares its own, inside itself.** The ISA
segment is the one fixed width segment in the standard, so a reader can find the delimiters
before it knows how to split anything. It's 106 characters, and the positions that matter are:

| Position | What lives there |
|---|---|
| 1 to 3 | The literal `ISA` |
| 4 | The element separator. Everything after this is separated by it |
| 83 | ISA11. See below, this one is a trap |
| 85 to 89 | ISA12, the interchange control version, for example `00401` or `00501` |
| 105 | ISA16, the component element separator |
| 106 | The segment terminator |

**ISA11 changed meaning between versions.** Through 00401 it was the interchange control
standards identifier and was always the letter `U`. From 00501 the same position became the
repetition separator. So what character 83 means depends on what's written at 85 to 89, and the
parser has to read the version before it can read that delimiter. `X12Delimiters.Detect` does
that, and it tolerates the common case where a partner moved to 00501 but still sends `U`.

2 other things that bite:

- **Files arrive in 2 shapes.** Half are pretty printed with one segment per line, half are one
  40,000 character line. Both are valid and have to parse the same. So line breaks get stripped,
  except some partners use a line feed as their segment terminator, and stripping those erases
  every segment boundary in the file. `X12Tokenizer` only removes a line break character the
  interchange hasn't claimed for itself.
- **Omitted elements are normal.** A sender can just stop early instead of padding out trailing
  separators, so a `PO1` with 6 elements and a `PO1` with 25 are both correct. Asking for an
  element that isn't there returns an empty string instead of throwing.

## Design decisions

The choices I'd expect to be asked about:

**Elements are indexed from 1.** `segment[3]` is BEG03. Every partner spec, implementation
guide and support ticket names elements by their 1-based ordinal. A parser that shifts by one
turns every conversation with a trading partner into an off-by-one argument.

**A transaction set keeps its own ST and SE.** SE01 counts the ST and SE themselves, so keeping
them inside `TransactionSet.Segments` means `Segments.Count` compares straight against SE01 with
no arithmetic. That comparison is one of the most common X12 failures I see.
`TransactionSet.Body` gives you the contents without the wrapper when you want that.

**Errors say what and where.** Every unexpected segment raises `X12ParseException` with the
segment id, its 1-based position, and what was expected. A parser that skips what it doesn't
understand can't tell you what's wrong with the file, and that's the reason I'm opening it.

**One interchange per document.** Batched files with several ISA/IEA pairs exist, and they're
rejected with a specific message instead of half parsed. Delimiters are declared per
interchange, so a batch could legally change them partway through. That's on the roadmap, not
faked.

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

Every `.edi` file in `samples/` was written by hand from the public X12 spec. None came from an
employer, a customer or a trading partner, scrubbed or otherwise. Sender and receiver are
`DEMOSENDER01` and `DEMORECEIVER01`, the qualifiers are `ZZ` (mutually defined), and the phone
number is a 555. Scrubbing a real interchange properly is harder than writing one, so I didn't
try.

The 4 files cover different shapes, not different business scenarios:

| File | What it exercises |
|---|---|
| `850-sample.edi` | 5010 purchase orders, two transaction sets in one group |
| `997-sample.edi` | 4010 functional acknowledgment, no repetition separator, colon component separator, one accepted and one rejected transaction set |
| `multi-group-sample.edi` | Two functional groups of different types in one interchange |
| `custom-delimiters-sample.edi` | Pipe, caret and bang delimiters, and a test rather than production interchange |

## Not done yet

- **No AK3/AK4 segment or element detail in the 997.** A transaction set comes back whole or
  rejected, never "accepted with errors".
- **No TA1.** ISA13/IEA02 and IEA01 problems show up in `validate`, but `ack` doesn't act on
  them, since a TA1 is a different document from a 997.
- **No transaction set schemas.** X12Lens validates the envelope, not the contents of an 850. It
  will parse a purchase order with nonsense in it without complaint. Envelope structure and
  business content are different problems.
- **No batched interchanges.** See the design note above.
- **No streaming.** Files are read into memory whole. Fine at EDI file sizes, not fine for a
  2 GB archive.

## License

MIT. See [LICENSE](LICENSE).
