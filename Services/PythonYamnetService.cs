using System.Diagnostics;
using System.Text.Json;
using YamnetSolutionAPI.Models;

namespace YamnetSolutionAPI.Services;

public class PythonYamnetService
{
    private readonly string _pythonScriptPath = "yamnet_batch_infer.py";

    public List<ChunkDetection> AnalyzeChunks(List<string> chunkPaths,string audioDir)
    {
        var results = new List<ChunkDetection>();

        if (chunkPaths == null || chunkPaths.Count == 0)
            return results;

        //string args = $"{_pythonScriptPath} " + string.Join(" ", chunkPaths.Select(p => $"\"{p}\""));

        string args = $"\"{_pythonScriptPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = "yamnet_batch_infer1.py",
            WorkingDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,@"..\..\..")),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(error))
                Console.WriteLine("Python stderr: " + error);

            if (!string.IsNullOrWhiteSpace(output))
            {
                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var rawResults = JsonSerializer.Deserialize<List<RawDetectionResult>>(output, jsonOptions);

                if (rawResults != null)
                {
                    foreach (var raw in rawResults)
                    {
                        if (!string.IsNullOrWhiteSpace(raw.Error))
                        {
                            Console.WriteLine($"Chunk {raw.ChunkPath} error: {raw.Error}");
                            continue;
                        }

                        results.Add(new ChunkDetection
                        {
                            ChunkIndex = GetChunkIndexFromPath(raw.ChunkPath),
                            Labels = raw.Labels ?? new(),
                            Scores = raw.Scores ?? new()
                        });
                    }

                    results = results.OrderBy(r => r.ChunkIndex).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Batch oooooooo YamNet execution error: " + ex.Message);
        }

        return results;
    }

    public List<ChunkDetection> AnalyzeChunks1(List<string> chunkPaths,string audioDir)
    {
        var results = new List<ChunkDetection>();

        if (chunkPaths == null || chunkPaths.Count == 0)
            return results;

        //string args = $"{_pythonScriptPath} " + string.Join(" ", chunkPaths.Select(p => $"\"{p}\""));
        List<string> outputlines = new List<string>();
        string args = $"\"{_pythonScriptPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = "yamnet_batch_infer1.py",
            WorkingDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,@"..\..\..")),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };



        // try
        // {

        using (var process = new Process())
        {
            process.StartInfo = psi;
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    
                    outputlines.Add(e.Data);
                    Console.WriteLine($"[PYTHON OUT]: {e.Data}");
                }
                ;
               
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                    if (e.Data != null)
                        Console.Error.WriteLine($"[PYTHON ERR]: {e.Data}");
             };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }

        string output = string.Join("", outputlines);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var rawResults = JsonSerializer.Deserialize<List<RawDetectionResult>>(output, jsonOptions);
       
             if (rawResults != null)
                {
                    
                    foreach (var raw in rawResults)
            {
                if (!string.IsNullOrWhiteSpace(raw.Error))
                {
                    Console.WriteLine($"Chunk {raw.ChunkPath} error: {raw.Error}");
                    continue;
                }

                results.Add(new ChunkDetection
                {
                    ChunkIndex = GetChunkIndexFromPath(raw.ChunkPath),
                    Labels = raw.Labels ?? new(),
                    Scores = raw.Scores ?? new()
                });
            }

                    results = results.OrderBy(r => r.ChunkIndex).ToList();
                }
      
            // if (!string.IsNullOrWhiteSpace(error))
        //     Console.WriteLine("Python stderr: " + error);

        // if (!string.IsNullOrWhiteSpace(output))
        // {
        //     var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        //     var rawResults = JsonSerializer.Deserialize<List<RawDetectionResult>>(output, jsonOptions);

        //     if (rawResults != null)
        //     {
        //         foreach (var raw in rawResults)
        //         {
        //             if (!string.IsNullOrWhiteSpace(raw.Error))
        //             {
        //                 Console.WriteLine($"Chunk {raw.ChunkPath} error: {raw.Error}");
        //                 continue;
        //             }

        //             results.Add(new ChunkDetection
        //             {
        //                 ChunkIndex = GetChunkIndexFromPath(raw.ChunkPath),
        //                 Labels = raw.Labels ?? new(),
        //                 Scores = raw.Scores ?? new()
        //             });
        //         }

        //         results = results.OrderBy(r => r.ChunkIndex).ToList();
        //     }
        // }
        // }
        // catch (Exception ex)
        // {
        //     Console.WriteLine("Batch YamNet execution error: " + ex.Message);
        // }

        return results;
    }

    private int GetChunkIndexFromPath(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var parts = name.Split('_');
        return int.TryParse(parts.Last(), out var index) ? index : -1;
    }

}
