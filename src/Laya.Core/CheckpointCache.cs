using System.Net;

namespace Laya.Core;

/// <summary>Maps a checkpoint name and a relative file (e.g. <c>"tokenizer/tokenizer.json"</c>,
/// <c>"model.onnx"</c>) to the URL it's published at. Kept pluggable rather than hardcoded to one
/// hosting scheme (GitHub Release assets, which don't support subdirectories and typically want a
/// flattened name like <c>"multilingual-tokenizer.json"</c>; a CDN or object store that does support
/// real paths; a private mirror; ...).</summary>
public delegate Uri CheckpointAssetUrl(string checkpointName, string relativePath);

/// <summary>
/// Downloads and caches checkpoint files (a tokenizer, an ONNX graph, a TorchScript module) on
/// demand — the download-and-cache half of the Python package's checkpoint loading (there,
/// <c>huggingface_hub.snapshot_download</c>). Where those files are actually hosted is the
/// caller's decision via <see cref="CheckpointAssetUrl"/>; this only handles fetching and caching
/// once a location is known. Downloads are atomic (write to a temp file, then move) so a crash
/// mid-download can't leave a corrupt file mistaken for a complete one.
/// </summary>
public sealed class CheckpointCache : IDisposable
{
	private readonly string _cacheRoot;
	private readonly CheckpointAssetUrl _assetUrl;
	private readonly HttpClient _http;
	private readonly bool _ownsHttpClient;

	public CheckpointCache(string cacheRoot, CheckpointAssetUrl assetUrl, HttpClient? httpClient = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(cacheRoot);
		ArgumentNullException.ThrowIfNull(assetUrl);
		_cacheRoot = cacheRoot;
		_assetUrl = assetUrl;
		_http = httpClient ?? new HttpClient();
		_ownsHttpClient = httpClient is null;
	}

	/// <summary>Downloads <paramref name="relativePath"/> into the local cache if it isn't already
	/// there, returning the local file path either way.</summary>
	public async Task<string> EnsureFileAsync(string checkpointName, string relativePath, CancellationToken cancellationToken = default)
	{
		var destination = Path.Combine(_cacheRoot, checkpointName, relativePath.Replace('/', Path.DirectorySeparatorChar));
		if (File.Exists(destination))
			return destination;

		var directory = Path.GetDirectoryName(destination);
		if (!string.IsNullOrEmpty(directory))
			_ = Directory.CreateDirectory(directory);

		var url = _assetUrl(checkpointName, relativePath);
		var tempPath = destination + ".download";
		try
		{
			using (var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
			{
				_ = response.EnsureSuccessStatusCode();
				await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
				await using var target = File.Create(tempPath);
				await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
			}
			File.Move(tempPath, destination, overwrite: true);
		}
		finally
		{
			if (File.Exists(tempPath))
				File.Delete(tempPath);
		}
		return destination;
	}

	/// <summary>Like <see cref="EnsureFileAsync"/>, but treats a 404 as "this file doesn't exist
	/// for this checkpoint" rather than an error — for optional companions like an ONNX external-data
	/// file, which only some exports produce.</summary>
	public async Task<string?> TryEnsureFileAsync(string checkpointName, string relativePath, CancellationToken cancellationToken = default)
	{
		try
		{
			return await EnsureFileAsync(checkpointName, relativePath, cancellationToken).ConfigureAwait(false);
		}
		catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	/// <summary>Ensures the tokenizer files are cached, returning the tokenizer directory (what
	/// <c>HuggingFaceBpeTokenizer.FromDirectory</c> expects).</summary>
	public async Task<string> EnsureTokenizerAsync(string checkpointName, CancellationToken cancellationToken = default)
	{
		_ = await EnsureFileAsync(checkpointName, "tokenizer/tokenizer.json", cancellationToken).ConfigureAwait(false);
		_ = await EnsureFileAsync(checkpointName, "tokenizer/tokenizer_config.json", cancellationToken).ConfigureAwait(false);
		return Path.Combine(_cacheRoot, checkpointName, "tokenizer");
	}

	/// <summary>Ensures the ONNX graph (and its external-data companion, when the export used one)
	/// is cached, returning the path to <c>model.onnx</c>.</summary>
	public async Task<string> EnsureOnnxModelAsync(string checkpointName, CancellationToken cancellationToken = default)
	{
		var modelPath = await EnsureFileAsync(checkpointName, "model.onnx", cancellationToken).ConfigureAwait(false);
		_ = await TryEnsureFileAsync(checkpointName, "model.onnx.data", cancellationToken).ConfigureAwait(false);
		return modelPath;
	}

	/// <summary>Ensures the TorchScript module is cached, returning the path to <c>model.pt</c>.</summary>
	public Task<string> EnsureTorchScriptModelAsync(string checkpointName, CancellationToken cancellationToken = default) =>
		EnsureFileAsync(checkpointName, "model.pt", cancellationToken);

	public void Dispose()
	{
		if (_ownsHttpClient)
			_http.Dispose();
	}
}
