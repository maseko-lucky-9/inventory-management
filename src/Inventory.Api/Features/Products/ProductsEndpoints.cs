using Inventory.Api.Shared.Errors;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventory.Api.Features.Products;

public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/products");
        group.MapGet("/", ListAsync);
        group.MapGet("/{code}", GetAsync);
        group.MapPost("/", CreateAsync);
        return app;
    }

    private static async Task<Ok<IReadOnlyList<Product>>> ListAsync(ProductStore store, CancellationToken cancellationToken) =>
        TypedResults.Ok(await store.ListAsync(cancellationToken));

    private static async Task<Ok<Product>> GetAsync(string code, ProductStore store, CancellationToken cancellationToken)
    {
        string trimmed = code.Trim();
        Product product = await store.FindAsync(trimmed, cancellationToken) ?? throw new NotFoundException("product", trimmed);
        return TypedResults.Ok(product);
    }

    private static async Task<Created<Product>> CreateAsync(
        CreateProductRequest request, ProductStore store, CancellationToken cancellationToken)
    {
        Product product = await store.CreateAsync(request.Code.Trim(), request.Description, cancellationToken);
        // Escaped because codes are unvalidated until T09 and could hold URL-reserved characters.
        return TypedResults.Created("/products/" + Uri.EscapeDataString(product.Code), product);
    }
}
