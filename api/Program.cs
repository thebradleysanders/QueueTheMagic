using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Stripe;
using System.Text;
using System.Threading.RateLimiting;
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

// ── Refuse to start in production with missing / default / weak secrets ──────────
const string DefaultJwtSecret = "qtm-default-secret-key-change-this-in-production";
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? DefaultJwtSecret;
if (!builder.Environment.IsDevelopment()) {
    var adminPassword = builder.Configuration["AdminPassword"];
    string[] weakPasswords = ["changeme", "your-secure-password", "password", "admin"];
    if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < 8
        || weakPasswords.Contains(adminPassword, StringComparer.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "AdminPassword is missing, too short (min 8) or a well-known default. Set a strong AdminPassword before going live.");

    if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32
        || jwtSecret == DefaultJwtSecret
        || jwtSecret.StartsWith("change-this", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "Jwt__Secret is missing, shorter than 32 characters or a placeholder. Generate one with: openssl rand -base64 48");
}

// ── Rate limiting (per client IP) ────────────────────────────────────────────────
builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    static RateLimitPartition<string> PerIp(HttpContext ctx, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0 });

    // Brute-force protection for the admin password
    options.AddPolicy("login", ctx => PerIp(ctx, 8, TimeSpan.FromMinutes(1)));
    // Stops card-testing / intent spam against Stripe
    options.AddPolicy("payment", ctx => PerIp(ctx, 15, TimeSpan.FromMinutes(1)));
    // Ballot-stuffing protection for song ratings
    options.AddPolicy("rating", ctx => PerIp(ctx, 30, TimeSpan.FromMinutes(1)));
    // Visit counter: generous (many phones can share one yard Wi-Fi / carrier NAT) but bounded
    options.AddPolicy("visit", ctx => PerIp(ctx, 60, TimeSpan.FromMinutes(1)));
    // Cheers: per-device cooldown is enforced in CommunityService; this caps a single network
    options.AddPolicy("cheer", ctx => PerIp(ctx, 120, TimeSpan.FromMinutes(1)));
    // Live audio signalling
    options.AddPolicy("listen", ctx => PerIp(ctx, 20, TimeSpan.FromMinutes(1)));
});

// nginx (127.0.0.1) sits in front of Kestrel and sets X-Forwarded-For.
builder.Services.Configure<ForwardedHeadersOptions>(o => {
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// JWT Auth
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
builder.Services.AddSingleton<ShowControlService>();
builder.Services.AddSingleton<CommunityService>();
builder.Services.AddSingleton<GuestService>();
builder.Services.AddSingleton<AudioRelayService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AudioRelayService>());
builder.Services.AddHostedService<MqttControlWorker>();
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

app.UseForwardedHeaders();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ShowHub>("/showHub");

app.Run();
