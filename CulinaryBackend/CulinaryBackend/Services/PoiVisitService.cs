using CulinaryBackend.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CulinaryBackend.Services
{
    public class PoiVisitService
    {
        private readonly IMongoCollection<PoiVisit> _visitCollection;
        private readonly PoiService _poiService;

        public PoiVisitService(IMongoDatabase mongoDatabase, PoiService poiService)
        {
            _visitCollection = mongoDatabase.GetCollection<PoiVisit>("PoiVisits");
            _poiService = poiService;
        }

        public async Task TrackVisitAsync(string poiId, string? userAgent)
        {
            var visit = new PoiVisit
            {
                PoiId = poiId,
                VisitedAt = DateTime.UtcNow,
                UserAgent = userAgent
            };
            await _visitCollection.InsertOneAsync(visit);
        }

        public async Task<List<PoiVisitStats>> GetVisitStatsAsync()
        {
            var pipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$poiId" },
                    { "count", new BsonDocument("$sum", 1) }
                }),
                new BsonDocument("$sort", new BsonDocument("count", -1))
            };

            var result = await _visitCollection.Aggregate<BsonDocument>(pipeline).ToListAsync();
            var stats = new List<PoiVisitStats>();

            foreach (var doc in result)
            {
                var idValue = doc["_id"];
                var poiId = idValue.BsonType == BsonType.ObjectId
                    ? idValue.AsObjectId.ToString()
                    : idValue.AsString;
                var count = doc["count"].AsInt32;
                var poi = await _poiService.GetByIdAsync(poiId);
                
                stats.Add(new PoiVisitStats
                {
                    PoiId = poiId,
                    PoiTitle = poi?.Localizations?.ContainsKey("vi") == true 
                        ? poi.Localizations["vi"].Title 
                        : "Unknown",
                    TotalVisits = count
                });
            }

            return stats;
        }

        public async Task<int> GetPoiVisitCountAsync(string poiId)
        {
            var count = await _visitCollection.CountDocumentsAsync(v => v.PoiId == poiId);
            return (int)count;
        }
    }
}
