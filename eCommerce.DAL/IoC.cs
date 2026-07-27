using eCommerce.DAL.Contexts;
using eCommerce.DAL.Repositories.Contarcts;
using eCommerce.DAL.Repositories.Implementations;
using eCommerce.DAL.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace eCommerce.DAL;

public static class IoC
{
    public static IServiceCollection AddDataAccessLayer(this IServiceCollection services, IConfiguration configuration)
    {
        BsonSerializer.RegisterSerializer(new GuidSerializer(MongoDB.Bson.GuidRepresentation.Standard));

        services.Configure<MongoDbSettings>(configuration.GetSection(nameof(MongoDbSettings)));

        services.AddSingleton<MongoDbContext>(); // Instance for the app

        services.AddScoped<IOrderRepository, OrderRepository>(); // Instance for the request

        return services;
    }
}
