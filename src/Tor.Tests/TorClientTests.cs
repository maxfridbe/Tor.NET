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

        [Fact]
        public void TestTorClientTakeOwnershipKillingMechanic()
        {
            string torPath = "/usr/bin/tor";
            if (!File.Exists(torPath))
            {
                torPath = "tor";
            }

            // Use ports 9450/9451 to avoid port conflicts
            ClientCreateParams createParams = new ClientCreateParams(torPath, 9451);
            createParams.SetConfig(ConfigurationNames.SocksPort, 9450);

            string dataDir = Path.Combine(Path.GetTempPath(), "tor-data-9450");
            if (Directory.Exists(dataDir))
            {
                try { Directory.Delete(dataDir, true); } catch { }
            }
            Directory.CreateDirectory(dataDir);
            createParams.SetConfig(ConfigurationNames.DataDirectory, dataDir);

            Client client = Client.Create(createParams);
            Assert.NotNull(client);
            Assert.True(client.IsRunning);

            // Wait for initialization and taking ownership to complete
            Thread.Sleep(2000);

            // Get the child process and the event socket using reflection
            var processField = typeof(Client).GetField("process", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(processField);
            var process = (System.Diagnostics.Process?)processField.GetValue(client);
            Assert.NotNull(process);
            Assert.False(process.HasExited);

            int pid = process.Id;

            var eventsField = typeof(Client).GetField("events", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(eventsField);
            var eventsObject = eventsField.GetValue(client);
            Assert.NotNull(eventsObject);

            var socketField = eventsObject.GetType().GetField("socket", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(socketField);
            var socket = (System.Net.Sockets.Socket?)socketField.GetValue(eventsObject);
            Assert.NotNull(socket);

            // Simulate host crash by manually closing the ownership-holding socket connection
            // without calling process.Kill() or client.Dispose()!
            socket.Close();

            // Wait for the Tor process to detect the socket termination and exit itself
            Thread.Sleep(3000);

            // Check if the OS process exists
            bool processExists = false;
            try
            {
                var osProcess = System.Diagnostics.Process.GetProcessById(pid);
                processExists = !osProcess.HasExited;
            }
            catch (ArgumentException)
            {
                // Process is not running (GetProcessById throws ArgumentException if PID does not exist)
                processExists = false;
            }

            Assert.False(processExists, "The Tor process did not shut down on its own after the control socket closed.");
            Assert.False(client.IsRunning, "The Tor client should no longer be running.");
        }
    }
}
