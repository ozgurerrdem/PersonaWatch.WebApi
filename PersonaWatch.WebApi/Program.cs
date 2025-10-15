using PersonaWatch.WebApi.Extensions;
using PersonaWatch.WebApi.Hosting;

var builder = WebApplication.CreateBuilder(args);

// DI kümeleri
builder.Services.AddPresentation(addGlobalAuthFilter: false);
builder.Services.AddOpenApi();
builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// Opsiyonel: seed'i hosted service ile çalıştır
builder.Services.AddHostedService<DatabaseSeederHostedService>();

var app = builder.Build();

// Pipeline
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseOpenApiIfDev(app.Environment);
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
