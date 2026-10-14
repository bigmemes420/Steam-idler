using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinFormsApp1;

internal sealed class SteamIdle : IDisposable
{
    public const int MaxGames = 34;

    private readonly Dictionary<uint, Process> _processes = new();

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
            }

            foreach (var appId in selected)
            {
                if (_processes.TryGetValue(appId, out var existing) && !existing.HasExited)
                {
                    continue;
                }

                _processes[appId] = Start(appId);
            }
        });
    }

    public void Dispose()
    {
        foreach (var process in _processes.Values)
        {
            Stop(process);
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
        // SAM pumps Steam callbacks on a 100 ms timer while it runs; do the same.
        while (!stop.WaitOne(100))
        {
            pipe.RunCallbacks();
        }
    }

    // The Steam connection below is ported from Steam Achievement Manager (SAM.API Client.cs,
    // Steam.cs, Wrappers/SteamClient018.cs), https://github.com/gibbed/SteamAchievementManager,
    // Copyright (c) 2024 Rick (rick 'at' gibbed 'dot' us), zlib License. Altered: 64-bit
    // steamclient64.dll, reduced to the calls needed to idle, and written as one class.
    private static string ClientLibraryPath()
    {
        // SAM: HKLM\Software\Valve\Steam InstallPath, then <path>\steamclient(64).dll.
        foreach (var keyPath in new[] { @"HKEY_LOCAL_MACHINE\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\Software\WOW6432Node\Valve\Steam" })
        {
            if (Registry.GetValue(keyPath, "InstallPath", null) is string install && File.Exists(Path.Combine(install, "steamclient64.dll")))
            {
                return Path.Combine(install, "steamclient64.dll");
            }
        }

        var path = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam\ActiveProcess", "SteamClientDll64", null) as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new InvalidOperationException("Steam is not installed, or its client library could not be found.");
        }

        return path;
    }

    private sealed class GamePipe : IDisposable
    {
        // ISteamClient018 vtable slots (SAM.API Interfaces/ISteamClient018.cs).
        private const int SlotCreateSteamPipe = 0;
        private const int SlotReleaseSteamPipe = 1;
        private const int SlotConnectToGlobalUser = 2;
        private const int SlotReleaseUser = 4;
        private const int SlotGetISteamUser = 5;
        private const int SlotGetISteamUtils = 9;
        private const int SlotGetISteamUserStats = 13;

        private readonly nint _module;
        private readonly nint _client;
        private readonly BGetCallbackDelegate _getCallback;
        private readonly FreeLastCallbackDelegate _freeLastCallback;
        private int _pipe;
        private int _user;

        private GamePipe(nint module, nint client, BGetCallbackDelegate getCallback, FreeLastCallbackDelegate freeLastCallback)
        {
            _module = module;
            _client = client;
            _getCallback = getCallback;
            _freeLastCallback = freeLastCallback;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);

        public static GamePipe Open(string clientPath, uint appId)
        {
            // Same order as SAM Client.Initialize: SteamAppId, load, CreateInterface, pipe, global user,
            // utils (app id check), then user / user stats / apps interfaces.
            Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());
            var steamDir = Path.GetDirectoryName(clientPath)!;
            SetDllDirectory(steamDir + ";" + Path.Combine(steamDir, "bin"));
            var module = NativeLibrary.Load(clientPath);
            GamePipe? result = null;
            try
            {
                var createInterface = Export<CreateInterfaceDelegate>(module, "CreateInterface");
                var client = createInterface("SteamClient018", 0);
                if (client == 0)
                {
                    throw new InvalidOperationException("Steam did not provide ISteamClient018.");
                }

                result = new GamePipe(module, client,
                    Export<BGetCallbackDelegate>(module, "Steam_BGetCallback"),
                    Export<FreeLastCallbackDelegate>(module, "Steam_FreeLastCallback"));

                result._pipe = result.Slot<CreateSteamPipeDelegate>(SlotCreateSteamPipe)(client);
                if (result._pipe == 0)
                {
                    throw new InvalidOperationException("Steam is not running.");
                }

                result._user = result.Slot<ConnectToGlobalUserDelegate>(SlotConnectToGlobalUser)(client, result._pipe);
                if (result._user == 0)
                {
                    throw new InvalidOperationException("Steam is not running, or no account is logged in to the open Steam client.");
                }

                var utils = result.Slot<GetPipeInterfaceDelegate>(SlotGetISteamUtils)(client, result._pipe, "SteamUtils005");
                if (utils != 0)
                {
                    // ISteamUtils005::GetAppID is slot 9.
                    var getAppId = Marshal.GetDelegateForFunctionPointer<GetAppIdDelegate>(VtableEntry(utils, 9));
                    if (getAppId(utils) != appId)
                    {
                        throw new InvalidOperationException($"Steam reported a different app id than {appId}.");
                    }
                }

                result.Slot<GetUserInterfaceDelegate>(SlotGetISteamUser)(client, result._user, result._pipe, "SteamUser012");
                result.Slot<GetUserInterfaceDelegate>(SlotGetISteamUserStats)(client, result._user, result._pipe, "STEAMUSERSTATS_INTERFACE_VERSION013");
                // SAM also requests ISteamApps001/008, but its delegate omits the `this` argument, so
                // on Steam that call never reaches ISteamClient::GetISteamApps correctly. It is
                // skipped here rather than introducing a call SAM effectively never makes.
                return result;
            }
            catch
            {
                if (result != null)
                {
                    result.Dispose();
                }
                else
                {
                    NativeLibrary.Free(module);
                }

                throw;
            }
        }

        /// <summary>Drains pending callbacks, as SAM does on its callback timer.</summary>
        public void RunCallbacks()
        {
            if (_pipe == 0)
            {
                return;
            }

            while (_getCallback(_pipe, out _, out _) != 0)
            {
                _freeLastCallback(_pipe);
            }
        }

        public void Dispose()
        {
            // SAM Client.Dispose: ReleaseUser, then ReleaseSteamPipe.
            try
            {
                if (_pipe > 0)
                {
                    if (_user > 0)
                    {
                        Slot<ReleaseUserDelegate>(SlotReleaseUser)(_client, _pipe, _user);
                        _user = 0;
                    }

                    Slot<ReleaseSteamPipeDelegate>(SlotReleaseSteamPipe)(_client, _pipe);
                    _pipe = 0;
                }
            }
            catch (Exception)
            {
                // The pipe is released when the process exits.
            }

            NativeLibrary.Free(_module);
        }

        private static nint VtableEntry(nint obj, int slot) => Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), slot * IntPtr.Size);

        private T Slot<T>(int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(VtableEntry(_client, slot));

        private static T Export<T>(nint module, string name) where T : Delegate
            => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, name));

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct CallbackMessage
        {
            public int User;
            public int Id;
            public nint ParamPointer;
            public int ParamSize;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private delegate nint CreateInterfaceDelegate(string version, nint returnCode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate byte BGetCallbackDelegate(int pipe, out CallbackMessage message, out int call);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate byte FreeLastCallbackDelegate(int pipe);
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate int CreateSteamPipeDelegate(nint self);
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate byte ReleaseSteamPipeDelegate(nint self, int pipe);
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate int ConnectToGlobalUserDelegate(nint self, int pipe);
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void ReleaseUserDelegate(nint self, int pipe, int user);
        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        private delegate nint GetPipeInterfaceDelegate(nint self, int pipe, string version);
        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        private delegate nint GetUserInterfaceDelegate(nint self, int user, int pipe, string version);
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate uint GetAppIdDelegate(nint self);
    }
}
