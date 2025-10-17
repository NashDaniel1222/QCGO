using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace QCGO.Models
{
    public class Account
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("username")]
        public string Username { get; set; } = string.Empty;

        [BsonElement("password")]
        public string Password { get; set; } = string.Empty; // stored plaintext in your screenshot - replace with hash in production
    }
}
