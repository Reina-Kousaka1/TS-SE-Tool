namespace TSSE.Desktop.Services;

/// <summary>Fixed CI-only milestones; never writes configuration, paths or save data.</summary>
internal static class SmokeTrace
{
    internal static void Mark(string stage)
    {
        if (Environment.GetEnvironmentVariable("TSSE_DESKTOP_CI_SMOKE_TEST") == "1")
            Console.Error.WriteLine("TSSE_SMOKE " + stage);
    }
}
