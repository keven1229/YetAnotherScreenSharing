using YASS.Server.Api.Services;
using YASS.Shared.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "YASS API", Version = "v1" });
});

// CORS for Blazor WASM and Desktop Client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Application services
builder.Services.AddSingleton<IRoomService, InMemoryRoomService>();
builder.Services.AddSingleton<IAuthService, DefaultAuthService>();

// Configuration
builder.Services.Configure<SrsSettings>(builder.Configuration.GetSection("Srs"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
