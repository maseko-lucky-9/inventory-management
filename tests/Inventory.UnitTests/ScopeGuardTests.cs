using System.Reflection;
using System.Text.RegularExpressions;
using Inventory.Api.Features.Warehouses;

namespace Inventory.UnitTests;

/// <summary>
/// ADR-008 scope guard, by reflection over the API's *Sql fields: each is a constant, and each one that reads warehouses,
/// stock or transfer_orders carries the caller's warehouse link, written in full. The behaviour is proven by ScopingTests;
/// this guard catches a new or edited query that forgets the join.
/// </summary>
public sealed class ScopeGuardTests
{
    private const BindingFlags DeclaredFields =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    // The stores whose queries are scoped (ADR-006). ProductStore and UserStore are global.
    private static readonly HashSet<string> ScopedStores = ["WarehouseStore", "StockStore", "TransferStore"];

    // Scoped reads that deliberately carry no link join. Keep it minimal: each entry says why, and a stale entry fails.
    // Never flagged, so never listed: products and users queries (global); INSERT INTO and UPDATE targets, which are writes
    // (the transfer writes by ids ResolveSql already scoped, and a warehouse create links its creator in the same statement);
    // and the transfer destination lookup, which is unscoped by design (G36) and sits in ResolveSql beside the scoped source.
    private static readonly Dictionary<string, string> AllowList = new(StringComparer.Ordinal)
    {
        ["TransferStore.AvailableSql"] = "Reads one stock row by the source id that ResolveSql resolved through the caller's link, "
            + "in the same transaction, only to report what is left after the guarded decrement refused.",
    };

    // A read of a scoped table: the table follows FROM or JOIN.
    private static readonly Regex ReadsScopedTable = new(@"\b(?:FROM|JOIN)\s+(?:warehouses|stock|transfer_orders)\b", RegexOptions.IgnoreCase);

    // JOIN or INNER JOIN on the links.
    private static readonly Regex LinkJoin = new(@"\bJOIN\s+user_warehouses\b", RegexOptions.IgnoreCase);

    // An outer join keeps unlinked rows, so it filters nothing.
    private static readonly Regex OuterLinkJoin = new(@"\b(?:LEFT|RIGHT|FULL|CROSS)\s+(?:OUTER\s+)?JOIN\s+user_warehouses\b", RegexOptions.IgnoreCase);

    // ADR-008's form for a query that matches either end of a transfer. NOT EXISTS would invert the scope.
    private static readonly Regex LinkExists = new(@"(?<!\bNOT\s+)\bEXISTS\s*\(\s*SELECT\b[^()]*\bFROM\s+user_warehouses\b", RegexOptions.IgnoreCase);

    // The link must be the caller's, not any user's.
    private static readonly Regex BoundToCaller = new(@"\buser_id\s*=\s*@UserId\b");

    [Fact]
    public void EverySqlFieldIsAConstant()
    {
        FieldInfo[] fields = [.. SqlFields()];
        string[] stores = [.. ApiTypes().Where(type => type.IsClass && type.Name.EndsWith("Store", StringComparison.Ordinal)).Select(type => type.Name)];

        // Not vacuous: every store is seen declaring SQL, so a binding-flag slip cannot pass by finding nothing.
        Assert.NotEmpty(stores);
        Assert.All(stores, store => Assert.Contains(fields, field => field.DeclaringType?.Name == store));
        Assert.Empty(fields.Where(field => !field.IsLiteral).Select(field => field.DeclaringType?.Name + "." + field.Name));
    }

    [Fact]
    public void EveryQueryThatReadsWarehousesStockOrTransferOrdersCarriesTheCallersLink()
    {
        SqlConstant[] scopedReads = [.. SqlConstants().Where(sql => ReadsScopedTable.IsMatch(sql.Text))];

        string[] offenders = [.. scopedReads
            .Where(sql => !AllowList.ContainsKey(sql.Name) && !CarriesCallersLink(sql.Text))
            .Select(sql => sql.Name)];

        // Not vacuous: each scoped store is seen with a scoped read, so a pattern slip cannot pass by finding nothing.
        Assert.Superset(ScopedStores, scopedReads.Select(sql => sql.Type).ToHashSet());
        Assert.Empty(offenders);
    }

    // Keeps the allow-list honest: an entry must name a real constant that reads a scoped table and would fail the guard without it.
    [Fact]
    public void EveryAllowListEntryIsAScopedReadThatWouldOtherwiseFailTheGuard()
    {
        Dictionary<string, string> texts = SqlConstants().ToDictionary(sql => sql.Name, sql => sql.Text, StringComparer.Ordinal);

        Assert.All(AllowList.Keys, name =>
        {
            Assert.True(texts.TryGetValue(name, out string? text), name + " is not a *Sql constant.");
            Assert.Matches(ReadsScopedTable, text);
            Assert.False(CarriesCallersLink(text), name + " already carries the caller's link; remove it from the allow-list.");
        });
    }

    // Pins each clause of the rule, so the guard itself is proven able to refuse.
    [Theory]
    [InlineData("SELECT w.code FROM warehouses w JOIN user_warehouses uw ON uw.warehouse_id = w.id AND uw.user_id = @UserId", true)]
    [InlineData("SELECT w.code FROM warehouses w INNER JOIN user_warehouses uw ON uw.warehouse_id = w.id AND uw.user_id = @UserId", true)]
    [InlineData("SELECT t.id FROM transfer_orders t WHERE EXISTS (SELECT 1 FROM user_warehouses uw WHERE uw.warehouse_id = t.source_warehouse_id AND uw.user_id = @UserId)", true)]
    [InlineData("SELECT w.code FROM warehouses w LEFT JOIN user_warehouses uw ON uw.warehouse_id = w.id AND uw.user_id = @UserId", false)]
    [InlineData("SELECT w.code FROM warehouses w LEFT OUTER JOIN user_warehouses uw ON uw.warehouse_id = w.id AND uw.user_id = @UserId", false)]
    [InlineData("SELECT t.id FROM transfer_orders t WHERE NOT EXISTS (SELECT 1 FROM user_warehouses uw WHERE uw.warehouse_id = t.source_warehouse_id AND uw.user_id = @UserId)", false)]
    [InlineData("SELECT w.code FROM warehouses w JOIN user_warehouses uw ON uw.warehouse_id = w.id", false)]
    [InlineData("SELECT w.code FROM warehouses w JOIN user_warehouses uw ON uw.warehouse_id = w.id AND uw.user_id <> @UserId", false)]
    [InlineData("SELECT code FROM warehouses WHERE code = @Code", false)]
    public void TheGuardAcceptsOnlyAnInnerLinkBoundToTheCaller(string sql, bool accepted) =>
        Assert.Equal(accepted, CarriesCallersLink(sql));

    private static bool CarriesCallersLink(string sql) =>
        (LinkJoin.IsMatch(sql) || LinkExists.IsMatch(sql)) && !OuterLinkJoin.IsMatch(sql) && BoundToCaller.IsMatch(sql);

    private static Type[] ApiTypes() => typeof(WarehouseStore).Assembly.GetTypes();

    private static IEnumerable<FieldInfo> SqlFields() =>
        ApiTypes().SelectMany(type => type.GetFields(DeclaredFields)).Where(field => field.Name.EndsWith("Sql", StringComparison.Ordinal));

    // Only constants can be read without an instance; EverySqlFieldIsAConstant fails if any *Sql field is not one.
    private static IEnumerable<SqlConstant> SqlConstants() => SqlFields()
        .Where(field => field.IsLiteral)
        .Select(field => new SqlConstant(field.DeclaringType?.Name ?? "", field.Name, (string?)field.GetRawConstantValue() ?? ""));

    private sealed record SqlConstant(string Type, string Field, string Text)
    {
        public string Name => Type + "." + Field;
    }
}
