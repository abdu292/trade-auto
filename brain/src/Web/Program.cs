using Brain.Application;
using Brain.Infrastructure.Data;
using Brain.Infrastructure.DependencyInjection;
using Brain.Web.Endpoints;
using Brain.Web.Filters;
using Microsoft.Data.SqlClient;
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

await using (var scope = app.Services.CreateAsyncScope())
{
	var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
	await dbContext.Database.EnsureCreatedAsync();

	if (app.Environment.IsDevelopment())
	{
		try
		{
			_ = await dbContext.HazardWindows.AsNoTracking().AnyAsync();
			_ = await dbContext.TelegramChannels.AsNoTracking().AnyAsync();
			_ = await dbContext.MacroCacheStates.AsNoTracking().AnyAsync();
			_ = await dbContext.LedgerAccounts.AsNoTracking().AnyAsync();
			_ = await dbContext.LedgerPositions.AsNoTracking().AnyAsync();
		}
		catch (SqlException ex) when (ex.Number == 208)
		{
			Log.Warning("Detected outdated development schema. Recreating LocalDB to apply latest model.");
			await dbContext.Database.EnsureDeletedAsync();
			await dbContext.Database.EnsureCreatedAsync();
		}
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
