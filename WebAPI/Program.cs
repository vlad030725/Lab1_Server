using WebAPI.Logging;
using WebAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddFileLogger(builder.Configuration.GetSection("FileLogging"));

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendClient", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("FrontendClient");
app.UseAuthorization();
app.MapGet("/api/health/live", () => Results.Ok(new { status = "Live" }));
app.MapGet("/api/health/ready", () => Results.Ok(new { status = "Ready" }));
app.MapControllers();
app.Run();
