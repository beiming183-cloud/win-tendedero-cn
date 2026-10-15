using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace WinTendedero.Core;

public readonly record struct Thumbnail(BitmapSource Image, int PixelWidth, int PixelHeight);

/// <summary>Decodes screenshots down to the size the line needs, patiently if the file is still being written.</summary>
public static class Thumbnails
{
    public static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic", ".avif",
    };

    public static bool IsImage(string path) => ImageExtensions.Contains(Path.GetExtension(path));

    /// <summary>A thumbnail no larger than `maxPixels` on its longest side. Null if the file cannot be read.</summary>
    public static Thumbnail? Load(string path, int maxPixels = 480, int attempts = 8)
    {
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length == 0) throw new IOException("empty");
                if (maxPixels <= 1024 && Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp")
                    return DecodeDetached(stream, maxPixels);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                int w = frame.PixelWidth, h = frame.PixelHeight;
                if (w <= 0 || h <= 0) throw new IOException("no pixels");

                stream.Position = 0;
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.StreamSource = stream;
                if (Math.Max(w, h) > maxPixels)
                {
                    if (w >= h) image.DecodePixelWidth = maxPixels; else image.DecodePixelHeight = maxPixels;
                }
                image.EndInit();
                image.Freeze();
                return new Thumbnail(image, w, h);
            }
            catch (Exception e) when (i < attempts - 1 && (e is IOException or UnauthorizedAccessException || (i < 2 && e is NotSupportedException or FileFormatException)))
            {
                // The capture is still being written. Give it a moment. A format
                // Windows cannot decode at all gets two tries, not eight.
                Thread.Sleep(80 + i * 40);
            }
            catch (Exception e)
            {
                Log.Error($"Could not decode {path}: {e.Message}");
                return null;
            }
        }
        return null;
    }

    // Explicitly dispose the full-size codec buffers. A WPF downscaled image can keep
    // its source decoder alive even though the rope only displays a small thumbnail.
    private static Thumbnail DecodeDetached(Stream stream, int maxPixels)
    {
        using var source = System.Drawing.Image.FromStream(stream, false, false);
        int w = source.Width, h = source.Height;
        double scale = Math.Min(1, maxPixels / (double)Math.Max(w, h));
        int tw = Math.Max(1, (int)Math.Round(w * scale)), th = Math.Max(1, (int)Math.Round(h * scale));
        using var resized = new System.Drawing.Bitmap(tw, th, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(resized))
        {
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, new System.Drawing.Rectangle(0, 0, tw, th));
        }
        var data = resized.LockBits(new System.Drawing.Rectangle(0, 0, tw, th), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            int stride = tw * 4;
            var pixels = new byte[stride * th];
            for (int row = 0; row < th; row++) Marshal.Copy(data.Scan0 + row * data.Stride, pixels, row * stride, stride);
            var image = BitmapSource.Create(tw, th, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
            image.Freeze();
            return new Thumbnail(image, w, h);
        }
        finally { resized.UnlockBits(data); }
    }

    public static void SavePng(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    public static byte[]? PngBytes(string path)
    {
        try
        {
            if (string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                return File.ReadAllBytes(path);
            var loaded = Load(path, int.MaxValue, 3);
            if (loaded is null) return null;
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(loaded.Value.Image));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch (Exception e)
        {
            Log.Error($"Could not read {path}: {e.Message}");
            return null;
        }
    }
}
