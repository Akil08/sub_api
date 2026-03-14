using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Hangfire;
using Hangfire.PostgreSql;
using subscription_api.Data;
using subscription_api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// PostgreSQL with Entity Framework Core
var connectionString = builder.Configuration.GetConnectionString("PostgreSql");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// Redis for rate limiting
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var redisConn = config.GetConnectionString("Redis");
    return ConnectionMultiplexer.Connect(redisConn ?? "localhost:6379");
});

// Services
builder.Services.AddScoped<IRateLimitService, RateLimitService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

// Hangfire with PostgreSQL storage
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(connectionString)
);
builder.Services.AddHangfireServer();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Hangfire Dashboard
app.UseHangfireDashboard();

// Register recurring job through Hangfire + DI-managed interface implementation
RecurringJob.AddOrUpdate<ISubscriptionService>(
    "DailySubscriptionJob",
    service => service.RunDailyJobAsync(),
    "0 2 * * *",
    timeZone: TimeZoneInfo.Utc
);

// Run migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

app.Run();