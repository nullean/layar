namespace Laya.Core;

/// <summary>
/// A <see cref="CheckpointAssetUrl"/> for checkpoints published as GitHub Release assets. Release
/// assets are flat (no subdirectories), so <c>"tokenizer/tokenizer.json"</c> becomes the asset name
/// <c>"{checkpointName}-tokenizer-tokenizer.json"</c> — matching how <c>tools/layar-export</c>'s
/// output would need to be renamed/uploaded via <c>gh release upload</c> when attaching it to a tag.
/// </summary>
public static class GitHubReleaseCheckpointUrls
{
	public static CheckpointAssetUrl Create(string owner, string repository, string tag)
	{
		ArgumentException.ThrowIfNullOrEmpty(owner);
		ArgumentException.ThrowIfNullOrEmpty(repository);
		ArgumentException.ThrowIfNullOrEmpty(tag);

		return (checkpointName, relativePath) => new Uri(
			$"https://github.com/{owner}/{repository}/releases/download/{tag}/" +
			$"{checkpointName}-{relativePath.Replace('/', '-')}");
	}
}
