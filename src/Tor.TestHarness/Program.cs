using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tor;
using Tor.Config;

namespace Tor.TestHarness
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 1. Setup Dependency Injection container
            var services = new ServiceCollection();

            services.AddLogging(configure =>
            {
                configure.AddConsole();
                configure.SetMinimumLevel(LogLevel.Debug);
            });

            // 2. Configure Tor Client settings
            string torPath = "/usr/bin/tor";
            if (!File.Exists(torPath))
            {
                torPath = "tor";
            }

            // Use non-conflicting ports for the test harness (Socks: 9250, Control: 9251)
            var createParams = new ClientCreateParams(torPath, 9251);
            createParams.SetConfig(ConfigurationNames.SocksPort, 9250);

            // Configure custom data directory to avoid locks
            string dataDir = Path.Combine(Path.GetTempPath(), "tor-data-9250");
            if (Directory.Exists(dataDir))
            {
                try { Directory.Delete(dataDir, true); } catch { }
            }
            Directory.CreateDirectory(dataDir);
            createParams.SetConfig(ConfigurationNames.DataDirectory, dataDir);

            services.AddTorClient(createParams);

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Starting Tor client through dependency injection...");

            // 3. Resolve the client (this boots up the Tor process)
            var client = serviceProvider.GetRequiredService<Client>();

            logger.LogInformation("Waiting 10 seconds for Tor to bootstrap...");
            await Task.Delay(10000);

            // 4. Create HttpClient configured with Tor SOCKS5 proxy
            var handler = new HttpClientHandler
            {
                Proxy = new WebProxy("socks5://127.0.0.1:9250")
            };

            using (var httpClient = new HttpClient(handler))
            {
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                string? html = null;
                string searchUrl = "https://www.google.com/search?tbm=isch&q=antigravity+space";
                logger.LogInformation($"Sending request to Google Image Search through Tor SOCKS proxy: {searchUrl}");

                try
                {
                    html = await httpClient.GetStringAsync(searchUrl);
                    logger.LogInformation("Received HTML response successfully from Google.");
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests || ex.StatusCode == HttpStatusCode.Forbidden)
                {
                    logger.LogWarning($"Google returned HTTP {ex.StatusCode} (rate limit/block). Falling back to Wikipedia Tor network page...");
                    searchUrl = "https://en.wikipedia.org/wiki/Tor_(network)";
                    try
                    {
                        html = await httpClient.GetStringAsync(searchUrl);
                        logger.LogInformation("Received HTML response successfully from Wikipedia.");
                    }
                    catch (Exception fallbackEx)
                    {
                        logger.LogError(fallbackEx, "Failed to fetch fallback Wikipedia page.");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fetch search page from Google.");
                }

                if (html != null)
                {
                    logger.LogInformation("Parsing image URLs from the HTML source...");

                    // Extract image src URLs from the page
                    var matches = Regex.Matches(html, @"<img[^>]+src=""([^""]+)""");
                    logger.LogInformation($"Found {matches.Count} image tags in the page source.");

                    int count = 0;
                    int successCount = 0;
                    foreach (Match match in matches)
                    {
                        var imgUrl = match.Groups[1].Value;

                        // Normalize Wikipedia's protocol-relative URLs
                        if (imgUrl.StartsWith("//"))
                        {
                            imgUrl = "https:" + imgUrl;
                        }

                        if (!imgUrl.StartsWith("http"))
                            continue;

                        logger.LogInformation($"Found image URL: {imgUrl}");
                        count++;

                        try
                        {
                            logger.LogInformation($"Downloading image {count}: {imgUrl}");
                            var imgBytes = await httpClient.GetByteArrayAsync(imgUrl);
                            logger.LogInformation($"Successfully downloaded {imgBytes.Length} bytes in memory.");
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning($"Failed to download image {count}: {ex.Message}");
                        }

                        // Add a small delay between requests to avoid rate limits
                        await Task.Delay(500);
                    }

                    logger.LogInformation($"Successfully downloaded {successCount} of {count} images in memory.");
                }
            }

            logger.LogInformation("Stopping Tor client and cleaning up...");
            client.Dispose();
            logger.LogInformation("Test harness run complete.");
        }
    }
}
