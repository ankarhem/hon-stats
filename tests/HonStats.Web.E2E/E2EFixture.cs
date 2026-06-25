using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Playwright;

namespace HonStats.Web.E2E;

// Self-contained E2E fixture: launches the real web app on a free port with an
// isolated temp DB (so reindex never hits cooldown) and drives a headless
// Chromium page against it. Torn down after the test run.
public sealed class E2EFixture : IAsyncLifetime
{
    private Process? _app;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public string BaseUrl { get; private set; } = "";
    public IPage Page { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var repoRoot = FindRepoRoot();
        var port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        var dbPath = Path.Combine(Path.GetTempPath(), $"honstats-e2e-{Guid.NewGuid():N}.db");

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("--project");
        psi.ArgumentList.Add("src/HonStats.Web/HonStats.Web.csproj");
        psi.ArgumentList.Add("--urls");
        psi.ArgumentList.Add(BaseUrl);
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["HonStats__Persistence__ConnectionString"] = $"Data Source={dbPath}";

        _app = Process.Start(psi) ?? throw new InvalidOperationException("failed to start app");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        for (var i = 0; i < 120; i++)
        {
            if (_app.HasExited)
                throw new InvalidOperationException(
                    "app exited early: " + await _app.StandardError.ReadToEndAsync()
                );
            try
            {
                if ((await http.GetAsync(BaseUrl + "/")).IsSuccessStatusCode)
                    break;
            }
            catch { }
            await Task.Delay(500);
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true }
        );
        Page = await (await _browser.NewContextAsync()).NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
            await _browser.CloseAsync();
        _playwright?.Dispose();
        if (_app is { HasExited: false })
            _app.Kill(entireProcessTree: true);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "HonStats.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? AppContext.BaseDirectory;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<E2EFixture> { }
