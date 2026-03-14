using Avalonia;

namespace AvaloniaCapabilityDemo;

internal static class Program
{
    internal static string? StartupPipePath { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        StartupPipePath = ParsePipePath(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

    private static string? ParsePipePath(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.Equals("--pipe", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                return args[i + 1];
            }

            if (arg.StartsWith("--pipe=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[7..];
            }
        }

        return null;
    }
}
