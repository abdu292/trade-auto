using Brain.Application;
using Brain.Infrastructure.Data;
using Brain.Infrastructure.DependencyInjection;
using Brain.Web.Endpoints;
using Brain.Web.Filters;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
	config.ReadFrom.Configuration(context.Configuration));

try
{
	Log.Information("🚀 Building application...");


builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<TradeApiSecurityOptions>(builder.Configuration.GetSection("Security"));

builder.Services
	.AddApplication()
	.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.MapHealthChecks("/health");

app.MapGroup("/api")
	.MapTradesEndpoints()
	.MapStrategyEndpoints()
	.MapRiskEndpoints()
	.MapSessionEndpoints()
	.MapSignalsEndpoints()
	.MapTradingViewEndpoints()
	.MapMonitoringEndpoints();

app.MapMt5Endpoints();
app.MapReplayEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
	var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
	var availableMigrations = dbContext.Database.GetMigrations().ToList();

	if (availableMigrations.Count > 0)
	{
		var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();
		if (pendingMigrations.Count > 0)
		{
			Log.Information("Applying {MigrationCount} pending database migration(s).", pendingMigrations.Count);
		}

		await dbContext.Database.MigrateAsync();
	}
	else
	{
		Log.Warning("No EF Core migrations were found. Falling back to EnsureCreated for compatibility.");
		await dbContext.Database.EnsureCreatedAsync();
	}
}

Log.Information("🚀 Starting application... Listening on: http://localhost:5000");
app.Run();
}
catch (Exception ex)
{
	Log.Fatal(ex, "💥 Application terminated unexpectedly");
}
finally
{
	Log.CloseAndFlush();
}

public partial class Program;
