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

        [BsonElement("bookmarks")]
        public List<string> Bookmarks { get; set; } = new List<string>();

        // Optional display name (username shown in UI) separate from the login identifier
        [BsonElement("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        // Role (e.g., "user", "admin"). Default to "user" for new registrations.
        [BsonElement("role")]
        public string Role { get; set; } = "user";

        // Gender (optional)
        [BsonElement("gender")]
        public string Gender { get; set; } = string.Empty;

        // Birthday (optional)
        [BsonElement("birthday")]
        public DateTime? Birthday { get; set; } = null;
    }
}
