using Microsoft.AspNetCore.Mvc;
using YamnetSolutionAPI.Models;
using YamnetSolutionAPI.Services;
using System.Diagnostics;

namespace YamnetSolutionAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SongDetectionController : ControllerBase
    {
        private readonly FfmpegService _ffmpeg;
        private readonly PythonYamnetService _yamnet;
        private readonly SongSegmentMerger _segmentMerger;
        private readonly AcrCloudService _acr;
        private readonly string baseVideoPath = @"C:\Users\10653343\Downloads\VideoTest";
        private readonly string baseRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\.."));

        public SongDetectionController(FfmpegService ffmpeg, PythonYamnetService yamnet,
            SongSegmentMerger segmentMerger, AcrCloudService acr)
        {
            _ffmpeg = ffmpeg;
            _yamnet = yamnet;
            _segmentMerger = segmentMerger;
            _acr = acr;
        }

       
[HttpPost("detect-media")]
  public async Task<IActionResult> DetectFromMedia([FromBody] string fileName)
   {
    var fullPath = Path.Combine(baseVideoPath, fileName);

    if (string.IsNullOrWhiteSpace(fullPath) || !System.IO.File.Exists(fullPath))
        return BadRequest("Invalid or missing file.");

    var extension = Path.GetExtension(fullPath).ToLower();

    var supportedExtensions = new[] { ".mp4", ".mp3", ".wav", ".avi", ".mkv", ".mov", ".webm" };
    if (!supportedExtensions.Contains(extension))
        return BadRequest("Unsupported format");

    return await ProcessDetection(fullPath);
}

[HttpPost("detect-blob")]
[Consumes("multipart/form-data")]
    public async Task<IActionResult> DetectFromBlob([FromForm] BlobUploadRequest request)
        {
            var blobFile = request.file;

            if (blobFile == null || blobFile.Length == 0)
                return BadRequest("Blob file is missing or empty.");

            var originalPath = Path.Combine(baseVideoPath, blobFile.FileName);
            using (var stream = new FileStream(originalPath, FileMode.Create))
            {
                await blobFile.CopyToAsync(stream);
            }

            var extension = Path.GetExtension(originalPath).ToLower();
            string processedPath = originalPath;

            if (extension == ".mp3")
            {
                var fixedPath = Path.Combine(baseVideoPath, "fixed_" + blobFile.FileName);
                _ffmpeg.FixMp3Headers(originalPath, fixedPath);
                processedPath = fixedPath;
            }

            
            var sw = Stopwatch.StartNew();
            Console.WriteLine("Splitting audio into 15s chunks...");
            sw.Restart();
            var chunks = _ffmpeg.SplitAudio(processedPath, baseRootPath, 15);
            sw.Stop();
            Console.WriteLine($"Audio splitting complete in {sw.Elapsed.TotalSeconds:F2} sec");
            
            Console.WriteLine("Detecting Blob data using ACRCloud...");


            sw.Restart();
            var metadata = await _acr.IdentifyBlobsAsync(chunks);
            sw.Stop();
            Console.WriteLine($"ACRCloud Blob detection complete in {sw.Elapsed.TotalSeconds:F2} sec");

            
             System.IO.DirectoryInfo dire = new DirectoryInfo(Path.Combine(baseRootPath, "Audio","audio_chunks"));
            foreach (FileInfo file in dire.GetFiles())
            {
                file.Delete();
            }

            return Ok(metadata); 
        }


       
        private async Task<IActionResult> ProcessDetection(string filePath)
        {
            var swTotal = Stopwatch.StartNew();

            Console.WriteLine("Starting audio extraction...");
            var sw = Stopwatch.StartNew();
            var audioPath = Path.Combine(baseRootPath, "Audio", "audio.wav");
            _ffmpeg.ExtractAudio(filePath, audioPath);
            sw.Stop();
            Console.WriteLine($"Audio extraction complete in {sw.Elapsed.TotalSeconds:F2} sec");



            Console.WriteLine("Splitting audio into 15s chunks...");
            sw.Restart();
            var chunks = _ffmpeg.SplitAudio(audioPath, baseRootPath, 15);
            sw.Stop();
            Console.WriteLine($"Audio splitting complete in {sw.Elapsed.TotalSeconds:F2} sec");



            Console.WriteLine("Running YAMNet inference on chunks...");
            sw.Restart();
            var chunkDetections = _yamnet.AnalyzeChunks1(chunks, audioPath);
            sw.Stop();
            Console.WriteLine($"YAMNet inference complete in {sw.Elapsed.TotalSeconds:F2} sec");


            Console.WriteLine("Merging music chunks into segments...");
            sw.Restart();
            var segments = _segmentMerger.MergeMusicChunks(chunkDetections);
            sw.Stop();
            Console.WriteLine($"Chunk merging complete in {sw.Elapsed.TotalSeconds:F2} sec");



            Console.WriteLine("Annotating segments using ACRCloud...");
            sw.Restart();
            var metadata = await _acr.IdentifyBestMatchesForSegmentsAsync(segments, chunks);
            sw.Stop();
            Console.WriteLine($"ACRCloud segment annotation complete in {sw.Elapsed.TotalSeconds:F2} sec");


            swTotal.Stop();
            Console.WriteLine($"Total processing completed in {swTotal.Elapsed.TotalSeconds:F2} sec");

            //Delete files

            System.IO.DirectoryInfo dire = new DirectoryInfo(Path.Combine(baseRootPath, "Audio","audio_chunks"));
            foreach (FileInfo file in dire.GetFiles())
            {
                file.Delete();
            }

            System.IO.DirectoryInfo dire1 = new DirectoryInfo(Path.Combine(baseRootPath, "Audio"));
            foreach (FileInfo file in dire1.GetFiles())
            {
                file.Delete();
            }

            return Ok(metadata);
        }
    }
}