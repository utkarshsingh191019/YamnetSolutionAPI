namespace YamnetSolutionAPI.Models
{
    public class MusicSegment
    {
        public double Start { get; set; }
        public double End { get; set; }
        public List<double> Scores { get; set; } = new();
    }
}
