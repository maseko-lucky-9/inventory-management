using Inventory.Api.Shared.Errors;
using Inventory.Api.Shared.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventory.Api.Features.Products;

public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/products");
        group.MapGet("/", ListAsync);
        group.MapGet("/{code}", GetAsync);
        group.MapPost("/", CreateAsync).AddEndpointFilter<ValidationFilter<CreateProductRequest>>();
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

    // The validator has run, so both fields are present and the code holds only URL-safe characters.
    private static async Task<Created<Product>> CreateAsync(
        CreateProductRequest request, ProductStore store, CancellationToken cancellationToken)
    {
        Product product = await store.CreateAsync(request.Code!.Trim(), request.Description!, cancellationToken);
        return TypedResults.Created("/products/" + product.Code, product);
    }
}
