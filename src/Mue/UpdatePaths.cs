namespace Mue;

internal static class UpdatePaths
{
    public static bool IsWithin(string path, string directory) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).Equals(
            Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
        || Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
