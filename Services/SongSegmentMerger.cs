using YamnetSolutionAPI.Models;
using Newtonsoft.Json; 

namespace YamnetSolutionAPI.Services
{
    public class SongSegmentMerger
    {
        private const int ChunkDuration = 15;
        private const int MinMusicFrames = 10;
        private const double ConfidenceThreshold = 0.5;

        public List<MusicSegment> MergeMusicChunks(List<ChunkDetection> chunkDetections)
        {
            var segments = new List<MusicSegment>();
            double currentStart = -1;
            var currentScores = new List<double>();

            for (int i = 0; i < chunkDetections.Count; i++)
            {
                var detection = chunkDetections[i];

               
                //Console.WriteLine($"Raw detection for chunk {i}:");
                //Console.WriteLine(JsonConvert.SerializeObject(detection));

                int musicHits = 0;

                for (int j = 0; j < detection.Labels.Count; j++)
                {
                    var label = detection.Labels[j].ToLower();
                    var score = detection.Scores[j];

                    
                    //Console.WriteLine($"Chunk {i} label: {label}, score: {score}");

                    if ((label.Contains("music") || label.Contains("song") || label.Contains("singing") || label.Contains("melody"))
                        && score > ConfidenceThreshold)
                    {
                        musicHits++;
                        currentScores.Add(score);
                    }
                }
                if (musicHits >= MinMusicFrames)
                {
                    double avgScore = currentScores.Count > 0 ? currentScores.Average() : 0;
                    double start = i * ChunkDuration;
                    double end = (i + 1) * ChunkDuration;

                    Console.WriteLine($"Start: {start:0.00}s, End: {end:0.00}s, Frames: {musicHits}, Avg Score: {avgScore:0.00}");
                }

                if (musicHits >= MinMusicFrames)
                {
                    if (currentStart == -1)
                        currentStart = i * ChunkDuration;
                }
                else if (currentStart != -1)
                {
                    segments.Add(new MusicSegment
                    {
                        Start = currentStart,
                        End = i * ChunkDuration,
                        Scores = new List<double>(currentScores)
                    });
                    currentStart = -1;
                    currentScores.Clear();
                }
            }

            if (currentStart != -1)
            {
                segments.Add(new MusicSegment
                {
                    Start = currentStart,
                    End = chunkDetections.Count * ChunkDuration,
                    Scores = new List<double>(currentScores)
                });
            }

            return segments;
        }
    }
}
