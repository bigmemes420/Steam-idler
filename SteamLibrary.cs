using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace WinFormsApp1;

internal sealed record SteamAccount(string AccountName, string PersonaName, ulong SteamId64, uint SteamId32, bool MostRecent);

internal sealed record SteamGame(uint AppId, string Name, string Type, bool Installed);

internal sealed class SteamLibraryScan
{
    public required string SteamPath { get; init; }
    public required SteamAccount Account { get; init; }
    public required IReadOnlyList<SteamGame> Games { get; init; }
    public int PackageCount { get; init; }
    public int UnmappedPackageCount { get; init; }
    public int UnnamedAppCount { get; init; }

    public static SteamLibraryScan Run()
    {
        var steamPath = FindSteamPath();
        var account = ReadMostRecentAccount(steamPath);
        var packages = ReadOwnedPackages(Path.Combine(steamPath, "userdata", account.SteamId32.ToString(), "config", "licensecache"), account.SteamId32);
        var packageApps = ReadPackageApps(Path.Combine(steamPath, "appcache", "packageinfo.vdf"));
        var appInfo = ReadAppInfo(Path.Combine(steamPath, "appcache", "appinfo.vdf"));
        var installed = ReadInstalledAppIds(Path.Combine(steamPath, "config", "libraryfolders.vdf"));

        var appIds = new SortedSet<uint>();
        var unmapped = 0;
        foreach (var packageId in packages)
        {
            if (!packageApps.TryGetValue(packageId, out var apps))
            {
                unmapped++;
                continue;
            }

            foreach (var appId in apps)
            {
                appIds.Add(appId);
            }
        }

        var games = new List<SteamGame>();
        var unnamed = 0;
        foreach (var appId in appIds)
        {
            appInfo.TryGetValue(appId, out var info);
            var name = info.Name;
            var type = info.Type;
            if (string.IsNullOrEmpty(name))
            {
                unnamed++;
                name = "(unknown)";
                type ??= "";
            }

            if (!IsGame(type))
            {
                continue;
            }

            games.Add(new SteamGame(appId, name, type!, installed.Contains(appId)));
        }

        games.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return new SteamLibraryScan
        {
            SteamPath = steamPath,
            Account = account,
            Games = games,
            PackageCount = packages.Count,
            UnmappedPackageCount = unmapped,
            UnnamedAppCount = unnamed,
        };
    }

    public string ToJson()
    {
        var document = new
        {
            scannedAt = DateTimeOffset.Now,
            steamPath = SteamPath,
            account = new
            {
                accountName = Account.AccountName,
                personaName = Account.PersonaName,
                steamId64 = Account.SteamId64.ToString(),
                steamId32 = Account.SteamId32,
            },
            packageCount = PackageCount,
            gameCount = Games.Count,
            games = Games.Select(game => new
            {
                appid = game.AppId,
                name = game.Name,
                type = game.Type,
                installed = game.Installed,
            }),
        };

        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
    }

    private static bool IsGame(string? type)
    {
        return type != null && type.Equals("game", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindSteamPath()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
            using var key = baseKey.OpenSubKey(@"Software\Valve\Steam");
            var path = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                return path;
            }
        }

        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        if (Directory.Exists(fallback))
        {
            return fallback;
        }

        throw new InvalidOperationException("Steam is not installed for this user.");
    }

    private static SteamAccount ReadMostRecentAccount(string steamPath)
    {
        var users = ReadTextVdf(Path.Combine(steamPath, "config", "loginusers.vdf"));
        if (!users.Children.TryGetValue("users", out var root))
        {
            throw new InvalidDataException("loginusers.vdf has no users section.");
        }

        SteamAccount? newest = null;
        ulong newestTimestamp = 0;
        foreach (var (steamIdText, user) in root.Children)
        {
            if (!ulong.TryParse(steamIdText, out var steamId64))
            {
                continue;
            }

            _ = ulong.TryParse(user.Values.GetValueOrDefault("Timestamp"), out var timestamp);
            var account = new SteamAccount(
                user.Values.GetValueOrDefault("AccountName") ?? "",
                user.Values.GetValueOrDefault("PersonaName") ?? "",
                steamId64,
                (uint)(steamId64 & 0xFFFFFFFF),
                user.Values.GetValueOrDefault("MostRecent") == "1");

            if (account.MostRecent || timestamp >= newestTimestamp)
            {
                newest = account;
                newestTimestamp = timestamp;
            }
        }

        return newest ?? throw new InvalidDataException("No Steam account has logged in on this machine.");
    }

    private static HashSet<uint> ReadOwnedPackages(string path, uint steamId32)
    {
        var encrypted = File.ReadAllBytes(path);
        if (encrypted.Length < 8)
        {
            throw new InvalidDataException("The Steam license cache is empty.");
        }

        var decrypted = LicenseCacheCipher.Decrypt(unchecked((int)steamId32), encrypted);
        var body = decrypted.AsSpan(0, decrypted.Length - 4);
        var packages = new HashSet<uint>();
        var offset = 0;
        while (offset < body.Length)
        {
            var key = ReadVarint(body, ref offset);
            var field = (int)(key >> 3);
            var wire = (int)(key & 7);
            if (field == 2 && wire == 2)
            {
                var length = (int)ReadVarint(body, ref offset);
                var license = body.Slice(offset, length);
                offset += length;
                ReadLicense(license, packages);
            }
            else
            {
                SkipProtobuf(body, ref offset, wire);
            }
        }

        return packages;
    }

    private static void ReadLicense(ReadOnlySpan<byte> license, HashSet<uint> packages)
    {
        var offset = 0;
        while (offset < license.Length)
        {
            var key = ReadVarint(license, ref offset);
            var field = (int)(key >> 3);
            var wire = (int)(key & 7);
            if (wire == 4)
            {
                return;
            }

            if (field == 1 && wire == 0)
            {
                packages.Add((uint)ReadVarint(license, ref offset));
            }
            else if (wire == 3)
            {
                var groupStart = offset;
                SkipProtobuf(license, ref offset, wire);
                ReadLicense(license[groupStart..offset], packages);
            }
            else
            {
                SkipProtobuf(license, ref offset, wire);
            }
        }
    }

    private static Dictionary<uint, List<uint>> ReadPackageApps(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream);
        var magic = reader.ReadUInt32();
        var version = magic & 0xFF;
        if ((magic >> 8) != 0x065655 || version is < 39 or > 40)
        {
            throw new InvalidDataException($"Unsupported packageinfo.vdf header {magic:X8}.");
        }

        _ = reader.ReadUInt32();
        var packages = new Dictionary<uint, List<uint>>();
        while (stream.Position < stream.Length)
        {
            var packageId = reader.ReadUInt32();
            if (packageId == 0xFFFFFFFF)
            {
                break;
            }

            stream.Position += 24;
            if (version >= 40)
            {
                stream.Position += 8;
            }

            var root = BinaryVdf.ReadObject(stream);
            var body = root.Children.Values.FirstOrDefault();
            var appIds = new List<uint>();
            if (body != null && body.Children.TryGetValue("appids", out var apps))
            {
                foreach (var value in apps.Values.Values)
                {
                    if (uint.TryParse(value, out var appId))
                    {
                        appIds.Add(appId);
                    }
                }
            }

            packages[packageId] = appIds;
        }

        return packages;
    }

    private static Dictionary<uint, (string? Name, string? Type)> ReadAppInfo(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream);
        var magic = reader.ReadUInt32();
        var version = magic & 0xFF;
        if ((magic >> 8) != 0x075644 || version is < 39 or > 41)
        {
            throw new InvalidDataException($"Unsupported appinfo.vdf header {magic:X8}.");
        }

        _ = reader.ReadUInt32();
        IReadOnlyList<string>? stringTable = null;
        if (version >= 41)
        {
            var tableOffset = reader.ReadInt64();
            var resume = stream.Position;
            stream.Position = tableOffset;
            var count = reader.ReadUInt32();
            var table = new string[count];
            for (var index = 0; index < count; index++)
            {
                table[index] = BinaryVdf.ReadString(stream);
            }

            stringTable = table;
            stream.Position = resume;
        }

        var apps = new Dictionary<uint, (string? Name, string? Type)>();
        while (stream.Position < stream.Length)
        {
            var appId = reader.ReadUInt32();
            if (appId == 0)
            {
                break;
            }

            var size = reader.ReadUInt32();
            var end = stream.Position + size;
            stream.Position += version >= 40 ? 60 : 40;
            var root = BinaryVdf.ReadObject(stream, stringTable);
            if (stream.Position != end)
            {
                stream.Position = end;
            }

            VdfNode? common = null;
            if (root.Children.TryGetValue("appinfo", out var appInfo))
            {
                appInfo.Children.TryGetValue("common", out common);
            }

            common ??= root.Children.GetValueOrDefault("common");
            apps[appId] = (common?.Values.GetValueOrDefault("name"), common?.Values.GetValueOrDefault("type"));
        }

        return apps;
    }

    private static HashSet<uint> ReadInstalledAppIds(string path)
    {
        var installed = new HashSet<uint>();
        if (!File.Exists(path))
        {
            return installed;
        }

        var root = ReadTextVdf(path);
        if (!root.Children.TryGetValue("libraryfolders", out var folders))
        {
            return installed;
        }

        foreach (var folder in folders.Children.Values)
        {
            if (!folder.Children.TryGetValue("apps", out var apps))
            {
                continue;
            }

            // libraryfolders.vdf can list apps that Steam has only seen run
            // (for example an idled game), so require a fully installed
            // app manifest in that library before calling a game installed.
            if (!folder.Values.TryGetValue("path", out var libraryPath) || string.IsNullOrWhiteSpace(libraryPath))
            {
                continue;
            }

            var steamApps = Path.Combine(libraryPath, "steamapps");
            foreach (var appIdText in apps.Values.Keys)
            {
                if (uint.TryParse(appIdText, out var appId) && IsFullyInstalled(steamApps, appId))
                {
                    installed.Add(appId);
                }
            }
        }

        return installed;
    }

    internal static IReadOnlyList<string> SteamAppsFolders()
    {
        var steamPath = FindSteamPath();
        var folders = new List<string> { Path.Combine(steamPath, "steamapps") };
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        try
        {
            if (File.Exists(vdf) && ReadTextVdf(vdf).Children.TryGetValue("libraryfolders", out var libraries))
            {
                foreach (var library in libraries.Children.Values)
                {
                    if (library.Values.TryGetValue("path", out var libraryPath) && !string.IsNullOrWhiteSpace(libraryPath))
                    {
                        folders.Add(Path.Combine(libraryPath, "steamapps"));
                    }
                }
            }
        }
        catch (Exception)
        {
            // Fall back to the main library only.
        }

        return folders.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static bool ManifestIsFullyInstalled(string steamApps, uint appId) => IsFullyInstalled(steamApps, appId);

    /// <summary>Reads StateFlags, installdir and SizeOnDisk from an app manifest, or null if unreadable.</summary>
    internal static (uint Flags, string? InstallDir, ulong SizeOnDisk)? ReadManifest(string manifest)
    {
        try
        {
            if (!ReadTextVdf(manifest).Children.TryGetValue("AppState", out var state))
            {
                return null;
            }

            state.Values.TryGetValue("StateFlags", out var flagsText);
            state.Values.TryGetValue("installdir", out var installDir);
            state.Values.TryGetValue("SizeOnDisk", out var sizeText);
            uint.TryParse(flagsText, out var flags);
            ulong.TryParse(sizeText, out var size);
            return (flags, installDir, size);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsFullyInstalled(string steamApps, uint appId)
    {
        var manifest = Path.Combine(steamApps, $"appmanifest_{appId}.acf");
        if (!File.Exists(manifest))
        {
            return false;
        }

        try
        {
            var root = ReadTextVdf(manifest);
            if (!root.Children.TryGetValue("AppState", out var state))
            {
                return false;
            }

            // StateFlags bit 4 is "fully installed".
            if (!state.Values.TryGetValue("StateFlags", out var flagsText) || !uint.TryParse(flagsText, out var flags) || (flags & 4) == 0)
            {
                return false;
            }

            if (state.Values.TryGetValue("installdir", out var installDir) && !string.IsNullOrWhiteSpace(installDir))
            {
                return Directory.Exists(Path.Combine(steamApps, "common", installDir));
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static VdfNode ReadTextVdf(string path)
    {
        var text = File.ReadAllText(path);
        var offset = 0;
        SkipWhitespace(text, ref offset);
        var root = new VdfNode();
        while (offset < text.Length)
        {
            if (text[offset] == '}')
            {
                break;
            }

            var name = ReadQuoted(text, ref offset);
            SkipWhitespace(text, ref offset);
            if (offset < text.Length && text[offset] == '{')
            {
                offset++;
                root.Children[name] = ReadTextVdfBlock(text, ref offset);
            }
            else
            {
                root.Values[name] = ReadQuoted(text, ref offset);
            }

            SkipWhitespace(text, ref offset);
        }

        return root;
    }

    private static VdfNode ReadTextVdfBlock(string text, ref int offset)
    {
        var node = new VdfNode();
        while (offset < text.Length)
        {
            SkipWhitespace(text, ref offset);
            if (offset >= text.Length)
            {
                break;
            }

            if (text[offset] == '}')
            {
                offset++;
                break;
            }

            var name = ReadQuoted(text, ref offset);
            SkipWhitespace(text, ref offset);
            if (offset < text.Length && text[offset] == '{')
            {
                offset++;
                node.Children[name] = ReadTextVdfBlock(text, ref offset);
            }
            else
            {
                node.Values[name] = ReadQuoted(text, ref offset);
            }
        }

        return node;
    }

    private static string ReadQuoted(string text, ref int offset)
    {
        if (offset >= text.Length || text[offset] != '"')
        {
            throw new InvalidDataException("Expected a quoted VDF value.");
        }

        offset++;
        var start = offset;
        var escaped = false;
        var value = new StringBuilder();
        while (offset < text.Length)
        {
            var ch = text[offset++];
            if (escaped)
            {
                value.Append(ch);
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == '"')
            {
                return value.Length == 0 ? text[start..(offset - 1)] : value.ToString();
            }

            value.Append(ch);
        }

        throw new InvalidDataException("Unterminated quoted VDF value.");
    }

    private static void SkipWhitespace(string text, ref int offset)
    {
        while (offset < text.Length && char.IsWhiteSpace(text[offset]))
        {
            offset++;
        }
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> data, ref int offset)
    {
        ulong value = 0;
        var shift = 0;
        while (offset < data.Length)
        {
            var current = data[offset++];
            value |= (ulong)(current & 0x7F) << shift;
            if (current < 128)
            {
                return value;
            }

            shift += 7;
        }

        throw new InvalidDataException("Truncated protobuf varint.");
    }

    private static void SkipProtobuf(ReadOnlySpan<byte> data, ref int offset, int wire)
    {
        switch (wire)
        {
            case 0:
                _ = ReadVarint(data, ref offset);
                break;
            case 1:
                offset += 8;
                break;
            case 2:
                var length = (int)ReadVarint(data, ref offset);
                if (length < 0 || offset > data.Length - length)
                {
                    throw new InvalidDataException($"Length-delimited field of {length} bytes runs past the buffer at {offset}.");
                }

                offset += length;
                break;
            case 3:
                var depth = 1;
                while (offset < data.Length && depth > 0)
                {
                    var key = ReadVarint(data, ref offset);
                    var groupWire = (int)(key & 7);
                    if (groupWire == 3)
                    {
                        depth++;
                    }
                    else if (groupWire == 4)
                    {
                        depth--;
                    }
                    else
                    {
                        SkipProtobuf(data, ref offset, groupWire);
                    }
                }

                break;
            case 5:
                offset += 4;
                break;
            default:
                throw new InvalidDataException($"Unsupported protobuf wire type {wire}.");
        }
    }

    private sealed class VdfNode
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, VdfNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static class BinaryVdf
    {
        public static VdfNode ReadObject(Stream stream, IReadOnlyList<string>? stringTable = null)
        {
            var node = new VdfNode();
            while (true)
            {
                var type = stream.ReadByte();
                if (type < 0)
                {
                    throw new EndOfStreamException("Unexpected end of binary VDF.");
                }

                if (type == 0x08)
                {
                    return node;
                }

                var name = ReadKey(stream, stringTable);
                switch (type)
                {
                    case 0x00:
                        node.Children[name] = ReadObject(stream, stringTable);
                        break;
                    case 0x01:
                        node.Values[name] = ReadString(stream);
                        break;
                    case 0x02:
                        node.Values[name] = ReadInt32(stream).ToString();
                        break;
                    case 0x03:
                        _ = ReadInt32(stream);
                        break;
                    case 0x07:
                    case 0x0A:
                        stream.Position += 8;
                        break;
                    default:
                        throw new InvalidDataException($"Unsupported binary VDF type 0x{type:X2}.");
                }
            }
        }

        public static string ReadString(Stream stream)
        {
            using var buffer = new MemoryStream();
            while (true)
            {
                var value = stream.ReadByte();
                if (value <= 0)
                {
                    return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
                }

                buffer.WriteByte((byte)value);
            }
        }

        private static string ReadKey(Stream stream, IReadOnlyList<string>? stringTable)
        {
            if (stringTable == null)
            {
                return ReadString(stream);
            }

            Span<byte> bytes = stackalloc byte[4];
            stream.ReadExactly(bytes);
            return stringTable[BitConverter.ToInt32(bytes)];
        }

        private static int ReadInt32(Stream stream)
        {
            Span<byte> bytes = stackalloc byte[4];
            stream.ReadExactly(bytes);
            return BitConverter.ToInt32(bytes);
        }
    }

    private static class LicenseCacheCipher
    {
        private const int TableSize = 32;
        private const int Multiplier = 16807;
        private const int Modulus = 2147483647;
        private const int Quotient = 127773;
        private const int Remainder = 2836;

        public static byte[] Decrypt(int key, byte[] data)
        {
            var generator = new RandomStream(key);
            var output = new byte[data.Length];
            for (var index = 0; index < data.Length; index++)
            {
                output[index] = (byte)(data[index] ^ generator.Next(32, 126));
            }

            return output;
        }

        private sealed class RandomStream
        {
            private int _seed;
            private int _current;
            private readonly int[] _table = new int[TableSize];

            public RandomStream(int key)
            {
                _seed = key >= 0 ? -key : key;
            }

            public int Next(int low, int high)
            {
                var span = high - low + 1;
                if (span <= 1)
                {
                    return low;
                }

                var limit = int.MaxValue - (int)(((uint)int.MaxValue + 1) % (uint)span);
                int sample;
                do
                {
                    sample = Next();
                }
                while (sample > limit);

                return low + (sample % span);
            }

            private int Next()
            {
                if (_seed <= 0 || _current == 0)
                {
                    _seed = -_seed < 1 ? 1 : -_seed;
                    for (var index = TableSize + 7; index >= 0; index--)
                    {
                        var quotient = _seed / Quotient;
                        _seed = (Multiplier * (_seed - (quotient * Quotient))) - (Remainder * quotient);
                        if (_seed < 0)
                        {
                            _seed += Modulus;
                        }

                        if (index < TableSize)
                        {
                            _table[index] = _seed;
                        }
                    }

                    _current = _table[0];
                }

                var nextQuotient = _seed / Quotient;
                _seed = (Multiplier * (_seed - (nextQuotient * Quotient))) - (Remainder * nextQuotient);
                if (_seed < 0)
                {
                    _seed += Modulus;
                }

                var slot = _current / (1 + ((Modulus - 1) / TableSize));
                if (slot is < 0 or >= TableSize)
                {
                    slot = slot % TableSize & int.MaxValue;
                }

                _current = _table[slot];
                _table[slot] = _seed;
                return _current;
            }
        }
    }
}
