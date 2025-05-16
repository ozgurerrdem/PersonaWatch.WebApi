using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonaWatch.WebApi.Data;
using PersonaWatch.WebApi.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);
var allowedOrigin = builder.Configuration["Cors:FrontendOrigin"];

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigin!)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<ScanService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IScanner, SerpApiScannerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!context.Users.Any(u => u.Username == "admin"))
    {
        var hasher = new PasswordHasher<User>();

        var user = new User
        {
            Username = "admin",
            FirstName = "Admin",
            LastName = "Admin",
            IsAdmin = true,
            
        };

        user.Password = hasher.HashPassword(user, "admin");

        context.Users.Add(user);
        context.SaveChanges();
    }
}

app.Run();
