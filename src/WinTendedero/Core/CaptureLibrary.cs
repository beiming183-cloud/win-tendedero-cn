using System.IO;
using System.Collections.Concurrent;

namespace WinTendedero.Core;

/// <summary>Durable screenshot storage, separate from the temporary rope and its thumbnail cache.</summary>
public static class CaptureLibrary
{
    private static readonly object Gate = new();
    private static readonly ConcurrentDictionary<string, string> Relocated = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> InternalWrites = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsInternalWrite(string path) => InternalWrites.ContainsKey(path);
    public static string Resolve(string path)
    {
        while (Relocated.TryGetValue(path, out var next) && !string.Equals(path, next, StringComparison.OrdinalIgnoreCase)) path = next;
        return path;
    }

    public static string Store(string path)
    {
        lock (Gate)
        {
            path = Resolve(path);
            if (Inbox.Contains(path)) return path;
            var target = Copy(path, Inbox.Folder);
            Relocated[path] = target;
            return target;
        }
    }

    private static string Copy(string source, string folder)
    {
        Directory.CreateDirectory(folder);
        var target = Inbox.UniquePath(folder, Path.GetFileName(source));
        InternalWrites[target] = 0;
        try
        {
            // Publish a complete file: Explorer and file watchers never see a half-written image.
            var temporary = target + "." + Guid.NewGuid().ToString("N") + ".capturing";
            try { File.Copy(source, temporary); File.Move(temporary, target); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return target;
        }
        catch { InternalWrites.TryRemove(target, out _); throw; }
    }

    public static void ChangeFolder(string folder)
    {
        lock (Gate)
        {
            folder = Path.GetFullPath(folder);
            var old = Inbox.Folder;
            if (string.Equals(Path.GetFullPath(old).TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
            Directory.CreateDirectory(folder);
            // Preserve originals and duplicate names. Only future operations follow the copies.
            var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(old).Where(Thumbnails.IsImage)) changes[file] = Copy(file, folder);
            foreach (var pair in changes) Relocated[pair.Key] = pair.Value;
            Settings.Current.InboxFolder = folder;
        }
    }
}
