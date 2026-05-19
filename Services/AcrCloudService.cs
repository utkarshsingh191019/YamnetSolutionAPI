using Microsoft.AspNetCore.Http;
using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YamnetSolutionAPI.Models;

namespace YamnetSolutionAPI.Services
{
    public class AcrCloudService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public AcrCloudService(IConfiguration config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;
        }

        public async Task<List<SongMetadata>> IdentifyBestMatchesForSegmentsAsync(
            List<MusicSegment> segments, List<string> allChunks, int chunkDuration = 15)
        {
            var results = new List<SongMetadata>();

            foreach (var segment in segments)
            {
                int startChunk = (int)(segment.Start / chunkDuration);
                int endChunk = (int)(segment.End / chunkDuration);

                SongMetadata bestMatch = null;
                float highestScore = 0;

                for (int i = startChunk; i < endChunk; i++)
                {
                    if (i < 0 || i >= allChunks.Count) continue;
                    var chunkPath = allChunks[i];

                    var result = await IdentifyChunkAsync(chunkPath, i, chunkDuration);
                    if (result != null)
                    {
                        if (result.Score >= 75)
                        {
                            result.StartTime = TimeSpan.FromSeconds(segment.Start);
                            result.EndTime = TimeSpan.FromSeconds(segment.End);
                            results.Add(result);
                            break;
                        }
                        else if (result.Score > highestScore)
                        {
                            bestMatch = result;
                            highestScore = result.Score;
                        }
                    }
                }

                if (bestMatch != null && highestScore > 0 && bestMatch.Score < 75)
                {
                    bestMatch.StartTime = TimeSpan.FromSeconds(segment.Start);
                    bestMatch.EndTime = TimeSpan.FromSeconds(segment.End);
                    results.Add(bestMatch);
                }
            }

            return results;
        }
public async Task<List<SongMetadata>> IdentifyBlobsAsync(List<string> chunkPaths, int chunkDuration = 15)
        {
            var results = new List<SongMetadata>();

            for (int i = 0; i < chunkPaths.Count; i++)
            {
                var result = await IdentifyChunkAsync(chunkPaths[i], i, chunkDuration);
                if (result != null)
                {
                    result.StartTime = TimeSpan.FromSeconds(i * chunkDuration);
                    result.EndTime = result.StartTime + TimeSpan.FromSeconds(chunkDuration);
                    results.Add(result);
                }
            }

            return results;
        }
        private async Task<SongMetadata?> IdentifyChunkAsync(string chunkPath, int index, int duration)
        {
            var settings = _config.GetSection("AcrCloud");
            string host = settings["Host"];
            string accessKey = settings["AccessKey"];
            string accessSecret = settings["AccessSecret"];
            string endpoint = settings["Endpoint"];
            string dataType = settings["DataType"];
            string sigVersion = settings["SignatureVersion"];

            string method = "POST";
            string timestamp = DateTime.Now.Ticks.ToString();
            string stringToSign = method + "\n" + endpoint + "\n" + accessKey + "\n" + dataType + "\n" + sigVersion + "\n" + timestamp; string signature = ComputeSignature(Encoding.ASCII.GetBytes(stringToSign), Encoding.ASCII.GetBytes(accessSecret));

          
            byte[] wavFile = File.ReadAllBytes(chunkPath);
            var formContent = new MultipartFormDataContent();

            formContent.Add(new StringContent(accessKey), "\"access_key\"");
            formContent.Add(new StringContent(timestamp), "\"timestamp\"");
            formContent.Add(new StringContent(signature), "\"signature\"");
            formContent.Add(new StringContent(dataType), "\"data_type\"");
            formContent.Add(new StringContent(sigVersion), "\"signature_version\"");
            formContent.Add(new StringContent(wavFile.Length.ToString()), "\"sample_bytes\"");

            var filestream = new ByteArrayContent(wavFile, 0, wavFile.Length);
            filestream.Headers.Add("Content-Type", "application/octet-stream");
            formContent.Add(filestream, "\"sample\"");

            var response = await _httpClient.PostAsync("https://" + host + "/v1/identify", formContent);
            
            var json = await response.Content.ReadAsStringAsync();
            return ParseMetadata(json, index, duration);
        }

        private static string ComputeSignature(byte[] data, byte[] secret)
        {
            HMACSHA1 hmac = new HMACSHA1(secret);
            byte[] hashedValue = hmac.ComputeHash(data);
            return Convert.ToBase64String(hashedValue, 0, hashedValue.Length);

        }

        private SongMetadata? ParseMetadata(string json, int chunkIndex, int chunkDurationSeconds)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetProperty("status").GetProperty("code").GetInt32() != 0)
                    return null;

                var music = root.GetProperty("metadata").GetProperty("music")[0];

                var title = music.GetProperty("title").GetString();
                var album = music.TryGetProperty("album", out var a) ? a.GetProperty("name").GetString() : "";
                var label = music.TryGetProperty("label", out var l) ? l.GetString() : "";

                var artists = new List<string>();
                if (music.TryGetProperty("artists", out var artistArray))
                {
                    foreach (var artist in artistArray.EnumerateArray())
                        artists.Add(artist.GetProperty("name").GetString());
                }

                float score = music.TryGetProperty("score", out var s) ? s.GetSingle() : 0;

                long beginOffset = music.TryGetProperty("sample_begin_time_offset_ms", out var begin) ? begin.GetInt64() : 0;
                long endOffset = music.TryGetProperty("sample_end_time_offset_ms", out var end) ? end.GetInt64() : chunkDurationSeconds * 1000;

                var startTime = TimeSpan.FromMilliseconds(chunkIndex * chunkDurationSeconds * 1000 + beginOffset);
                var endTime = TimeSpan.FromMilliseconds(chunkIndex * chunkDurationSeconds * 1000 + endOffset);

                var links = new Dictionary<string, string>();

                if (music.TryGetProperty("external_metadata", out var meta))
                {
                    if (meta.TryGetProperty("spotify", out var spotify) &&
                        spotify.TryGetProperty("track", out var track) &&
                        track.TryGetProperty("id", out var id))
                        links["Spotify"] = $"https://open.spotify.com/track/{id.GetString()}";

                    if (meta.TryGetProperty("youtube", out var youtube) &&
                        youtube.TryGetProperty("vid", out var vid))
                        links["YouTube"] = $"https://www.youtube.com/watch?v={vid.GetString()}";

                    if (meta.TryGetProperty("deezer", out var deezer) &&
                        deezer.TryGetProperty("track", out var dtrack) &&
                        dtrack.TryGetProperty("id", out var did))
                        links["Deezer"] = $"https://www.deezer.com/track/{did.GetString()}";
                }

                return new SongMetadata
                {
                    Title = title,
                    Album = album,
                    Label = label,
                    Artists = artists,
                    Score = score,
                    StartTime = startTime,
                    EndTime = endTime,
                    SourceLinks = links
                };
            }
            catch
            {
                return null;
            }
        }
    }
}