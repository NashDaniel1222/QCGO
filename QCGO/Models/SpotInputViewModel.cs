namespace QCGO.Models
{
    public class SpotInputViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Barangay { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
        public bool PublicTransport { get; set; }
        public bool ParkingAvailable { get; set; }
        public bool WheelchairAccessible { get; set; }
        public string? MapUrl { get; set; }
    }
}
