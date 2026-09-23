using System.Net;
using AwesomeAssertions;
using Laya.Core;

namespace Laya.Tests;

/// <summary>Routes requests to in-memory content by URL, or 404s — no real network needed to test
/// <see cref="CheckpointCache"/>.</summary>
internal sealed class StubHttpMessageHandler(IReadOnlyDictionary<string, string> contentByUrl) : HttpMessageHandler
{
	private int _requestCount;

	public int RequestCount => _requestCount;

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		Interlocked.Increment(ref _requestCount);
		var url = request.RequestUri!.ToString();
		if (contentByUrl.TryGetValue(url, out var content))
		{
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(content),
			});
		}
		return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
	}
}

public class CheckpointCacheTests
{
	private static string TempDir() => Directory.CreateTempSubdirectory("layar-checkpoint-cache-").FullName;

	// Real GitHubReleaseCheckpointUrls.Create, just pointed at a fake owner/repo/tag so the URLs
	// stay stable test fixtures without depending on an actual GitHub release existing.
	private static CheckpointAssetUrl FlatGitHubReleaseStyle() =>
		GitHubReleaseCheckpointUrls.Create("example", "example-repo", "v0.0.0-test");

	[Test]
	public async Task EnsureFileAsync_downloads_and_writes_to_the_expected_local_path()
	{
		var cacheRoot = TempDir();
		var handler = new StubHttpMessageHandler(new Dictionary<string, string>
		{
			["https://github.com/example/example-repo/releases/download/v0.0.0-test/multilingual-model.onnx"] = "fake-onnx-bytes",
		});
		using var cache = new CheckpointCache(cacheRoot, FlatGitHubReleaseStyle(), new HttpClient(handler));

		var path = await cache.EnsureFileAsync("multilingual", "model.onnx");

		path.Should().Be(Path.Combine(cacheRoot, "multilingual", "model.onnx"));
		File.Exists(path).Should().BeTrue();
		(await File.ReadAllTextAsync(path)).Should().Be("fake-onnx-bytes");
	}

	[Test]
	public async Task EnsureFileAsync_does_not_re_download_a_cached_file()
	{
		var cacheRoot = TempDir();
		var handler = new StubHttpMessageHandler(new Dictionary<string, string>
		{
			["https://github.com/example/example-repo/releases/download/v0.0.0-test/multilingual-model.onnx"] = "fake-onnx-bytes",
		});
		using var cache = new CheckpointCache(cacheRoot, FlatGitHubReleaseStyle(), new HttpClient(handler));

		_ = await cache.EnsureFileAsync("multilingual", "model.onnx");
		_ = await cache.EnsureFileAsync("multilingual", "model.onnx");

		handler.RequestCount.Should().Be(1);
	}

	[Test]
	public async Task EnsureFileAsync_leaves_no_temp_file_behind_on_success()
	{
		var cacheRoot = TempDir();
		var handler = new StubHttpMessageHandler(new Dictionary<string, string>
		{
			["https://github.com/example/example-repo/releases/download/v0.0.0-test/multilingual-model.onnx"] = "fake-onnx-bytes",
		});
		using var cache = new CheckpointCache(cacheRoot, FlatGitHubReleaseStyle(), new HttpClient(handler));

		var path = await cache.EnsureFileAsync("multilingual", "model.onnx");

		File.Exists(path + ".download").Should().BeFalse();
	}

	[Test]
	public async Task TryEnsureFileAsync_returns_null_on_404_instead_of_throwing()
	{
		var cacheRoot = TempDir();
		using var cache = new CheckpointCache(cacheRoot, FlatGitHubReleaseStyle(), new HttpClient(new StubHttpMessageHandler(
			new Dictionary<string, string>())));

		var result = await cache.TryEnsureFileAsync("multilingual", "model.onnx.data");

		result.Should().BeNull();
	}

	[Test]
	public async Task EnsureFileAsync_throws_on_404_for_a_required_file()
	{
		var cacheRoot = TempDir();
		using var cache = new CheckpointCache(cacheRoot, FlatGitHubReleaseStyle(), new HttpClient(new StubHttpMessageHandler(
			new Dictionary<string, string>())));

		var act = () => cache.EnsureFileAsync("multilingual", "model.onnx");
		await act.Should().ThrowAsync<HttpRequestException>();
	}

	[Test]
	public async Task EnsureTokenizerAsync_fetches_both_tokenizer_files_and_returns_the_tokenizer_directory()
	{
		var cacheRoot = TempDir();
		var handler = new StubHttpMessageHandler(new Dictionary<string, string>
		{
			["https://github.com/example/example-repo/releases/download/v0.0.0-test/multilingual-tokenizer-tokenizer.json"] = "{}",
			["https://github.com/example/example-repo/releases/download/v0.0.0-test/multilingual-tokenizer-tokenizer_config.json"] = "{}",
		});
		using var cache = new CheckpointCache(cacheRoot, FlatGitHubReleaseStyle(), new HttpClient(handler));

		var tokenizerDir = await cache.EnsureTokenizerAsync("multilingual");

		tokenizerDir.Should().Be(Path.Combine(cacheRoot, "multilingual", "tokenizer"));
		File.Exists(Path.Combine(tokenizerDir, "tokenizer.json")).Should().BeTrue();
		File.Exists(Path.Combine(tokenizerDir, "tokenizer_config.json")).Should().BeTrue();
	}

	[Test]
	public async Task EnsureOnnxModelAsync_fetches_the_optional_external_data_file_without_failing_when_absent()
	{
		var cacheRoot = TempDir();
		// Only model.onnx is served -- no model.onnx.data -- matching a small export with no
		// external-data companion.
		var handler = new StubHttpMessageHandler(new Dictionary<string, string>
		{
			["https://github.com/example/example-repo/releases/download/v0.0.0-test/multilingual-model.onnx"] = "fake-onnx-bytes",
		});
		using var cache = new CheckpointCache(cacheRoot, FlatGitHubReleaseStyle(), new HttpClient(handler));

		var modelPath = await cache.EnsureOnnxModelAsync("multilingual");

		modelPath.Should().Be(Path.Combine(cacheRoot, "multilingual", "model.onnx"));
		File.Exists(Path.Combine(cacheRoot, "multilingual", "model.onnx.data")).Should().BeFalse();
	}
}

public class GitHubReleaseCheckpointUrlsTests
{
	[Test]
	public async Task Create_flattens_a_nested_relative_path_into_a_single_asset_name()
	{
		var urlOf = GitHubReleaseCheckpointUrls.Create("nullean", "layar", "0.1.0");
		urlOf("multilingual", "tokenizer/tokenizer.json").Should().Be(new Uri(
			"https://github.com/nullean/layar/releases/download/0.1.0/multilingual-tokenizer-tokenizer.json"));
	}

	[Test]
	public async Task Create_handles_a_top_level_file_without_a_slash()
	{
		var urlOf = GitHubReleaseCheckpointUrls.Create("nullean", "layar", "0.1.0");
		urlOf("english", "model.onnx").Should().Be(new Uri(
			"https://github.com/nullean/layar/releases/download/0.1.0/english-model.onnx"));
	}
}
