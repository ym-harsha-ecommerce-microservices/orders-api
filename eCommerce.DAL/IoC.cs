using eCommerce.DAL.Contexts;
using eCommerce.DAL.Repositories.Contracts;
using eCommerce.DAL.Repositories.Implementations;
using eCommerce.DAL.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace eCommerce.DAL;

/// <summary>
/// Provides an extension method for registering the Data Access Layer's
/// MongoDB context, repositories, and BSON serialization settings into the DI container.
/// </summary>
public static class IoC
{
    /// <summary>
    /// Registers Data Access Layer dependencies: configures MongoDB GUID serialization,
    /// binds and resolves <see cref="MongoDbSettings"/> (substituting host/port from
    /// environment variables), and registers <see cref="MongoDbContext"/> and repository implementations.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration used to bind <see cref="MongoDbSettings"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, for chaining.</returns>
    public static IServiceCollection AddDataAccessLayer(this IServiceCollection services, IConfiguration configuration)
    {
        BsonSerializer.RegisterSerializer(new GuidSerializer(MongoDB.Bson.GuidRepresentation.Standard));

        services.Configure<MongoDbSettings>(configuration.GetSection(nameof(MongoDbSettings)));

        services.PostConfigure<MongoDbSettings>(settings =>
        {
            var host = Environment.GetEnvironmentVariable("DB_HOST");
            var port = Environment.GetEnvironmentVariable("DB_PORT");
            var databaseName = Environment.GetEnvironmentVariable("DB_NAME");
            settings.ConnectionString = settings.ConnectionString.Replace("$DB_HOST", host)
            .Replace("$DB_PORT", port);

            settings.DatabaseName = settings.DatabaseName.Replace("$DB_NAME", databaseName);
        });


        services.AddSingleton<MongoDbContext>(); // Instance for the app

        services.AddScoped<IOrderRepository, OrderRepository>(); // Instance for the request

        return services;
    }
}