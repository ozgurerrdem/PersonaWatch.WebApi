using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace PersonaWatch.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ToolsController : ControllerBase
{
    private readonly IConfiguration _cfg;

    public ToolsController(IConfiguration cfg)
    {
        _cfg = cfg;
    }

    // GET /api/tools/youtube/clip?videoId=...&start=330&end=390&title=Optional
    [HttpGet("youtube/clip")]
    public async Task<IActionResult> Clip(
        [FromQuery] string videoId,
        [FromQuery] int start,
        [FromQuery] int end,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(videoId))
            return BadRequest("videoId gereklidir.");
        if (start < 0 || end <= 0 || end <= start)
            return BadRequest("Geçersiz zaman aralığı.");

        var max = _cfg.GetValue<int?>("Tools:MaxClipSeconds") ?? 300;
        var duration = end - start;
        if (duration > max) return BadRequest($"Maksimum süre {max} sn.");

        // ffmpeg / yt-dlp pathlerini bul (env veya PATH)
        var ffmpegPath = FindTool("FFMPEG_PATH", "ffmpeg");
        var ytdlpPath = FindTool("YTDLP_PATH", "yt-dlp");
        if (string.IsNullOrWhiteSpace(ffmpegPath))
            return ToolMissingProblem("ffmpeg", "FFMPEG_PATH");

        // 1) Stream URL'lerini bul: önce YoutubeExplode (custom UA), olmazsa yt-dlp
        (string? videoUrl, string? audioUrl) = await TryResolveStreamsAsync(videoId, ytdlpPath, ct);

        if (videoUrl is null)
            return StatusCode(502, "Video akış URL'si alınamadı.");

        var safeName = MakeSafeFileName($"{DateTime.Now.Ticks}_{start}-{end}.mp4");

        try
        {
            // ffmpeg argümanları (muxed tek input, adaptif iki input)
            string args;
            if (audioUrl == null)
            {
                // Sadece video varsa
                args = $"-ss {start} -i \"{videoUrl}\" -t {duration} -c:v libx264 -c:a aac -preset ultrafast -f mp4 -movflags frag_keyframe+empty_moov pipe:1 -loglevel error";
            }
            else
            {
                // Video ve audio ayrı streamler varsa
                args = $"-ss {start} -i \"{videoUrl}\" -ss {start} -i \"{audioUrl}\" -t {duration} -c:v libx264 -c:a aac -preset ultrafast -map 0:v:0 -map 1:a:0 -f mp4 -movflags frag_keyframe+empty_moov pipe:1 -loglevel error";
            }

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath!,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

            proc.Start();

            // stderr'i oku ve logla
            var errorTask = Task.Run(async () =>
            {
                try
                {
                    var errorOutput = await proc.StandardError.ReadToEndAsync();
                }
                catch (Exception)
                {
                    
                }
            }, ct);

            Response.StatusCode = 200;
            Response.ContentType = "video/mp4";
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{safeName}\"";
            Response.Headers["Cache-Control"] = "no-store";

            // stdout → HTTP response (diske yazmadan)
            await proc.StandardOutput.BaseStream.CopyToAsync(Response.Body, 81920, ct);
            await Response.Body.FlushAsync(ct);

            // Process'in bitmesini bekle
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
            {
                return Problem("FFmpeg işlem hatası.");
            }

            await errorTask; // Hata okuma task'ını bekle

            return new EmptyResult();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Problem($"İşlem başarısız: {ex.Message}");
        }
    }

    // --- Yardımcılar ---

    // YoutubeExplode (custom UA) → olmazsa yt-dlp fallback
    private async Task<(string? videoUrl, string? audioUrl)> TryResolveStreamsAsync(string videoId, string? ytdlpPath, CancellationToken ct)
    {
        // 1) yt-dlp önce dene (daha güvenilir)
        if (!string.IsNullOrWhiteSpace(ytdlpPath))
        {
            var (v, a) = await ResolveWithYtDlpAsync(videoId, ytdlpPath!, ct);
            if (v != null)
            {
                return (v, a);
            }
        }

        // 2) YoutubeExplode'u desktop UA ile dene
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.Clear();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(Windows NT 10.0; Win64; x64)"));
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AppleWebKit", "537.36"));
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(KHTML, like Gecko)"));
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Chrome", "124.0.0.0"));
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Safari", "537.36"));

            var yt = new YoutubeClient(http);
            var manifest = await yt.Videos.Streams.GetManifestAsync(new VideoId(videoId), ct);

            // Önce muxed stream dene
            var muxed = manifest.GetMuxedStreams()
                .Where(s => s.Container == Container.Mp4)
                .GetWithHighestVideoQuality();
            
            if (muxed != null)
            {
                return (muxed.Url, null);
            }

            // Adaptif stream'ler
            var bestVideo = manifest.GetVideoStreams()
                .Where(s => s.Container == Container.Mp4)
                .GetWithHighestVideoQuality();
            var bestAudio = manifest.GetAudioStreams()
                .Where(s => s.Container == Container.Mp4)
                .GetWithHighestBitrate();
            
            if (bestVideo != null && bestAudio != null)
            {
                return (bestVideo.Url, bestAudio.Url);
            }

            // Son çare olarak herhangi bir video stream
            var anyVideo = manifest.GetVideoStreams().GetWithHighestVideoQuality();
            if (anyVideo != null)
            {
                return (anyVideo.Url, null);
            }
        }
        catch (Exception)
        {
        }

        return (null, null);
    }

    // yt-dlp -g ile URL'leri al
    private async Task<(string? videoUrl, string? audioUrl)> ResolveWithYtDlpAsync(
        string videoId, string ytdlpPath, CancellationToken ct)
    {
        var url = $"https://www.youtube.com/watch?v={videoId}";
        
        // Önce best video+audio formatını dene
        var psi = new ProcessStartInfo
        {
            FileName = ytdlpPath,
            Arguments = $"-f \"best[height<=720]\" -g \"{url}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var p = new Process { StartInfo = psi };
            p.Start();
            
            var stdout = await p.StandardOutput.ReadToEndAsync();
            var stderr = await p.StandardError.ReadToEndAsync();
            
            await p.WaitForExitAsync(ct);

            if (p.ExitCode == 0 && !string.IsNullOrEmpty(stdout))
            {
                var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 1)
                {
                    return (lines[0], null);
                }
                if (lines.Length >= 2)
                {
                    return (lines[0], lines[1]);
                }
            }
        }
        catch (Exception)
        {
        }

        return (null, null);
    }

    private static string? FindTool(string envVarName, string defaultName)
    {
        var explicitPath = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrWhiteSpace(explicitPath) && System.IO.File.Exists(explicitPath))
            return explicitPath;

        try
        {
            var which = OperatingSystem.IsWindows() ? "where" : "which";
            var psi = new ProcessStartInfo
            {
                FileName = which,
                Arguments = defaultName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var p = new Process { StartInfo = psi };
            p.Start();
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            var path = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path)) return path;
        }
        catch { }

        return defaultName;
    }

    private ObjectResult ToolMissingProblem(string tool, string envVar)
        => new(new
        {
            error = $"{tool} bulunamadı ya da çalıştırılamadı.",
            fix = $"{tool} kurulu olmalı ve PATH'te görünmeli. İstersen {envVar} ortam değişkeni ile mutlak yolu ver."
        })
        { StatusCode = 500 };

    private static string MakeSafeFileName(string name)
    {
        var invalids = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}