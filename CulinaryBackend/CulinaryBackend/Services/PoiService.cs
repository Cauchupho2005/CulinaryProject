using CulinaryBackend.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;

namespace CulinaryBackend.Services
{
    public class PoiService
    {
        private readonly IMongoCollection<Poi> _poiCollection;

        public PoiService(IConfiguration configuration)
        {
            var mongoClient = new MongoClient(configuration["MongoDbSettings:ConnectionString"]);
            var mongoDatabase = mongoClient.GetDatabase(configuration["MongoDbSettings:DatabaseName"]);
            _poiCollection = mongoDatabase.GetCollection<Poi>(configuration["MongoDbSettings:PoiCollectionName"]);

            // Tự động tạo Index 2dsphere khi khởi động app để có thể tìm kiếm GPS
            //var indexKeysDefinition = Builders<Poi>.IndexKeys.Geo2DSphere(p => p.Location);
            //_poiCollection.Indexes.CreateOne(new CreateIndexModel<Poi>(indexKeysDefinition));
        }

        // 1. Lấy toàn bộ danh sách (Dùng cho Admin hoặc tải lần đầu)
        public async Task<List<Poi>> GetAsync() =>
            await _poiCollection.Find(_ => true).ToListAsync();

        // 2. TÌM KIẾM THEO BÁN KÍNH GPS ($nearSphere)
        public async Task<List<Poi>> GetNearbyPoisAsync(double longitude, double latitude, double maxDistanceInMeters)
        {
            var point = GeoJson.Point(GeoJson.Geographic(longitude, latitude));

            var filter = Builders<Poi>.Filter.NearSphere(
                p => p.Location,
                point,
                maxDistance: maxDistanceInMeters);

            return await _poiCollection.Find(filter).ToListAsync();
        }

        // 3. Thêm mới một địa điểm
        public async Task CreateAsync(Poi newPoi) =>
            await _poiCollection.InsertOneAsync(newPoi);
    }
}