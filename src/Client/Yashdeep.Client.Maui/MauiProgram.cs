using Microsoft.Extensions.DependencyInjection;
using Yashdeep.Client.Blazor.DependencyInjection;

namespace Yashdeep.Client.Maui;

public static class MauiProgram
{
    public static void ConfigureServices(IServiceCollection services)
    {
        // Register Blazor Hybrid client foundation services for MAUI shell host
        services.AddYashdeepClientServices();
    }
}
