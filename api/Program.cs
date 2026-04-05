using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Stripe;
using System.Text;
using XlightsQueue.Data;
using XlightsQueue.Hubs;
using XlightsQueue.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=qtm.db")
    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

// Controllers
builder.Services.AddControllers();

// SignalR
builder.Services.AddSignalR();

// CORS
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                builder.Configuration["AllowedOrigins"]?.Split(',')
                ?? ["http://localhost:5173", "http://localhost:3000"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// JWT Auth
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "qtm-default-secret-key-change-this-in-production";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "QueueTheMagic",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Issuer"] ?? "QueueTheMagic",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        // Support SignalR token from query string
        options.Events = new JwtBearerEvents {
            OnMessageReceived = context => {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.Request.Path.StartsWithSegments("/showHub"))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Stripe
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"] ?? string.Empty;

// Services
builder.Services.AddSingleton<FppService>();
builder.Services.AddSingleton<MqttService>();
builder.Services.AddSingleton<AdminAuthService>();
builder.Services.AddSingleton<PaymentService>();
builder.Services.AddSingleton<QueueService>();
builder.Services.AddSingleton<QueueManagerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<QueueManagerService>());
builder.Services.AddSingleton<IQueueTrigger>(sp => sp.GetRequiredService<QueueManagerService>());

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>()
        .CreateDbContext();
    db.Database.Migrate();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ShowHub>("/showHub");

app.Run();
