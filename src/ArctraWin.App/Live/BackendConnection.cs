using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ArctraWin.App.Live;

public sealed class BackendConnection(string executable) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Process? process;
    private bool disposed;
    public async Task<JsonElement> SendAsync(object request, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (process is null || process.HasExited)
            {
                process?.Dispose();
                if (!File.Exists(executable)) throw new FileNotFoundException("Live backend is missing. Run scripts/build.ps1.", executable);
                process = new Process { StartInfo = new ProcessStartInfo(executable) {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
                } };
                process.Start();
                _ = process.StandardError.ReadToEndAsync(cancellationToken);
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), timeout.Token).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(timeout.Token).ConfigureAwait(false);
                var line = await process.StandardOutput.ReadLineAsync(timeout.Token).ConfigureAwait(false);
                if (line is null) throw new IOException("Live backend exited without a response.");
                using var response = JsonDocument.Parse(line);
                var root = response.RootElement;
                if (root.GetProperty("schemaVersion").GetInt32() != 1 || !root.GetProperty("ok").GetBoolean())
                    throw new InvalidDataException(root.TryGetProperty("error", out var error) ? error.GetString() : "Invalid backend response.");
                return root.Clone();
            }
            catch { StopProcess(); throw; }
        }
        finally { gate.Release(); }
    }
    private void StopProcess()
    {
        try { if (process is { HasExited: false }) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }
    public void Dispose() { disposed = true; StopProcess(); }
}
