using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Add CORS policy to allow frontend connections.
// Origins come from configuration (Cors:AllowedOrigins, comma-separated); defaults to local dev.
string[] allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? new[]
    {
        "http://localhost:4200",      // Local development server (Angular)
        "http://192.168.1.180:4200",  // Frontend running on another device
        "http://192.168.1.104:4200"   // Another device frontend
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Allow credentials with specific origins
    });
});

// Add services to the container
builder.Services.AddControllers();  // For API controllers
builder.Services.AddSignalR();      // For SignalR hub

// Configure logging
builder.Logging.ClearProviders();   // Clears default logging providers
builder.Logging.AddConsole();       // Adds console logging

var app = builder.Build();

// Enable WebSocket support (Ensure WebSocket is enabled)
app.UseWebSockets();  // Enable WebSockets

// Use CORS policy for frontend communication
app.UseCors("AllowFrontend");

// Map controllers for API endpoints
app.MapControllers();

// Map SignalR hub for real-time communication
app.MapHub<ChatHub>("/chatHub").RequireCors("AllowFrontend");  // Ensure the SignalR hub is mapped with the CORS policy

// Bind address/port come from ASPNETCORE_URLS (set to http://0.0.0.0:5001 in the container).
app.Run();