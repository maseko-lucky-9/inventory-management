using System.Text.RegularExpressions;

namespace Inventory.UnitTests;

/// <summary>Reads the API's C# sources for the source-scan tests.</summary>
public static partial class SourceFiles
{
    public sealed record SourceFile(string Path, string Text);

    public static IReadOnlyList<SourceFile> Api()
    {
        string root = RepoRoot();
        string api = System.IO.Path.Combine(root, "src", "Inventory.Api");
        return Directory.EnumerateFiles(api, "*.cs", SearchOption.AllDirectories)
            .Select(file => System.IO.Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(path => !path.Contains("/bin/", StringComparison.Ordinal) && !path.Contains("/obj/", StringComparison.Ordinal))
            .Select(path => new SourceFile(path, File.ReadAllText(System.IO.Path.Combine(root, path))))
            .ToList();
    }

    public static bool IsStore(string path) => StorePath().IsMatch(path);

    // ADR-008: SQL lives only in feature stores and the database plumbing.
    public static bool MayContainSql(string path) =>
        IsStore(path) || path.StartsWith("src/Inventory.Api/Shared/Persistence/", StringComparison.Ordinal);

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Inventory.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Inventory.slnx not found above the test output.");
    }

    [GeneratedRegex(@"^src/Inventory\.Api/Features/[^/]+/[^/]+Store\.cs$")]
    private static partial Regex StorePath();
}
