using Microsoft.Win32;

namespace WinFormsApp1;

/// <summary>
/// Undoes the "installed" state the Steam client can record for a game that was only idled.
/// Before a game is idled, its install state is snapshotted. After idling stops, anything Steam
/// created for that game is removed only if the game was not installed before and is still not
/// installed now: an app manifest with no installed flag, no game folder and no size on disk, and
/// the per-user registry flag HKCU\Software\Valve\Steam\Apps\&lt;id&gt;\Installed.
/// Real installs are never touched.
/// </summary>
internal sealed class SteamInstallGuard
{
    private const string AppsKey = @"Software\Valve\Steam\Apps";

    private sealed record Snapshot(bool WasInstalled, HashSet<string> Manifests, bool HadRegistryKey, object? RegistryInstalled);

    private readonly Dictionary<uint, Snapshot> _snapshots = new();
    private readonly object _gate = new();

    public void BeforeIdle(uint appId)
    {
        lock (_gate)
        {
            if (_snapshots.ContainsKey(appId))
            {
                return;
            }

            try
            {
                var folders = SteamLibraryScan.SteamAppsFolders();
                var manifests = folders.Select(f => Path.Combine(f, $"appmanifest_{appId}.acf"))
                    .Where(File.Exists).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var wasInstalled = folders.Any(f => SteamLibraryScan.ManifestIsFullyInstalled(f, appId));
                using var key = Registry.CurrentUser.OpenSubKey($@"{AppsKey}\{appId}");
                _snapshots[appId] = new Snapshot(wasInstalled, manifests, key != null, key?.GetValue("Installed"));
            }
            catch (Exception)
            {
                // Without a snapshot the game is never cleaned up, which is the safe outcome.
            }
        }
    }

    public void AfterIdle(uint appId)
    {
        lock (_gate)
        {
            if (!_snapshots.Remove(appId, out var snapshot) || snapshot.WasInstalled)
            {
                return;
            }

            try
            {
                CleanManifests(appId, snapshot);
                CleanRegistry(appId, snapshot);
            }
            catch (Exception)
            {
                // Cleanup is best effort.
            }
        }
    }

    private static void CleanManifests(uint appId, Snapshot snapshot)
    {
        foreach (var folder in SteamLibraryScan.SteamAppsFolders())
        {
            if (SteamLibraryScan.ManifestIsFullyInstalled(folder, appId))
            {
                return; // It really is installed now (e.g. the user started a download). Leave everything.
            }
        }

        foreach (var folder in SteamLibraryScan.SteamAppsFolders())
        {
            var manifest = Path.Combine(folder, $"appmanifest_{appId}.acf");
            if (snapshot.Manifests.Contains(manifest) || !File.Exists(manifest))
            {
                continue; // Existed before idling (e.g. a paused download) or not there at all.
            }

            var info = SteamLibraryScan.ReadManifest(manifest);
            if (info is not { } m || (m.Flags & 4) != 0 || m.SizeOnDisk != 0)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(m.InstallDir) && Directory.Exists(Path.Combine(folder, "common", m.InstallDir)))
            {
                continue;
            }

            File.Delete(manifest);
        }
    }

    private static void CleanRegistry(uint appId, Snapshot snapshot)
    {
        if (snapshot.RegistryInstalled is int before && before != 0)
        {
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey($@"{AppsKey}\{appId}", writable: true);
        if (key?.GetValue("Installed") is int now && now != 0)
        {
            key.SetValue("Installed", snapshot.RegistryInstalled is int value ? value : 0, RegistryValueKind.DWord);
        }

        if (key != null && key.GetValue("Running") is int running && running != 0)
        {
            key.SetValue("Running", 0, RegistryValueKind.DWord);
        }
    }
}
