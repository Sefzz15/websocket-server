
var builder = WebApplication.CreateBuilder(args);

// Add CORS policy to allow requests from the frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200") // Άδεια στο Angular frontend
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // Απαραίτητο για SignalR με credentials
        });
});

// Retrieve connection string from configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");


// Add controllers for API endpoints
builder.Services.AddControllers();
builder.Services.AddLogging();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddSignalR();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();




var app = builder.Build();

app.UseCors("AllowLocalhost");
app.UseRouting();
app.MapControllers();

app.MapHub<ChatHub>("/chatHub");  // Ρύθμιση του SignalR Hub
app.MapControllers();  // Ρύθμιση των API endpoints

app.Run();
