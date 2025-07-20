using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PersonaWatch.WebApi.Data;
using PersonaWatch.WebApi.Services.Interfaces;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var allowedOrigin = builder.Configuration["Cors:FrontendOrigin"];

// Add services to the container.
builder.Services.AddControllers();
//builder.Services.AddControllers(options =>
//{
//    var policy = new AuthorizationPolicyBuilder()
//        .RequireAuthenticatedUser()
//        .Build();
//    //options.Filters.Add(new AuthorizeFilter(policy));
//});

builder.Services.AddScoped<TokenService>();
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
//builder.Services.AddScoped<IScanner, SerpApiScannerService>();
//builder.Services.AddScoped<IScanner, YouTubeScannerService>();
builder.Services.AddScoped<IScanner, FilmotScannerService>();

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

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
