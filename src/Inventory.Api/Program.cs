using Inventory.Api.Features.Products;
using Inventory.Api.Features.Stock;
using Inventory.Api.Features.Warehouses;
using Inventory.Api.Shared.Errors;
using Inventory.Api.Shared.Persistence;
using Npgsql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Catch captive dependencies and missing registrations at startup, in every environment.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// JSON logs with scopes carry the request's trace id; bodies and SQL parameters are never logged.
builder.Logging.ClearProviders().AddJsonConsole(options => options.IncludeScopes = true);

// Read when first resolved, so test settings apply. Pool capped at 50 (capacity baseline in CLAUDE.md).
builder.Services.AddSingleton(services => NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(
    services.GetRequiredService<IConfiguration>().GetConnectionString("Inventory"))
{
    MaxPoolSize = 50,
    Timeout = 5,
    Options = "-c statement_timeout=5000",
}.ConnectionString));
builder.Services.AddHostedService<SchemaInitializer>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions.TryAdd("code", ErrorCodes.ForStatus(context.ProblemDetails.Status)));
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddScoped<ProductStore>();
builder.Services.AddScoped<WarehouseStore>();
builder.Services.AddScoped<StockStore>();

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapHealthChecks("/health");
app.MapProductsEndpoints();
app.MapWarehousesEndpoints();
app.MapStockEndpoints();

app.Run();
