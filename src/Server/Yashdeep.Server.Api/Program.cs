var builder = WebApplication.CreateBuilder(args);

// Foundation Composition Root: Add framework services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseHttpsRedirection();

// Minimal health check endpoint for executable host validation
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "Yashdeep.Server.Api",
    TimestampUtc = DateTime.UtcNow,
    Version = "1.0.0"
}))
.WithName("HealthCheck");

app.MapControllers();

app.Run();

// Partial Program class declaration for WebApplicationFactory / Integration Testing
public partial class Program { }
