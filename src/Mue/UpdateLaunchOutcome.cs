namespace Mue;

public enum UpdateLaunchResult { None, Succeeded, RolledBack }

public sealed record UpdateLaunchOutcome(UpdateLaunchResult Result, string? ExpectedVersion, string CurrentVersion);

public interface IUpdatePendingStore
{
    string? ExpectedVersion { get; set; }
}

public sealed class MemoryUpdatePendingStore : IUpdatePendingStore
{
    public string? ExpectedVersion { get; set; }
}

public sealed class FileUpdatePendingStore(string path) : IUpdatePendingStore
{
    private readonly string _path = Path.GetFullPath(path);
    public string? ExpectedVersion
    {
        get
        {
            try { return File.ReadAllText(_path); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }
        set
        {
            if (value == null) { File.Delete(_path); return; }
            var directory = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Missing store directory");
            Directory.CreateDirectory(directory);
            var temporary = Path.Combine(directory, Path.GetRandomFileName());
            try
            {
                File.WriteAllText(temporary, value);
                File.Move(temporary, _path, overwrite: true);
            }
            finally { File.Delete(temporary); }
        }
    }
}
