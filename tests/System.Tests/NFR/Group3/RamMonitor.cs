using System.Diagnostics;

namespace System.Tests.NFR.Group3;

/// <summary>
/// Đo working set của tiến trình API (PID trong file NFR_API_PID_FILE). Quá ngưỡng bảo vệ máy thì kill API
/// và khởi động lại bằng NFR_API_RESTART để các ca sau vẫn chạy được; ca đang đo vẫn Failed.
/// </summary>
public sealed class RamMonitor : IDisposable
{
    private const double GuardMb = 2500;
    private readonly Process _api;
    private readonly double _baseline;
    private double _peak;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    public bool Killed { get; private set; }

    public RamMonitor()
    {
        _api = Process.GetProcessById(int.Parse(File.ReadAllText(G3Env.Required("NFR_API_PID_FILE")).Trim()));
        _baseline = _peak = Mb();
        _loop = Task.Run(async () =>
        {
            while (!_stop.IsCancellationRequested && !_api.HasExited)
            {
                var now = Mb();
                _peak = Math.Max(_peak, now);
                if (now - _baseline > GuardMb)
                {
                    Killed = true;
                    _api.Kill();
                }
                await Task.Delay(50);
            }
        });
    }

    private double Mb()
    {
        _api.Refresh();
        return _api.HasExited ? 0 : _api.WorkingSet64 / 1048576.0;
    }

    public bool Alive
    {
        get { _api.Refresh(); return !_api.HasExited; }
    }

    public double PeakDeltaMb() => _peak - _baseline;

    public void Dispose()
    {
        _stop.Cancel();
        _loop.Wait(2000);
        if (!Alive) Restart();
    }

    private static void Restart()
    {
        var psi = new ProcessStartInfo("powershell") { UseShellExecute = false };
        foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", G3Env.Required("NFR_API_RESTART") }) psi.ArgumentList.Add(a);
        Process.Start(psi)!.WaitForExit(30_000);
        using var http = new HttpClient { BaseAddress = G3Env.ApiUrl, Timeout = TimeSpan.FromSeconds(3) };
        for (var i = 0; i < 60; i++)
        {
            try
            {
                http.GetAsync("/api/v1/common/auth/me").GetAwaiter().GetResult();
                return;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                Task.Delay(1000).Wait();
            }
        }
    }
}
