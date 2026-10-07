using System.Globalization;
using System.Text;

namespace AqualLifeStyle.Customer11Canonicalisation;

internal static class Program
{
    internal static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            if (args.Length != 4 || args[0] != "--manifest" || args[2] != "--capture")
                throw new AdapterValidationException("Arguments");
            // File-only input. No host, service provider, environment connection string or remote client.
            var input = Customer11ManifestReader.Read(File.ReadAllBytes(args[1]), File.ReadAllBytes(args[3]));
            var result = Customer11CanonicalisationAdapter.Invoke(input);
            output.Write(Encoding.UTF8.GetString(CanonicalOutputWriter.Write(input, result)));
            return 0;
        }
        catch (AdapterValidationException ex)
        {
            error.WriteLine("REJECTED: " + ex.Category);
            return 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // Do not print exception messages, source contents or source paths.
            error.WriteLine("REJECTED: InputReadOrJson");
            return 2;
        }
    }

    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        Console.OutputEncoding = new UTF8Encoding(false);
        return Run(args, Console.Out, Console.Error);
    }
}
