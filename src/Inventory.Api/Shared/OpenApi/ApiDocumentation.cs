namespace Inventory.Api.Shared.OpenApi;

/// <summary>
/// The built-in OpenAPI document and a Swagger UI over it. Both are mapped only when OpenApi:Enabled is true:
/// on by default in Development (appsettings.Development.json), off everywhere else unless turned on.
/// </summary>
public static class ApiDocumentation
{
    private const string EnabledSetting = "OpenApi:Enabled";
    private const string DocumentPath = "/openapi/v1.json";

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecurityTransformer>();
            options.AddOperationTransformer<BearerSecurityTransformer>();
        });

    // Anonymous, so a caller can read the API and log in from the UI; the API routes themselves still need a token.
    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>(EnabledSetting))
        {
            app.MapOpenApi().AllowAnonymous();
            // Swashbuckle's route prefix takes no leading slash; its route is "swagger/{**path}".
            app.MapSwaggerUI("swagger", options => options.SwaggerEndpoint(DocumentPath, "Inventory API")).AllowAnonymous();
        }
        return app;
    }
}
