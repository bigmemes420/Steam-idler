using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinFormsApp1;

internal sealed class SteamIdle : IDisposable
{
    public const int MaxGames = 34;

    private readonly Dictionary<uint, Process> _processes = new();
    private readonly SteamInstallGuard _guard = new();

    public bool IsConnected => _processes.Values.Any(process => !process.HasExited);

    public IReadOnlyList<uint> Playing => _processes.Keys.ToArray();

    public Task SetPlayingAsync(IReadOnlyList<uint> appIds)
    {
        var selected = appIds.Distinct().Take(MaxGames).ToArray();
        return Task.Run(() =>
        {
            foreach (var appId in _processes.Keys.Except(selected).ToArray())
            {
                Stop(_processes[appId]);
                _processes.Remove(appId);
                _guard.AfterIdle(appId);
            }

            foreach (var appId in selected)
            {
                if (_processes.TryGetValue(appId, out var existing) && !existing.HasExited)
                {
                    continue;
                }

                _guard.BeforeIdle(appId);
                _processes[appId] = Start(appId);
            }
        });
    }

    public void Dispose()
    {
        foreach (var (appId, process) in _processes)
        {
            Stop(process);
            _guard.AfterIdle(appId);
        }

        _processes.Clear();
    }

    private static Process Start(uint appId)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? throw new InvalidOperationException("The application path is not available."),
                Arguments = $"--idle {appId}",
                UseShellExecute = false,
                RedirectStandardInput = true,
                EnvironmentVariables = { ["SteamAppId"] = appId.ToString() },
                CreateNoWindow = true,
            },
        };
        process.Start();
        return process;
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                // Ask the child to release its Steam pipe and exit cleanly, so Steam sees the
                // game close normally instead of a crashed process.
                try
                {
                    process.StandardInput.Close();
                }
                catch (Exception)
                {
                }

                if (!process.WaitForExit(3000))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }
            }
        }
        catch (Exception)
        {
            // The process has already gone away.
        }
        finally
        {
            process.Dispose();
        }
    }

    public static void Hold(uint appId)
    {
        using var pipe = GamePipe.Open(ClientLibraryPath(), appId);
        using var stop = new ManualResetEvent(false);
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stop.Set();
        };
        // The parent closes our standard input to ask us to stop.
        var reader = new Thread(() =>
        {
            try
            {
                using var input = Console.OpenStandardInput();
                var buffer = new byte[64];
                while (input.Read(buffer, 0, buffer.Length) > 0)
                {
                }
            }
            catch (Exception)
            {
            }

            stop.Set();
        }) { IsBackground = true };
        reader.Start();
        stop.WaitOne();
    }

    private static string ClientLibraryPath()
    {
        var path = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam\ActiveProcess", "SteamClientDll64", null) as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new InvalidOperationException("Steam is not installed, or its client library could not be found.");
        }

        return path;
    }

    private sealed class GamePipe : IDisposable
    {
        private readonly nint _module;
        private readonly int _pipe;

        private GamePipe(nint module, int pipe)
        {
            _module = module;
            _pipe = pipe;
        }

        public static GamePipe Open(string clientPath, uint appId)
        {
            var module = NativeLibrary.Load(clientPath);
            try
            {
                var createPipe = Export<CreatePipeDelegate>(module, "Steam_CreateSteamPipe");
                var connectUser = Export<ConnectUserDelegate>(module, "Steam_ConnectToGlobalUser");
                var loggedOn = Export<LoggedOnDelegate>(module, "Steam_BLoggedOn");
                var pipe = createPipe();
                var user = connectUser(pipe);
                if (pipe == 0 || user == 0 || loggedOn(user, pipe) == 0)
                {
                    throw new InvalidOperationException("Steam is not running, or no account is logged in to the open Steam client.");
                }

                return new GamePipe(module, pipe);
            }
            catch
            {
                NativeLibrary.Free(module);
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                Export<ReleasePipeDelegate>(_module, "Steam_BReleaseSteamPipe")(_pipe);
            }
            catch (Exception)
            {
                // The pipe is released when the process exits.
            }

            NativeLibrary.Free(_module);
        }

        private static T Export<T>(nint module, string name) where T : Delegate
            => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, name));

        private delegate int CreatePipeDelegate();
        private delegate int ConnectUserDelegate(int pipe);
        private delegate byte LoggedOnDelegate(int user, int pipe);
        private delegate void ReleasePipeDelegate(int pipe);
    }
}
