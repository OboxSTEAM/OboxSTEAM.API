using OboxSteam.API.Architecture;
using OboxSteam.API.Middlewares;
using OboxSteam.Application.Interfaces;
using SwaggerThemes;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Text.Json.Serialization;

EnvFileLoader.LoadFromSolutionRoot();

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.SetupIocContainer();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                // Allow all origins in development
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            }
            else
            {
                // Load allowed origin from environment variable in production
                var allowedOrigin = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGIN")
                    ?? "https://oboxsteam.com";

                policy.WithOrigins(allowedOrigin)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            }
        });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// Tắt việc map claim mặc định
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.WebHost.UseUrls("http://0.0.0.0:5000");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});
var app = builder.Build();

// Apply database migrations before anything else
app.Logger.LogInformation("Starting OboxSteam API...");
app.Logger.LogInformation($"Environment: {app.Environment.EnvironmentName}");
app.Logger.LogInformation($"IsDevelopment: {app.Environment.IsDevelopment()}");
try
{
    app.ApplyMigrations(app.Logger);
    app.Logger.LogInformation("Database migrations completed successfully");
}
catch (Exception e)
{
    app.Logger.LogCritical(e, "CRITICAL: Failed to apply database migrations. Application cannot start.");
    throw; // Stop application if migrations fail
}

app.UseCors("AllowFrontend");

// Static files for Swagger UI customization
app.UseStaticFiles();

// Middlewares
app.UseMiddleware<GlobalExceptionMiddleware>();

// Configure the HTTP request pipeline - REMEMBER
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OboxSteam API v1");
        c.RoutePrefix = string.Empty;
        c.HeadContent = $@"
            <style>{SwaggerTheme.GetSwaggerThemeCss(Theme.Dracula)}</style>
            <style>
                #checkboxContainer label {{
                    color: #000000 !important;
                    background: #f8fafc !important;
                    border: 2px solid #e5e7eb !important;
                }}
                #checkboxContainer label * {{
                    color: #000000 !important;
                }}
            </style>";
        c.ConfigObject.AdditionalItems.Add("persistAuthorization", "true");
        c.InjectJavascript("/custom-swagger.js");
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.MapControllers();

app.Logger.LogInformation("OboxSteam API is running on http://0.0.0.0:5000");
app.Run();