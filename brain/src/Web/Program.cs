using Brain.Application;
using Brain.Infrastructure.Data;
using Brain.Infrastructure.DependencyInjection;
using Brain.Web.Endpoints;
using Brain.Web.Filters;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
	.ReadFrom.Configuration(builder.Configuration)
	.WriteTo.Console()
	.CreateLogger();

builder.Host.UseSerilog();

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
	.MapSignalsEndpoints();

app.MapMt5Endpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
	var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
	await dbContext.Database.EnsureCreatedAsync();
}

app.Run();

public partial class Program;
