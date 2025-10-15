using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PersonaWatch.WebApi.Data;
using PersonaWatch.WebApi.Services;
using PersonaWatch.WebApi.Services.Interfaces;

namespace PersonaWatch.WebApi.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // DbContext
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
        });

        // HttpClient’lar
        services.AddHttpClient();                    // generic factory
        services.AddHttpClient<ApifyService>();
        services.AddHttpClient<EksiScannerService>();

        // Uygulama servisleri
        services.AddScoped<TokenService>();
        services.AddScoped<ScanService>();

        // IClip implementasyonu
        services.AddScoped<IClipService, ClipService>();

        // IScanner implementasyonları (çoklu kayıt — hepsi IScanner olarak enjekte edilebilir)
        services.AddScoped<IScanner, SerpApiScannerService>();
        services.AddScoped<IScanner, YouTubeScannerService>();
        services.AddScoped<IScanner, FilmotScannerService>();
        services.AddScoped<IScanner, EksiScannerService>();
        services.AddScoped<IScanner, SikayetvarScannerService>();

        // Apify tabanlı scanner’lar
        services.AddScoped<IScanner, XApifyScannerService>();
        services.AddScoped<IScanner, InstagramApifyScannerService>();
        services.AddScoped<IScanner, FacebookApifyScannerService>();
        services.AddScoped<IScanner, TiktokApifyScannerService>();

        // Authentication / JWT
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = configuration["Jwt:Issuer"],
                    ValidAudience            = configuration["Jwt:Audience"],
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!))
                };
            });

        return services;
    }
}
