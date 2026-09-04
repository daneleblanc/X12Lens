using Microsoft.Extensions.DependencyInjection;
using X12Lens;
using X12Lens.Cli;

const int ExitOk = 0;
const int ExitParseError = 1;
const int ExitUsageError = 2;
const int ExitValidationFailed = 3;

var services = new ServiceCollection()
    .AddSingleton<X12Parser>()
    .AddSingleton<TextWriter>(Console.Out)
    .AddSingleton<InspectCommand>()
    .AddSingleton<ValidateCommand>()
    .BuildServiceProvider();

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintUsage();
    return args.Length == 0 ? ExitUsageError : ExitOk;
}

var command = args[0].ToLowerInvariant();

if (command is not ("inspect" or "validate" or "ack"))
{
    Console.Error.WriteLine($"Unknown command '{args[0]}'.");
    PrintUsage();
    return ExitUsageError;
}

var path = args.Skip(1).FirstOrDefault(a => !a.StartsWith('-'));

if (path is null)
{
    Console.Error.WriteLine($"{command} needs a file to look at.");
    PrintUsage();
    return ExitUsageError;
}

if (!File.Exists(path))
{
    Console.Error.WriteLine($"No such file: {path}");
    return ExitUsageError;
}

try
{
    switch (command)
    {
        case "inspect":
            var showSegments = args.Any(a => a is "--segments" or "-s");
            await services.GetRequiredService<InspectCommand>().RunAsync(path, showSegments);
            return ExitOk;

        case "validate":
            var isValid = await services.GetRequiredService<ValidateCommand>().RunAsync(path);
            return isValid ? ExitOk : ExitValidationFailed;

        case "ack":
            var outPath = ReadOptionValue(args, "--out", "-o");
            var interchange = await services.GetRequiredService<X12Parser>().ParseFileAsync(path);
            var acknowledgment = X12AcknowledgmentGenerator.Generate(interchange);

            if (outPath is null)
            {
                Console.Out.Write(acknowledgment);
            }
            else
            {
                await File.WriteAllTextAsync(outPath, acknowledgment);
                Console.Out.WriteLine($"Wrote {outPath}");
            }

            return ExitOk;

        default:
            return ExitUsageError;
    }
}
catch (X12ParseException ex)
{
    Console.Error.WriteLine($"Could not parse {Path.GetFileName(path)}: {ex.Message}");
    return ExitParseError;
}

static string? ReadOptionValue(string[] commandArgs, params string[] flags)
{
    for (var i = 0; i < commandArgs.Length - 1; i++)
    {
        if (flags.Contains(commandArgs[i], StringComparer.OrdinalIgnoreCase))
        {
            return commandArgs[i + 1];
        }
    }

    return null;
}

static void PrintUsage()
{
    Console.WriteLine("x12lens - look inside an X12 EDI interchange.");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  x12lens inspect  <file> [--segments]");
    Console.WriteLine("  x12lens validate <file>");
    Console.WriteLine("  x12lens ack      <file> [--out <file>]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --segments, -s   inspect: list every segment inside each transaction set.");
    Console.WriteLine("  --out, -o        ack: write the generated 997 to a file instead of stdout.");
    Console.WriteLine();
    Console.WriteLine(
        "Exit codes: 0 ok, 1 not a valid interchange, 2 usage problem, 3 validate found an unbalanced count.");
}
