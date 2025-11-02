using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace QCGO.Models
{
    [BsonIgnoreExtraElements]
    public class Spot
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string? Name { get; set; }

        [BsonElement("district")]
        public string? District { get; set; }

        [BsonElement("barangay")]
        public string? Barangay { get; set; }

        [BsonElement("type")]
        public string? Type { get; set; }

        [BsonElement("image_url")]
        public string? ImageUrl { get; set; } // Store the relative path

        public string? GetImageUrl()
        {
            if (string.IsNullOrEmpty(ImageUrl))
                return null;

            // If it's already an absolute URL, return as-is
            if (ImageUrl.StartsWith("http://") || ImageUrl.StartsWith("https://"))
                return ImageUrl;

            // Normalize Windows backslashes to forward slashes
            var normalized = ImageUrl.Replace('\\', '/').Trim();

            // If path contains "wwwroot", extract the portion after it
            var idx = normalized.IndexOf("wwwroot", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var after = normalized.Substring(idx + "wwwroot".Length);
                var path = after.TrimStart('/', '\\');
                return "/" + path.Replace('\\', '/').TrimStart('/');
            }

            // If the value already starts with a slash, treat it as app-relative
            if (normalized.StartsWith("/"))
                return normalized;

            // Otherwise treat it as relative to wwwroot and return a web-rooted path
            return "/" + normalized.TrimStart('/');
        }

        [BsonElement("description")]
        public string? Description { get; set; }

        [BsonElement("tags")]
        public List<string>? Tags { get; set; }

        [BsonElement("rating")]
        public double? Rating { get; set; }

        [BsonElement("created_at")]
        public DateTime? CreatedAt { get; set; }

        [BsonElement("added_by")]
        public string? AddedBy { get; set; }

        [BsonElement("coordinates")]
        public Coordinates? Coordinates { get; set; }

        [BsonElement("accessibility")]
        public Accessibility? Accessibility { get; set; }

        [BsonElement("map_open_hours")]
        public MapOpenHours? MapOpenHours { get; set; }
    }

    public class Coordinates
    {
        [BsonElement("lat")]
        public double Lat { get; set; }

        [BsonElement("lng")]
        public double Lng { get; set; }
    }

    public class Accessibility
    {
        [BsonElement("public_transport")]
        public bool PublicTransport { get; set; }

        [BsonElement("parking_available")]
        public bool ParkingAvailable { get; set; }

        [BsonElement("wheelchair_accessible")]
        public bool WheelchairAccessible { get; set; }
    }

    public class MapOpenHours
    {
        [BsonElement("url")]
        public string? Url { get; set; }
    }
}
