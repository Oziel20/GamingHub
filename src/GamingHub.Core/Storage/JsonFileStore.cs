using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace GamingHub.Core.Storage;

/// <summary>
/// One JSON document per file. Serialized access, atomic writes (tmp + move), .bak backup,
/// quarantine of corrupt files and recovery from .tmp/.bak.
/// </summary>
public sealed class JsonFileStore<T> where T : class, new()
{
    private const long MaxBytes = 50L * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private T? _cache;

    public JsonFileStore(string path, ILogger log) { _path = path; _log = log; }

    /// <summary>Reads under the lock. Do not leak the document: project to copies/DTOs.</summary>
    public async Task<TResult> ReadAsync<TResult>(Func<T, TResult> read, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { return read(await LoadAsync(ct).ConfigureAwait(false)); }
        finally { _gate.Release(); }
    }

    /// <summary>The callback must not mutate the document when it returns Commit=false.</summary>
    public async Task<TResult> UpdateAsync<TResult>(Func<T, (bool Commit, TResult Result)> update, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var doc = await LoadAsync(ct).ConfigureAwait(false);
            (bool Commit, TResult Result) outcome;
            try { outcome = update(doc); }
            catch { _cache = null; throw; }
            if (outcome.Commit)
            {
                try { await SaveAsync(doc, ct).ConfigureAwait(false); }
                catch { _cache = null; throw; }
            }
            return outcome.Result;
        }
        finally { _gate.Release(); }
    }

    private async Task<T> LoadAsync(CancellationToken ct)
    {
        if (_cache is not null) return _cache;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var doc = await TryReadAsync(_path, ct).ConfigureAwait(false);
        if (doc is null)
        {
            var recovered = await TryReadAsync(_path + ".tmp", ct).ConfigureAwait(false)
                            ?? await TryReadAsync(_path + ".bak", ct).ConfigureAwait(false);
            if (File.Exists(_path)) Quarantine();
            if (recovered is not null) _log.LogWarning("Recovered {File} from tmp/backup", Path.GetFileName(_path));
            doc = recovered ?? new T();
            await SaveAsync(doc, ct).ConfigureAwait(false);
        }
        _cache = doc;
        return doc;
    }

    private async Task<T?> TryReadAsync(string file, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(file)) return null;
            var len = new FileInfo(file).Length;
            if (len == 0 || len > MaxBytes) return null;
            var text = await File.ReadAllTextAsync(file, Encoding.UTF8, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text)) return null;
            return JsonSerializer.Deserialize<T>(text, Options);
        }
        catch (JsonException) { _log.LogWarning("Invalid JSON in {File}", Path.GetFileName(file)); return null; }
        catch (IOException ex) { _log.LogError("I/O error reading {File}: {Type}", Path.GetFileName(file), ex.GetType().Name); return null; }
        catch (UnauthorizedAccessException) { _log.LogError("Access denied reading {File}", Path.GetFileName(file)); return null; }
    }

    private void Quarantine()
    {
        try
        {
            var target = $"{_path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            File.Move(_path, target, true);
            _log.LogWarning("Quarantined corrupt file {File}", Path.GetFileName(_path));
        }
        catch (IOException) { _log.LogError("Could not quarantine {File}", Path.GetFileName(_path)); }
    }

    private async Task SaveAsync(T doc, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(doc, Options);
        var tmp = _path + ".tmp";
        var bytes = new UTF8Encoding(false).GetBytes(json);
        await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await fs.WriteAsync(bytes, ct).ConfigureAwait(false);
            await fs.FlushAsync(ct).ConfigureAwait(false);
        }
        if (File.Exists(_path)) File.Copy(_path, _path + ".bak", true);
        File.Move(tmp, _path, true);
    }
}
