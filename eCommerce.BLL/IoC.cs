using eCommerce.BLL.Services.Contarcts;
using eCommerce.BLL.Services.Implementations;
using eCommerce.BLL.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace eCommerce.BLL;

/// <summary>
/// Provides an extension method for registering the Business Logic Layer's
/// services, validators, and AutoMapper profiles into the DI container.
/// </summary>
public static class IoC
{
    /// <summary>
    /// Registers Business Logic Layer dependencies, including AutoMapper profiles
    /// discovered in this assembly, FluentValidation validators, and application
    /// services such as <see cref="IOrdersService"/>.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, for chaining.</returns>
    public static IServiceCollection AddBusinessLogicLayer(this IServiceCollection services)
    {
        services.AddAutoMapper(confg =>
        {
            confg.AddMaps(Assembly.GetExecutingAssembly());
        });

        services.AddValidatorsFromAssemblyContaining<OrderAddRequestValidator>();

        services.AddScoped<IOrdersService, OrderService>();

        return services;
    }
}