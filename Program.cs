var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Database connection string from Railway environment variable
// Handle PostgreSQL URLs (e.g., from Neon) by converting to Npgsql connection string format
var rawConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(rawConnectionString))
{
    var connectionString = rawConnectionString;
    
    // If it's a PostgreSQL URL (postgresql://), parse and convert it
    if (connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
        connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var uri = new Uri(connectionString);
            var connStringBuilder = new System.Text.StringBuilder();
            
            // Extract components from URL
            var dbHost = uri.Host;
            var dbPort = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');
            var username = Uri.UnescapeDataString(uri.UserInfo.Split(':')[0]);
            var password = uri.UserInfo.Contains(':') 
                ? Uri.UnescapeDataString(uri.UserInfo.Substring(uri.UserInfo.IndexOf(':') + 1))
                : "";
            
            // Build Npgsql connection string
            connStringBuilder.Append($"Host={dbHost};Port={dbPort};Database={database};Username={username}");
            if (!string.IsNullOrEmpty(password))
            {
                connStringBuilder.Append($";Password={password}");
            }
            
            // Parse query string for additional parameters (e.g., sslmode)
            var sslMode = "Require";
            if (!string.IsNullOrEmpty(uri.Query) && uri.Query.Length > 1)
            {
                var queryString = uri.Query.Substring(1); // Remove '?'
                var queryParams = queryString.Split('&');
                foreach (var param in queryParams)
                {
                    var parts = param.Split('=');
                    if (parts.Length == 2 && parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                    {
                        sslMode = Uri.UnescapeDataString(parts[1]);
                        break;
                    }
                }
            }
            connStringBuilder.Append($";SSL Mode={sslMode}");
            
            connectionString = connStringBuilder.ToString();
        }
        catch (Exception ex)
        {
            // If parsing fails, log and use original connection string (Npgsql might handle it)
            Console.WriteLine($"Warning: Failed to parse PostgreSQL URL: {{ex.Message}}");
        }
    }
    
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;
}

// Configure URL for Railway deployment
var port = Environment.GetEnvironmentVariable("PORT");
var url = string.IsNullOrEmpty(port) ? "http://0.0.0.0:8080" : $"http://0.0.0.0:{port}";
builder.WebHost.UseUrls(url);

var app = builder.Build();

// Enable Swagger in all environments (including production)
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Add a simple root route to verify the service is running
app.MapGet("/", () => new { 
    message = "Backend API is running", 
    status = "ok",
    swagger = "/swagger",
    api = "/api/test"
});

app.Run();
