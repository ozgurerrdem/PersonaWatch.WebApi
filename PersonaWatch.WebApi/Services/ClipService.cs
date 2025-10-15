using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using PersonaWatch.WebApi.Services.Interfaces;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace PersonaWatch.WebApi.Services;

public class ClipService : IClipService
{
    public async Task<IActionResult> ClipAsync(
        HttpResponse response,
        string videoId,
        int start,
        int end,
        CancellationToken ct = default)
    {
        var ffmpegPath = FindTool("FFMPEG_PATH", "ffmpeg");
        var ytdlpPath  = FindTool("YTDLP_PATH", "yt-dlp");
        if (string.IsNullOrWhiteSpace(ffmpegPath))
            return ToolMissingProblem("ffmpeg", "FFMPEG_PATH");

        var (videoUrl, audioUrl) = await TryResolveStreamsAsync(videoId, ytdlpPath, ct);
        if (videoUrl is null)
            return Problem("Video akış URL'si alınamadı.", 502);

        var safeName = MakeSafeFileName($"{DateTime.Now.Ticks}_{start}-{end}.mp4");

        try
        {
            // 1) yt-dlp varsa: download-sections + force-keyframes-at-cuts → en tutarlı sonuç
            if (!string.IsNullOrWhiteSpace(ytdlpPath))
            {
                var tmpFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
                try
                {
                    var ytdlpArgs =
                        $"-f \"bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]\" " +
                        $"--download-sections \"*{start}-{end}\" " +
                        $"--force-keyframes-at-cuts " +
                        $"--retries infinite --fragment-retries infinite " +
                        $"--no-part --no-continue -o \"{tmpFile}\" " +
                        $"\"https://www.youtube.com/watch?v={videoId}\"";

                    var p1 = new ProcessStartInfo
                    {
                        FileName = ytdlpPath!,
                        Arguments = ytdlpArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true
                    };

                    using (var proc = new Process { StartInfo = p1 })
                    {
                        proc.Start();
                        // STDERR/STDOUT’u okuyalım ki buffer dolup bloklamasın
                        var _ = proc.StandardError.ReadToEndAsync();
                        var __ = proc.StandardOutput.ReadToEndAsync();

                        await proc.WaitForExitAsync(ct);
                        if (proc.ExitCode != 0 || !File.Exists(tmpFile))
                            throw new Exception("yt-dlp kesim başarısız.");
                    }

                    response.StatusCode = 200;
                    response.ContentType = "video/mp4";
                    response.Headers["Content-Disposition"] = $"attachment; filename=\"{safeName}\"";
                    response.Headers["Cache-Control"] = "no-store";

                    await using (var fs = File.OpenRead(tmpFile))
                    {
                        await fs.CopyToAsync(response.Body, ct);
                    }
                    await response.Body.FlushAsync(ct);
                    return new EmptyResult();
                }
                finally
                {
                    try { if (File.Exists(tmpFile)) File.Delete(tmpFile); } catch { }
                }
            }

            // 2) FFmpeg ile doğrudan (HLS/DASH’te -ss mutlaka input SONRASINDA)
            var isHlsOrDash = videoUrl.Contains("m3u8", StringComparison.OrdinalIgnoreCase) ||
                              videoUrl.Contains("dash",  StringComparison.OrdinalIgnoreCase);

            string args;
            if (audioUrl == null)
            {
                // Muxed (tek URL)
                args =
                    $"-i \"{videoUrl}\" " +
                    (isHlsOrDash ? "-seek_timestamp 1 " : string.Empty) +
                    $"-ss {start} -t {end - start} " +
                    // Kare hassasiyet için filtre (bazı akışlarda -ss tek başına yetmeyebilir)
                    $"-filter_complex \"[0:v]trim=start=0:end={end - start},setpts=PTS-STARTPTS[v];" +
                    $"[0:a]atrim=start=0:end={end - start},asetpts=PTS-STARTPTS[a]\" " +
                    "-map \"[v]\" -map \"[a]\" " +
                    "-c:v libx264 -c:a aac -preset veryfast -shortest " +
                    "-movflags +faststart " +
                    "-avoid_negative_ts make_zero -reset_timestamps 1 " +
                    "-loglevel error -f mp4 pipe:1";
            }
            else
            {
                // Adaptif (ayrı video + audio)
                args =
                    $"-i \"{videoUrl}\" -i \"{audioUrl}\" " +
                    (isHlsOrDash ? "-seek_timestamp 1 " : string.Empty) +
                    $"-ss {start} -t {end - start} " +
                    $"-filter_complex \"[0:v]trim=start=0:end={end - start},setpts=PTS-STARTPTS[v];" +
                    $"[1:a]atrim=start=0:end={end - start},asetpts=PTS-STARTPTS[a]\" " +
                    "-map \"[v]\" -map \"[a]\" " +
                    "-c:v libx264 -c:a aac -preset veryfast -shortest " +
                    "-movflags +faststart " +
                    "-avoid_negative_ts make_zero -reset_timestamps 1 " +
                    "-loglevel error -f mp4 pipe:1";
            }

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath!,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            using var ff = new Process { StartInfo = psi };
            ff.Start();

            response.StatusCode = 200;
            response.ContentType = "video/mp4";
            response.Headers["Content-Disposition"] = $"attachment; filename=\"{safeName}\"";
            response.Headers["Cache-Control"] = "no-store";

            // STDERR’i okuyalım (buffer dolmasın)
            var errTask = ff.StandardError.ReadToEndAsync();

            await ff.StandardOutput.BaseStream.CopyToAsync(response.Body, 81920, ct);
            await response.Body.FlushAsync(ct);

            await ff.WaitForExitAsync(ct);
            _ = await errTask;

            if (ff.ExitCode != 0)
                return Problem("FFmpeg işlem hatası.");

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

    // ---- Yardımcılar --------------------------------------------------------

    private async Task<(string? videoUrl, string? audioUrl)> TryResolveStreamsAsync(
        string videoId, string? ytdlpPath, CancellationToken ct)
    {
        // 1) yt-dlp (muxed üretirse en iyi)
        if (!string.IsNullOrWhiteSpace(ytdlpPath))
        {
            var (v, a) = await ResolveWithYtDlpAsync(videoId, ytdlpPath!, ct);
            if (v != null) return (v, a);
        }

        // 2) YoutubeExplode fallback
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

            // MP4 muxed varsa onu seç
            var muxed = manifest.GetMuxedStreams()
                .Where(s => s.Container == Container.Mp4)
                .GetWithHighestVideoQuality();
            if (muxed != null)
                return (muxed.Url, null);

            // Adaptif fallback
            var bestVideo = manifest.GetVideoStreams()
                .Where(s => s.Container == Container.Mp4)
                .GetWithHighestVideoQuality();
            var bestAudio = manifest.GetAudioStreams()
                .Where(s => s.Container == Container.Mp4)
                .GetWithHighestBitrate();

            if (bestVideo != null && bestAudio != null)
                return (bestVideo.Url, bestAudio.Url);

            var anyVideo = manifest.GetVideoStreams().GetWithHighestVideoQuality();
            if (anyVideo != null)
                return (anyVideo.Url, null);
        }
        catch { }

        return (null, null);
    }

    private async Task<(string? videoUrl, string? audioUrl)> ResolveWithYtDlpAsync(
        string videoId, string ytdlpPath, CancellationToken ct)
    {
        var url = $"https://www.youtube.com/watch?v={videoId}";

        var psi = new ProcessStartInfo
        {
            FileName = ytdlpPath,
            Arguments = $"-f \"best[height<=720]\" -g \"{url}\"",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        try
        {
            using var p = new Process { StartInfo = psi };
            p.Start();

            var stdout = await p.StandardOutput.ReadToEndAsync();
            var _      = await p.StandardError.ReadToEndAsync();

            await p.WaitForExitAsync(ct);

            if (p.ExitCode == 0 && !string.IsNullOrEmpty(stdout))
            {
                var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 1) return (lines[0], null);
                if (lines.Length >= 2) return (lines[0], lines[1]);
            }
        }
        catch { }

        return (null, null);
    }

    private static string? FindTool(string envVarName, string defaultName)
    {
        var explicitPath = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        try
        {
            var which = OperatingSystem.IsWindows() ? "where" : "which";
            var psi = new ProcessStartInfo
            {
                FileName  = which,
                Arguments = defaultName,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false
            };
            using var p = new Process { StartInfo = psi };
            p.Start();
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            var path = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
        }
        catch { }

        return defaultName;
    }

    private static ObjectResult ToolMissingProblem(string tool, string envVar)
        => new(new
        {
            error = $"{tool} bulunamadı ya da çalıştırılamadı.",
            fix   = $"{tool} kurulu olmalı ve PATH'te görünmeli. İstersen {envVar} ortam değişkeni ile mutlak yolu ver."
        })
        { StatusCode = 500 };

    private static ObjectResult Problem(string message, int status = 500)
        => new(new { error = message }) { StatusCode = status };

    private static string MakeSafeFileName(string name)
    {
        var invalids = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
