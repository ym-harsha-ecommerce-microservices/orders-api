using eCommerce.BLL.BackgroundServices;
using eCommerce.BLL.HttpClients;
using eCommerce.BLL.Policies.Implementations;
using eCommerce.BLL.Policies.Interfaces;
using eCommerce.BLL.RabbitMQ;
using eCommerce.BLL.Services.Contracts;
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

        services.AddSingleton<ICacheService, DistributedCacheService>();
        services.AddSingleton<IRabbitMQConsumer, RabbitMQConsumer>();
        services.AddSingleton<IRabbitMQPublisher, RabbitMQPublisher>();

        services.AddHostedService<RabbitMQBackgroundService>();

        services.Configure<RabbitMQOptions>(options =>
        {
            options.RABBITMQ_HOST = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
            options.RABBITMQ_PORT = Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? "5672";
            options.RABBITMQ_USERNAME = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest";
            options.RABBITMQ_PASSWORD = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest";
            options.RABBITMQ_PRODUCT_EXCHANGE = Environment.GetEnvironmentVariable("RABBITMQ_PRODUCT_EXCHANGE") ?? "product.exchange";
            options.RABBITMQ_PRODUCT_UPDATE_NAME_ROUTEING_KEY = Environment.GetEnvironmentVariable("RABBITMQ_PRODUCT_UPDATE_NAME_ROUTEING_KEY") ?? "product.update.name";
            options.RABBITMQ_PRODUCT_DELETE_ROUTEING_KEY = Environment.GetEnvironmentVariable("RABBITMQ_PRODUCT_DELETE_ROUTEING_KEY") ?? "product.delete";
            options.RABBITMQ_PRODUCT_DELETE_QUEUE = Environment.GetEnvironmentVariable("RABBITMQ_PRODUCT_DELETE_QUEUE") ?? "product.delete.queue";
            options.RABBITMQ_PRODUCT_UPDATE_QUEUE = Environment.GetEnvironmentVariable("RABBITMQ_PRODUCT_UPDATE_QUEUE") ?? "product.update.queue";
        });


        services.AddAutoMapper(confg =>
        {
            confg.AddMaps(Assembly.GetExecutingAssembly());
        });

        services.AddValidatorsFromAssemblyContaining<OrderAddRequestValidator>();

        services.AddScoped<IOrdersService, OrderService>();

        services.AddTransient<IPolicyService, PolicyService>();
        services.AddTransient<IUsersMicroservicePolicies, UsersMicroservicePolicies>();
        services.AddTransient<IProductsMicroservicePolicies, ProductsMicroservicePolicies>();


        services.AddHttpClient<IUsersMicroserviceHttpClient, UsersMicroserviceHttpClient>(client =>
        {
            var usersMicroserviceName = Environment.GetEnvironmentVariable("USERS_MICROSERVICE_NAME");
            var usersMicroservicePort = Environment.GetEnvironmentVariable("USERS_MICROSERVICE_PORT");
            client.BaseAddress = new Uri($"http://{usersMicroserviceName}:{usersMicroservicePort}");
        })
        .AddPolicyHandler((serviceProvider, request) =>
        {
            var usersPolicies = serviceProvider.GetRequiredService<IUsersMicroservicePolicies>();
            return usersPolicies.GetUsersPolicies();
        });


        services.AddHttpClient<IProductsMicroserviceHttpClient, ProductsMicroserviceHttpClient>(client =>
        {
            var productsMicroserviceName = Environment.GetEnvironmentVariable("PRODUCTS_MICROSERVICE_NAME");
            var productsMicroservicePort = Environment.GetEnvironmentVariable("PRODUCTS_MICROSERVICE_PORT");
            client.BaseAddress = new Uri($"http://{productsMicroserviceName}:{productsMicroservicePort}");
        })
            .AddPolicyHandler((serviceProvider, request) =>
        {
            var productsPolicies = serviceProvider.GetRequiredService<IProductsMicroservicePolicies>();
            return productsPolicies.GetProductsPolicies();
        });

        services.AddStackExchangeRedisCache(options =>
        {
            var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST");
            var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT");
            options.Configuration = $"{redisHost}:{redisPort}";
            options.InstanceName = "OrdersService_Cache_";
        });




        return services;
    }
}