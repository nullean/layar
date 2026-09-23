namespace Laya.Tests;

/// <summary>
/// Locates the local <c>.artifacts/models/&lt;name&gt;</c> checkpoint directories that
/// <c>tools/layar-export</c> produces. These are gitignored (650MB-1.2GB each), so tests that need
/// them skip gracefully when they're absent — see <see cref="RequireOrSkip"/> — rather than failing
/// CI until checkpoint hosting (README's "Checkpoint distribution" follow-up) exists. Run
/// <c>tools/layar-export/export.py</c> locally to populate them and turn these tests on.
/// </summary>
internal static class CheckpointFixtures
{
	private static readonly Lazy<string?> RepoRootLazy = new(FindRepoRoot);

	public static string? RepoRoot => RepoRootLazy.Value;

	private static string? FindRepoRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			if (dir.GetFiles("layar.slnx").Length > 0)
				return dir.FullName;
			dir = dir.Parent;
		}
		return null;
	}

	public static string? ModelDirectory(string checkpointName) =>
		RepoRoot is null ? null : Path.Combine(RepoRoot, ".artifacts", "models", checkpointName);

	/// <summary>Returns the checkpoint directory, or throws <see cref="TUnit.Core.Exceptions.SkipTestException"/>
	/// when it hasn't been exported locally.</summary>
	public static string RequireOrSkip(string checkpointName)
	{
		var dir = ModelDirectory(checkpointName);
		if (dir is null || !Directory.Exists(Path.Combine(dir, "tokenizer")))
		{
			throw new TUnit.Core.Exceptions.SkipTestException(
				$"'{checkpointName}' checkpoint not exported locally — run tools/layar-export/export.py " +
				$"and populate .artifacts/models/{checkpointName} to run this test.");
		}
		return dir;
	}
}
