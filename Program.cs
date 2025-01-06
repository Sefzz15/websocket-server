using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Add CORS policy to allow frontend connections
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",    // Local development server (Angular)
                "http://192.168.1.180:4200",  // Frontend running on another device
                "http://192.168.1.104:4200"  // Another device frontend
            )
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

// Make sure the app listens on all IP addresses for remote access
// app.Run("http://192.168.1.180:5001");  //ektelesh me prosvash pou mporoun na ftasoun stin ip 192.168.1.180
app.Run();