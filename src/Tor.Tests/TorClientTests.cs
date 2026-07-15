using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Tor;
using Tor.Config;
using Xunit;
using Xunit.Abstractions;

namespace Tor.Tests
{
    public class TorClientTests
    {
        private readonly ITestOutputHelper output;

        public TorClientTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void TestTorClientInitialization()
        {
            string torPath = "/usr/bin/tor";
            if (!File.Exists(torPath))
            {
                torPath = "tor";
            }

            ClientCreateParams createParams = new ClientCreateParams(torPath, 9151);
            createParams.SetConfig(ConfigurationNames.SocksPort, 9150);

            // Configure custom data directory to avoid locks
            string dataDir = Path.Combine(Path.GetTempPath(), "tor-data-9150");
            if (Directory.Exists(dataDir))
            {
                try { Directory.Delete(dataDir, true); } catch { }
            }
            Directory.CreateDirectory(dataDir);
            createParams.SetConfig(ConfigurationNames.DataDirectory, dataDir);

            using (Client client = Client.Create(createParams))
            {
                Assert.NotNull(client);
                Assert.True(client.IsRunning);
                Assert.NotNull(client.Configuration);
                Assert.NotNull(client.Status);

                // Wait for the asynchronous configuration fetch to complete
                Thread.Sleep(2000);

                output.WriteLine($"Configuration ControlPort: {client.Configuration.ControlPort}");
                output.WriteLine($"Configuration SocksPort: {client.Configuration.SocksPort}");

                Assert.Equal(9151, client.Configuration.ControlPort);
                Assert.Equal(9150, client.Configuration.SocksPort);
            }
        }

        [Fact]
        public void TestTorClientDependencyInjection()
        {
            var services = new ServiceCollection();

            string torPath = "/usr/bin/tor";
            if (!File.Exists(torPath))
            {
                torPath = "tor";
            }

            // Use ports 9350/9351 for this DI test to avoid conflict
            var createParams = new ClientCreateParams(torPath, 9351);
            createParams.SetConfig(ConfigurationNames.SocksPort, 9350);

            // Configure custom data directory to avoid locks
            string dataDir = Path.Combine(Path.GetTempPath(), "tor-data-9350");
            if (Directory.Exists(dataDir))
            {
                try { Directory.Delete(dataDir, true); } catch { }
            }
            Directory.CreateDirectory(dataDir);
            createParams.SetConfig(ConfigurationNames.DataDirectory, dataDir);

            services.AddTorClient(createParams);

            using (var serviceProvider = services.BuildServiceProvider())
            {
                var client = serviceProvider.GetService<Client>();
                
                Assert.NotNull(client);
                Assert.True(client.IsRunning);

                // Wait for async initialization
                Thread.Sleep(2000);

                Assert.Equal(9351, client.Configuration.ControlPort);
                Assert.Equal(9350, client.Configuration.SocksPort);
            }
        }
    }
}
