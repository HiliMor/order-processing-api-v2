using OrderProcessing.Api.Contracts;
using OrderProcessing.Api.DependencyInjection;
using OrderProcessing.Api.Services;
using OrderProcessing.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddOptions<OrderProcessingOptions>()
    .Bind(builder.Configuration.GetSection(OrderProcessingOptions.SectionName))
    .Validate(o => o.MinDelayMs >= 0, "MinDelayMs must be >= 0.")
    .Validate(o => o.MaxDelayMs > o.MinDelayMs, "MaxDelayMs must be greater than MinDelayMs.")
    .ValidateOnStart();

builder.Services.AddOrderProcessingServices();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = "Unexpected server error." });
    });
});

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/api/orders/process", async (
    ProcessOrderRequest request,
    IOrderProcessor orderProcessor,
    CancellationToken cancellationToken) =>
{
    if (!OrderValidator.IsValidOrderId(request.OrderId))
        return Results.BadRequest(new { error = $"orderId is required and must not exceed {OrderValidator.MaxOrderIdLength} characters." });

    var result = await orderProcessor.ProcessAsync(request, cancellationToken);
    return Results.Ok(result);
});

app.MapGet("/api/orders/stats", (IStatisticsCollector statisticsCollector) =>
{
    var snapshot = statisticsCollector.GetSnapshot();
    return Results.Ok(new OrderStatsResponse(
        snapshot.TotalOrdersProcessed,
        snapshot.AverageProcessingDurationMs,
        snapshot.LastFiveProcessingDurationsMs));
});

app.Run();

public partial class Program;
