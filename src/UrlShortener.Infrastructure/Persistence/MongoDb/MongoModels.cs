using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UrlShortener.Infrastructure.Persistence.MongoDb;

public sealed class ShortUrlDocument
{
    [BsonId]
    public ObjectId Id { get; init; }

    [BsonElement("code")]
    public string Code { get; init; } = string.Empty;

    [BsonElement("originalUrl")]
    public string OriginalUrl { get; init; } = string.Empty;

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }

    [BsonElement("expiresAtUtc")]
    public DateTime? ExpiresAtUtc { get; init; }
}

public sealed class ShortUrlAnalyticsDocument
{
    [BsonId]
    [BsonElement("code")]
    public string Code { get; init; } = string.Empty;

    [BsonElement("totalResolutions")]
    public long TotalResolutions { get; init; }

    [BsonElement("lastResolvedAtUtc")]
    public DateTime? LastResolvedAtUtc { get; init; }
}
