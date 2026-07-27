using Microsoft.Extensions.DependencyInjection;

namespace eCommerce.BLL;

public static class IoC
{
    public static IServiceCollection AddBusinessLogicLayer(this IServiceCollection services)
    {
        return services;
    }
}
