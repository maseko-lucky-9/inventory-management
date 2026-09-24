using System.Text.RegularExpressions;

namespace Inventory.UnitTests;

public sealed class SqlHygieneTests
{
    // Multi-word, uppercase SQL shapes; single words like "Select" in C# never match.
    private static readonly Regex[] SqlShapes =
    [
        new(@"\bSELECT\b[\s\S]{0,300}?\bFROM\b"),
        new(@"\bINSERT\s+INTO\b"),
        new(@"\bUPDATE\s+\w+\s+SET\b"),
        new(@"\bDELETE\s+FROM\b"),
        new(@"\b(?:CREATE|ALTER|DROP)\s+(?:TABLE|INDEX|VIEW|FUNCTION)\b"),
        new(@"\bSET\s+LOCAL\b"),
        new(@"\bpg_advisory"),
    ];

    // Ways to build SQL from pieces (H3: never interpolated, formatted or concatenated).
    private static readonly Regex[] PieceBuilders =
    [
        new(@"\$@?"""),
        new(@"\b[Ss]tring\.(?:Format|Concat)\b"),
        new(@"\bStringBuilder\b"),
        new(@"\+="),
        new(@"\.Replace\("),
        new(@"""\s*\+"),
        new(@"\+\s*"""),
    ];

    [Fact]
    public void SqlAppearsOnlyInStoresAndPersistence()
    {
        string[] offenders = SourceFiles.Api()
            .Where(file => !SourceFiles.MayContainSql(file.Path))
            .Where(file => SqlShapes.Any(shape => shape.IsMatch(file.Text)))
            .Select(file => file.Path)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void StoresAndPersistenceNeverBuildSqlFromPieces()
    {
        string[] offenders = SourceFiles.Api()
            .Where(file => SourceFiles.MayContainSql(file.Path))
            .SelectMany(file => PieceBuilders
                .Where(builder => builder.IsMatch(file.Text))
                .Select(builder => file.Path + " matches " + builder))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void StoreCommandsPassOnlySqlConstants()
    {
        Regex command = new(@"new\s+CommandDefinition\(\s*([^,\s)]+)");
        Regex literalCall = new(@"(?:Execute|Query)\w*Async(?:<[^>]*>)?\(\s*""");

        string[] offenders = SourceFiles.Api()
            .Where(file => SourceFiles.IsStore(file.Path))
            .SelectMany(file => command.Matches(file.Text)
                .Where(match => !match.Groups[1].Value.EndsWith("Sql", StringComparison.Ordinal))
                .Select(match => file.Path + ": " + match.Value)
                .Concat(literalCall.Matches(file.Text).Select(match => file.Path + ": " + match.Value)))
            .ToArray();

        Assert.Empty(offenders);
    }
}
