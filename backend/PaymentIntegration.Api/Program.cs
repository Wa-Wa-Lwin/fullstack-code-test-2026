using Microsoft.EntityFrameworkCore;
using PaymentIntegration.Api;
using PaymentIntegration.Api.Models;
using PaymentIntegration.Application.Services;
using PaymentIntegration.Infrastructure;
using PaymentIntegration.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<PaymentResultService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<PaymentNotificationService>();
builder.Services.AddScoped<TransactionService>();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.Configure<FrontendOptions>(builder.Configuration.GetSection(FrontendOptions.SectionName));
var frontend = builder.Configuration.GetSection(FrontendOptions.SectionName).Get<FrontendOptions>() ?? new FrontendOptions();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(frontend.Origin)
    .AllowAnyHeader()
    .AllowAnyMethod()
    // The frontend reads Retry-After from a 409 to know when to retry a checkout.
    .WithExposedHeaders("Retry-After")));

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Payment Integration API",
        Version = "v1",
        Description = "Checkout with 2C2P (PGW v4.3, sandbox): hosted payment page for new payment methods, "
            + "Do Payment + Payment Inquiry for saved cards. POST /api/checkout requires an Idempotency-Key header.",
    });

    options.SchemaFilter<ProblemDetailsExamplesFilter>();

    // Summaries, response descriptions and <example> values from the XML comments.
    foreach (var file in new[] { "PaymentIntegration.Api.xml", "PaymentIntegration.Application.xml" })
    {
        var path = Path.Combine(AppContext.BaseDirectory, file);
        if (File.Exists(path))
        {
            options.IncludeXmlComments(path);
        }
    }
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // Creates the tables and seed rows on first run, so `docker compose up -d` followed by
    // `dotnet run` is enough. In production, migrations run as a separate deployment step
    // instead, so several app instances never migrate at the same time.
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
    }

    // Docker Compose runs the API in Development too, so Swagger is available there.
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("v1/swagger.json", "Payment Integration API v1");
        options.DocumentTitle = "Payment Integration API";
    });
}
else
{
    // Not in Development: 2C2P's notification POST to an http URL would be redirected, and
    // the redirect drops the request body.
    app.UseHttpsRedirection();
}

app.UseCors();
app.MapControllers();

app.Run();

/// <summary>Lets the integration tests start the app with WebApplicationFactory.</summary>
public partial class Program;
