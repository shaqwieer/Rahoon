using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules;

/// <summary>Single place where each module registers its services and endpoints.</summary>
public static class ModuleRegistry
{
    public static IServiceCollection AddRahoonModules(this IServiceCollection services)
    {
        services.AddScoped<OtpService>();
        services.AddScoped<Market.MarketService>();
        services.AddScoped<OrgDirectory.DirectoryImporter>();
        return services;
    }

    public static IEndpointRouteBuilder MapRahoonEndpoints(this IEndpointRouteBuilder app)
    {
        // Rahoon team sign-in and sessions; owners' and buyers' mobile sign-in.
        AuthEndpoints.Map(app);
        PhoneAuthEndpoints.Map(app);
        // Exit/buy marketplace: sale and buyer requests, team review, opportunities, interests.
        Market.MarketPublicEndpoints.Map(app);
        Market.SaleRequestEndpoints.Map(app);
        Market.BuyerEndpoints.Map(app);
        Market.TeamMarketEndpoints.Map(app);
        Market.TeamOpportunityEndpoints.Map(app);
        // Saudi organization directory (developers, banks, finance companies) and stored files.
        OrgDirectory.DirectoryEndpoints.Map(app);
        Files.FileEndpoints.Map(app);
        return app;
    }
}
