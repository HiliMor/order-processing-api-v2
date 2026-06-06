using OrderProcessing.Api.DependencyInjection;
using OrderProcessing.Api.Endpoints;
using OrderProcessing.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddOrderProcessingOptions(builder.Configuration);
builder.Services.AddOrderProcessingServices();
builder.Services.AddOrderProcessingRateLimiter(builder.Configuration);
builder.Services.AddHealthChecks();
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

app.Use(async (context, next) =>
{
    var requestContext = context.RequestServices.GetRequiredService<IRequestContext>();
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-Correlation-ID"] = requestContext.CorrelationId.ToString();
        return Task.CompletedTask;
    });
    await next();
});

app.UseHttpsRedirection();
app.UseRateLimiter();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapOrderProcessingEndpoints();

app.Run();

public partial class Program;
