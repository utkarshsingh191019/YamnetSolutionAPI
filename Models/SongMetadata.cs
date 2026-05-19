namespace YamnetSolutionAPI.Models
{
    public class SongMetadata
    {
        public string Title { get; set; }
        public List<string> Artists { get; set; }
        public string Album { get; set; }
        public string Label { get; set; }
        public float Score { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public Dictionary<string, string> SourceLinks { get; set; } = new();
    }
}