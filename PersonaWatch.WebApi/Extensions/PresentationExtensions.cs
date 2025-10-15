using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;

namespace PersonaWatch.WebApi.Extensions;

public static class PresentationExtensions
{
    public static IServiceCollection AddPresentation(this IServiceCollection services, bool addGlobalAuthFilter = false)
    {
        services.AddControllers(options =>
        {
            if (addGlobalAuthFilter)
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                options.Filters.Add(new AuthorizeFilter(policy));
            }
        })
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddEndpointsApiExplorer();
        return services;
    }
}
