using eCommerce.DAL.Entities;
using eCommerce.DAL.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;

namespace eCommerce.DAL.Contexts;

/// <summary>
/// Provides access to the MongoDB database and its collections, using
/// connection settings resolved from <see cref="MongoDbSettings"/>.
/// </summary>
public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly MongoDbSettings _settings;

    /// <summary>
    /// Initializes a new instance of <see cref="MongoDbContext"/>, connecting to
    /// MongoDB using the provided settings.
    /// </summary>
    /// <param name="settings">The MongoDB connection settings, including connection string and database name.</param>
    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        _settings = settings.Value;
        var client = new MongoClient(_settings.ConnectionString);
        _database = client.GetDatabase(_settings.DatabaseName);
    }

    /// <summary>
    /// Gets the MongoDB collection containing <see cref="Order"/> documents.
    /// </summary>
    public IMongoCollection<Order> Orders => _database.GetCollection<Order>(_settings.OrdersCollectionName);
}