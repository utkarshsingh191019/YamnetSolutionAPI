using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace YamnetSolutionAPI.Services
{
    public class FfmpegService
    {
    public void FixMp3Headers(string inputPath, string outputPath)
    {
    string rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,@"..\..\.."));
    var ffmpegPath = Path.Combine(rootPath, "ffmpeg", "bin", "ffmpeg.exe");

    var args = $"-hide_banner -loglevel error -i \"{inputPath}\" -af \"afftdn=nf=-25\" -codec:a libmp3lame -b:a 192k -y \"{outputPath}\"";
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }
    };

    process.Start();
    process.WaitForExit();
}

        public void ExtractAudio(string videoPath, string outputPath)
        {
            //Directory.CreateDirectory("C:/Users/10653343/Downloads/YamnetSolutionAPI/FFMPEG_output");
            var args = $"-i \"{videoPath}\" -vn -acodec pcm_s16le -ar 44100 -ac 2 \"{outputPath}\" -y";
            RunFFmpeg(args);
        }

        public List<string> SplitAudio(string inputPath,string baseRootPath, int seconds = 15)
        {
            //Directory.CreateDirectory("C:/Users/10653343/Downloads/YamnetSolutionAPI/FFMPEG_output/audio_chunks");
            var chunks = new List<string>();
            for (int i = 0; ; i++)
            {
                // string output = $"C:/Users/10653343/Downloads/YamnetSolutionAPI/FFMPEG_output/audio_chunks/chunk_{i}.wav";
                string output = Path.Combine(baseRootPath, "Audio", "audio_chunks", $"chunk_{i}.wav");
                string args = $"-i \"{inputPath}\" -ss {i * seconds} -t {seconds} -y \"{output}";
                RunFFmpeg(args);
                if (!File.Exists(output) || new FileInfo(output).Length < 10000) break;
                chunks.Add(output);
            }
            return chunks;
        }

        private void RunFFmpeg(string args)
        {
            string rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,@"..\..\.."));
            var ffmpegPath = Path.Combine(rootPath, "ffmpeg", "bin", "ffmpeg.exe");
            Process.Start(new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = true
            })?.WaitForExit();
        }
    }
}