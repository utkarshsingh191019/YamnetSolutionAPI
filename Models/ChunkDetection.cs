namespace YamnetSolutionAPI.Models
{
    public class ChunkDetection
    {
        public int ChunkIndex { get; set; }
        public List<string> Labels { get; set; } = new();
        public List<double> Scores { get; set; } = new();
    }
}