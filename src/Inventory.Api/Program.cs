using FluentValidation;
using Inventory.Api.Features.Auth;
using Inventory.Api.Features.Orders;
using Inventory.Api.Features.Products;
using Inventory.Api.Features.Stock;
using Inventory.Api.Features.Warehouses;
using Inventory.Api.Shared.Auth;
using Inventory.Api.Shared.Errors;
using Inventory.Api.Shared.OpenApi;
using Inventory.Api.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
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
// After SchemaInitializer: the demo users must exist before their passwords are set.
builder.Services.AddHostedService<DemoPasswordInitializer>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ErrorCodes.Complete);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddApiDocumentation();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTokenAuthentication();
builder.Services.AddLoginRateLimit(builder.Configuration);
builder.Services.AddSingleton<JsonWebTokenHandler>();
builder.Services.AddSingleton<TokenIssuer>();
builder.Services.AddSingleton<PasswordHasher<User>>();
builder.Services.AddSingleton<PasswordVerifier>();
// Singleton, unlike the scoped stores: it holds only the data source, and the startup password step needs it.
builder.Services.AddSingleton<UserStore>();
builder.Services.AddSingleton<IValidator<LoginRequest>, LoginValidator>();
// Scoped, like the stores that read it: one caller per request (ADR-006).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<ProductStore>();
builder.Services.AddScoped<WarehouseStore>();
builder.Services.AddScoped<StockStore>();
builder.Services.AddSingleton<IValidator<CreateProductRequest>, CreateProductValidator>();
builder.Services.AddSingleton<IValidator<CreateWarehouseRequest>, CreateWarehouseValidator>();
builder.Services.AddSingleton<IValidator<ReceiveStockRequest>, ReceiveStockValidator>();
builder.Services.AddSingleton<IValidator<StockQuery>, StockQueryValidator>();
builder.Services.AddSingleton<IValidator<CreateTransferOrderRequest>, TransferOrderValidator>();
builder.Services.AddScoped<ITransferStore, TransferStore>();
builder.Services.AddScoped<TransferService>();

WebApplication app = builder.Build();

// First, so everything after it (the login rate limit above all) sees the client's address, not a trusted proxy's.
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
// After the status-code pages, so a bare 401 or 429 still gets its Problem Details body.
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapAuthEndpoints();
app.MapProductsEndpoints();
app.MapWarehousesEndpoints();
app.MapStockEndpoints();
app.MapOrdersEndpoints();
// A setting, not the environment, so Compose (Production) can turn it on for local use.
app.MapApiDocumentation();

app.Run();
