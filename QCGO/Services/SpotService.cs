using MongoDB.Bson;
using MongoDB.Driver;
using QCGO.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace QCGO.Services
{
    public class SpotService
    {
        private readonly IMongoCollection<Spot>? _spots;
        private readonly ILogger<SpotService> _logger;
        private readonly bool _connected;

        public SpotService(MongoSettings settings, ILogger<SpotService> logger)
        {
            _logger = logger;
            try
            {
                var client = new MongoClient(settings.ConnectionString);
                var db = client.GetDatabase(settings.DatabaseName ?? "QCGO");
                _spots = db.GetCollection<Spot>(settings.SpotsCollectionName ?? "spots");

                db.RunCommandAsync((Command<BsonDocument>)"{ping:1}").GetAwaiter().GetResult();
                _connected = true;

                _logger.LogInformation("Connected to MongoDB database '{dbName}' and collection '{col}'.",
                    settings.DatabaseName, settings.SpotsCollectionName);

                try
                {
                    var total = _spots.CountDocuments(Builders<Spot>.Filter.Empty);
                    _logger.LogInformation("SpotService: collection contains {count} document(s).", total);
                }
                catch (Exception exCount)
                {
                    _logger.LogWarning(exCount, "SpotService: failed to count documents in collection.");
                }
            }
            catch (Exception ex)
            {
                _connected = false;
                _logger.LogError(ex, "Failed to initialize MongoDB SpotService. Mongo connection string: {conn}",
                    settings.ConnectionString);
            }
        }

        // ✅ Add new spot to MongoDB
        public void AddSpot(Spot spot)
        {
            // AddSpot functionality removed/disabled. Keep method as no-op to avoid breaking calls.
            _logger.LogInformation("AddSpot called but is disabled in this build.");
            return;
        }

        public List<Spot> GetAll()
        {
            if (!_connected || _spots == null)
            {
                _logger.LogWarning("SpotService is not connected to MongoDB. Returning empty list.");
                return new List<Spot>();
            }

            try
            {
                return _spots.Find(Builders<Spot>.Filter.Empty).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching spots from MongoDB.");
                return new List<Spot>();
            }
        }

        public Spot? GetById(string id)
        {
            if (!_connected || _spots == null) return null;

            try
            {
                var filter = Builders<Spot>.Filter.Eq(s => s.Id, id);
                return _spots.Find(filter).FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching spot by ID from MongoDB.");
                return null;
            }
        }

        // Accept single or multiple tag/district values. Keep backward-compatible overload by allowing callers
        // to pass comma-separated strings or arrays handled in controller.
        public List<Spot> Search(string? q, IEnumerable<string>? tags = null, IEnumerable<string>? districts = null)
        {
            if (!_connected || _spots == null) return new List<Spot>();

            try
            {
                var filters = new List<FilterDefinition<Spot>>();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var nameFilter = Builders<Spot>.Filter.Regex("name", new BsonRegularExpression(q, "i"));
                    var barangayFilter = Builders<Spot>.Filter.Regex("barangay", new BsonRegularExpression(q, "i"));
                    var tagsFilter = Builders<Spot>.Filter.Regex("tags", new BsonRegularExpression(q, "i"));
                    filters.Add(Builders<Spot>.Filter.Or(nameFilter, barangayFilter, tagsFilter));
                }

                // Tags: match any of the provided tags. The Spot document stores tags as an array, so use $in
                if (tags != null)
                {
                    var tagList = tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList();
                    if (tagList.Count > 0)
                    {
                        filters.Add(Builders<Spot>.Filter.In("tags", tagList));
                    }
                }

                // Districts: match any of the provided district values
                if (districts != null)
                {
                    var districtList = districts.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim()).ToList();
                    if (districtList.Count > 0)
                    {
                        filters.Add(Builders<Spot>.Filter.In("district", districtList));
                    }
                }

                var finalFilter = filters.Count == 0
                    ? Builders<Spot>.Filter.Empty
                    : Builders<Spot>.Filter.And(filters);

                return _spots.Find(finalFilter).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while searching spots in MongoDB.");
                return new List<Spot>();
            }
        }

        public List<string> GetTopTags(int count = 7)
        {
            if (!_connected || _spots == null) return new List<string>();

            try
            {
                var pipeline = new[]
                {
                    new BsonDocument("$unwind", "$tags"),
                    new BsonDocument("$group", new BsonDocument
                    {
                        { "_id", "$tags" },
                        { "count", new BsonDocument("$sum", 1) }
                    }),
                    new BsonDocument("$sort", new BsonDocument("count", -1)),
                    new BsonDocument("$limit", count)
                };

                var result = _spots.Aggregate<BsonDocument>(pipeline).ToList();
                return result.Select(doc => doc["_id"].AsString).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while aggregating top tags in MongoDB.");
                return new List<string>();
            }
        }

        // Return all distinct tags across the spots collection, sorted alphabetically.
        public List<string> GetAllTags()
        {
            if (!_connected || _spots == null) return new List<string>();

            try
            {
                // Use an aggregation pipeline to unwind tags and return distinct tag names sorted ascending
                var pipeline = new[]
                {
                    new BsonDocument("$unwind", "$tags"),
                    new BsonDocument("$group", new BsonDocument
                    {
                        { "_id", "$tags" }
                    }),
                    new BsonDocument("$sort", new BsonDocument("_id", 1))
                };

                var result = _spots.Aggregate<BsonDocument>(pipeline).ToList();
                return result.Select(doc => doc["_id"].AsString).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while aggregating all tags in MongoDB.");
                return new List<string>();
            }
        }
    }
}
