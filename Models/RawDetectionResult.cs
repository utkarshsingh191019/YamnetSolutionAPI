namespace YamnetSolutionAPI.Models
{
    public class RawDetectionResult
    {
        public string ChunkPath { get; set; }
        public List<string> Labels { get; set; }
        public List<double> Scores { get; set; }
        public List<double> Timestamps { get; set; }
        public string Error { get; set; }
    }
}