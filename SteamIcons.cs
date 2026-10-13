namespace WinFormsApp1;

internal static class SteamIcons
{
    private static readonly string[] PreferredNames = ["library_600x900.jpg", "header.jpg", "logo.png"];

    public static string? Find(string steamPath, uint appId)
    {
        var folder = Path.Combine(steamPath, "appcache", "librarycache", appId.ToString());
        if (!Directory.Exists(folder))
        {
            return null;
        }

        foreach (var name in PreferredNames)
        {
            var candidate = Path.Combine(folder, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Directory.EnumerateFiles(folder, "*.jpg").FirstOrDefault();
    }

    public static Image LoadSquare(string path, int size)
    {
        using var source = Image.FromFile(path);
        var scale = Math.Max((float)size / source.Width, (float)size / source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var scaled = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(scaled))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source, new Rectangle(0, 0, width, height));
        }

        var icon = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(icon))
        {
            graphics.DrawImage(scaled, new Rectangle((size - width) / 2, (size - height) / 2, width, height));
        }

        return icon;
    }
}
