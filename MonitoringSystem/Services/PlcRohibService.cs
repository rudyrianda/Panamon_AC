using System.Diagnostics;
using System.Net.Sockets;

namespace MonitoringSystem.Services
{
    /// <summary>
    /// Menjalankan aplikasi PLC ROHIB (Plclogger) bersamaan dengan Panamon,
    /// lalu meneruskan request /plcrohib-app/... ke aplikasi tersebut (reverse proxy).
    /// Konfigurasi di appsettings.json bagian "PlcRohib".
    /// </summary>
    public class PlcRohibLauncher : IHostedService
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<PlcRohibLauncher> _logger;
        private Process? _process;

        public PlcRohibLauncher(IConfiguration cfg, ILogger<PlcRohibLauncher> logger)
        {
            _cfg = cfg;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_cfg.GetValue("PlcRohib:AutoStart", true))
                return Task.CompletedTask;

            var target = new Uri(_cfg["PlcRohib:Url"] ?? "http://localhost:5000");
            if (IsListening(target))
            {
                _logger.LogInformation("PLC ROHIB sudah berjalan di {Url}, tidak dijalankan ulang.", target);
                return Task.CompletedTask;
            }

            var folder = Path.Combine(AppContext.BaseDirectory, _cfg["PlcRohib:Folder"] ?? "PlcRohib");
            var exe = Path.Combine(folder, "Plclogger.exe");
            if (!File.Exists(exe))
            {
                _logger.LogWarning("PLC ROHIB tidak ditemukan di {Exe}. Halaman PLC ROHIB tidak akan tersedia.", exe);
                return Task.CompletedTask;
            }

            try
            {
                var psi = new ProcessStartInfo(exe)
                {
                    WorkingDirectory = folder,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.ArgumentList.Add("--urls");
                psi.ArgumentList.Add(_cfg["PlcRohib:ListenUrls"] ?? "http://0.0.0.0:5000");

                _process = Process.Start(psi);
                if (_process != null)
                {
                    // Output konsol Plclogger (tabel log PLC) tidak ditampilkan di konsol Panamon
                    _process.OutputDataReceived += (_, _) => { };
                    _process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger.LogWarning("[PLC ROHIB] {Line}", e.Data); };
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                    _logger.LogInformation("PLC ROHIB dijalankan (PID {Pid}) dari {Exe}.", _process.Id, exe);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gagal menjalankan PLC ROHIB dari {Exe}.", exe);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Hanya hentikan proses yang dijalankan oleh Panamon sendiri
            try
            {
                if (_process is { HasExited: false })
                    _process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gagal menghentikan PLC ROHIB.");
            }
            return Task.CompletedTask;
        }

        private static bool IsListening(Uri url)
        {
            try
            {
                using var client = new TcpClient();
                return client.ConnectAsync(url.Host, url.Port).Wait(500) && client.Connected;
            }
            catch
            {
                return false;
            }
        }
    }

    public static class PlcRohibProxy
    {
        // Jangan pakai "/plcrohib": bentrok dengan halaman Razor /PlcRohib/Index (routing tidak case-sensitive)
        public const string PathPrefix = "/plcrohib-app";

        private static readonly HashSet<string> SkipRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Host", "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade", "Proxy-Connection", "Cookie", "Accept-Encoding"
        };

        private static readonly HashSet<string> SkipResponseHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade", "Server"
        };

        /// <summary>
        /// Teruskan /plcrohib-app/{path} ke aplikasi PLC ROHIB (default http://localhost:5000/{path}).
        /// Hanya untuk user yang sudah login di Panamon.
        /// </summary>
        public static void MapPlcRohibProxy(this WebApplication app)
        {
            app.Map(PathPrefix + "/{**path}", async (HttpContext ctx, IHttpClientFactory factory, IConfiguration cfg, string? path) =>
            {
                // "/plcrohib-app" -> "/plcrohib-app/" supaya URL relatif di halaman PLC ROHIB (css/, js/, api/) benar
                if (string.IsNullOrEmpty(path) && !(ctx.Request.Path.Value ?? "").EndsWith('/'))
                {
                    ctx.Response.Redirect(PathPrefix + "/" + ctx.Request.QueryString);
                    return;
                }

                var baseUrl = (cfg["PlcRohib:Url"] ?? "http://localhost:5000").TrimEnd('/');
                var targetUri = new Uri($"{baseUrl}/{path}{ctx.Request.QueryString}");

                using var request = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), targetUri);
                if (ctx.Request.ContentLength > 0 || ctx.Request.Headers.ContainsKey("Transfer-Encoding"))
                    request.Content = new StreamContent(ctx.Request.Body);

                foreach (var header in ctx.Request.Headers)
                {
                    if (SkipRequestHeaders.Contains(header.Key)) continue;
                    if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                        request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }

                HttpResponseMessage response;
                try
                {
                    response = await factory.CreateClient("PlcRohib")
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    ctx.Response.StatusCode = StatusCodes.Status502BadGateway;
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    await ctx.Response.WriteAsync(
                        "<div style=\"font-family:sans-serif;padding:24px;color:#b91c1c\">" +
                        "<h3>PLC ROHIB tidak aktif</h3>" +
                        $"<p>Aplikasi PLC ROHIB di <b>{baseUrl}</b> tidak merespons. Pastikan aplikasinya berjalan, lalu muat ulang halaman ini.</p></div>");
                    return;
                }

                using (response)
                {
                    ctx.Response.StatusCode = (int)response.StatusCode;
                    foreach (var header in response.Headers.Concat(response.Content.Headers))
                    {
                        if (SkipResponseHeaders.Contains(header.Key)) continue;
                        ctx.Response.Headers[header.Key] = header.Value.ToArray();
                    }
                    await response.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
                }
            }).RequireAuthorization();
        }
    }
}
