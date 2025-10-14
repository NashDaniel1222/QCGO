namespace QCGO.Models
{
    public class SpotInputViewModel
    {
        public string Name { get; set; }
        public string District { get; set; }
        public string Barangay { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<string> Tags { get; set; }
        public bool PublicTransport { get; set; }
        public bool ParkingAvailable { get; set; }
        public bool WheelchairAccessible { get; set; }
        public string? MapUrl { get; set; }
    }
}
