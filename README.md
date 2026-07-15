# Tor.NET
A managed library to use the Tor network for SOCKS5 communications. All credits go to Chris Copeland. Originally posted on [CodeProject](http://www.codeproject.com/Articles/1072864/Tor-NET-A-managed-Tor-network-library).

> [!NOTE]
> This library has been ported to **.NET 10 on Linux**, updated to support Microsoft Dependency Injection (`IServiceCollection`) and Logging (`ILogger`), and all legacy Windows-only binaries and controls have been cleaned out.

# What Tor.NET Provides

### 1. Programmatic Tor Process Lifecycle Control
Tor.NET can automatically launch, monitor, and clean up local native `tor` daemon processes from C# on both Linux and Windows, or connect to a remote instance.

* **Guaranteed Process Cleanup (`TAKEOWNERSHIP`)**: If the parent C# application crashes or is forcefully terminated (e.g. `kill -9`), the operating system automatically closes the control connection. The Tor daemon detects this and immediately shuts down, preventing orphaned processes, port conflicts, and directory locks.

```csharp
// Configure and launch local Tor process
string torPath = "/usr/bin/tor"; // On Windows: @"C:\Tools\tor\tor.exe"
ClientCreateParams createParams = new ClientCreateParams(torPath, 9151); // Control Port
createParams.SetConfig(ConfigurationNames.SocksPort, 9150);              // SOCKS Port

using (Client client = Client.Create(createParams))
{
    // Process is started and running here
    Console.WriteLine($"Tor is running: {client.IsRunning}");
} // Automatically disposes, kills the daemon process, and cleans up resources
```

### 2. Tor Control Port Protocol Integration
It wraps the Tor Control Port protocol, enabling you to rotate identity, control circuits, and subscribe to real-time events.

* **Force IP Rotation (New Identity)**:
```csharp
// Force Tor to build new circuits and rotate your exit IP address
client.Controller.CleanCircuits();
```

* **Inspect and Close Circuits / Streams**:
```csharp
// List all active routing circuits
foreach (Circuit circuit in client.Status.Circuits)
{
    Console.WriteLine($"Circuit ID: {circuit.ID}, Status: {circuit.Status}");
}

// Close an active circuit
client.Controller.CloseCircuit(someCircuit);
```

* **Subscribe to Real-Time Network Events**:
```csharp
// Monitor download and upload speeds in real-time
client.Status.BandwidthChanged += (sender, e) =>
{
    Console.WriteLine($"Download: {e.DownloadRate}/s, Upload: {e.UploadRate}/s");
};

// Monitor circuit state changes (e.g., when a circuit is built or closed)
client.Status.CircuitsChanged += (sender, e) =>
{
    Console.WriteLine("Routing circuits updated.");
};
```

### 3. Traffic Routing (SOCKS5 & TCP Streams)
You can route HTTP/HTTPS web requests or establish raw TCP socket streams over the Tor network.

* **Routing HttpClient Web Requests**:
```csharp
using System.Net;
using System.Net.Http;

var handler = new HttpClientHandler
{
    Proxy = new WebProxy("socks5://127.0.0.1:9150")
};

using (var httpClient = new HttpClient(handler))
{
    var myIp = await httpClient.GetStringAsync("https://icanhazip.com");
    Console.WriteLine($"Public IP routed through Tor: {myIp.Trim()}");
}
```

* **Routing Raw TCP socket streams**:
```csharp
// Establish a raw TCP stream through Tor to a target host and port
using (Stream stream = client.GetStream("example.com", 80))
{
    byte[] request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n");
    await stream.WriteAsync(request, 0, request.Length);
    
    byte[] response = new byte[1024];
    int read = await stream.ReadAsync(response, 0, response.Length);
    Console.WriteLine(Encoding.ASCII.GetString(response, 0, read));
}
```

### 4. Modern .NET Ecosystem Integration
Includes native integration with Microsoft dependency injection container (`IServiceCollection`) and logger abstraction (`ILogger`).

```csharp
var services = new ServiceCollection();

// Pipes all internal Tor daemon logs directly to ILogger
services.AddLogging(configure => configure.AddConsole());

var createParams = new ClientCreateParams("/usr/bin/tor", 9151);
createParams.SetConfig(ConfigurationNames.SocksPort, 9150);

// Register the Tor Client
services.AddTorClient(createParams);

var serviceProvider = services.BuildServiceProvider();

// Resolve and start the client
var client = serviceProvider.GetRequiredService<Client>();
```

# Installation and Usage (Linux)

1. Install the native Tor package and GeoIP databases on your Linux system:
   ```bash
   sudo apt-get update && sudo apt-get install -y tor
   ```
2. Make sure Tor is not running as a system daemon (so it does not bind to your local ports 9050/9051):
   ```bash
   # Linux systems without systemd can keep it disabled.
   # If systemd is present:
   sudo systemctl stop tor
   sudo systemctl disable tor
   ```
3. Add the project reference to your application.

# Code Samples

## 1. Setup Dependency Injection (IOC) & Logging

You can register the Tor Client using `AddTorClient` in your `ServiceCollection`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tor;
using Tor.Config;

var services = new ServiceCollection();

// Setup Logging
services.AddLogging(configure =>
{
    configure.AddConsole();
    configure.SetMinimumLevel(LogLevel.Debug);
});

// Configure Tor Parameters
var createParams = new ClientCreateParams("/usr/bin/tor", 9151); // Control Port
createParams.SetConfig(ConfigurationNames.SocksPort, 9150);      // SOCKS Port

services.AddTorClient(createParams);

var serviceProvider = services.BuildServiceProvider();

// Resolve the client (this automatically starts the Tor process and routes logs to ILogger)
var client = serviceProvider.GetRequiredService<Client>();
```

## 2. Using SOCKS5 Proxy natively in HttpClient (.NET 6+)

Modern .NET HttpClient natively supports SOCKS5 proxy schemes:

```csharp
using System.Net;
using System.Net.Http;

var handler = new HttpClientHandler
{
    Proxy = new WebProxy("socks5://127.0.0.1:9150")
};

using (var httpClient = new HttpClient(handler))
{
    var ipInfo = await httpClient.GetStringAsync("https://icanhazip.com");
    Console.WriteLine($"Your Tor IP: {ipInfo.Trim()}");
}
```

## 3. Using legacy SOCKS5 Stream

```csharp
using (Stream stream = client.GetStream("12.34.56.789", 1234))
{
    stream.Write(...);
    stream.Read(...);
}
```

# Projects in the Repository

The codebase is split into the following subdirectories:

1. **[Tor](file:///var/home/maxfridbe/Dev/vibecoding/Tor.NET/src/Tor)**: The core Tor.NET library (SDK-style targeting `net10.0`).
2. **[Tor.Tests](file:///var/home/maxfridbe/Dev/vibecoding/Tor.NET/src/Tor.Tests)**: Unit test project using xUnit (targeting `net10.0`).
3. **[Tor.TestHarness](file:///var/home/maxfridbe/Dev/vibecoding/Tor.NET/src/Tor.TestHarness)**: A console test harness showing Dependency Injection, Logging, and fetching Google image search results through the Tor network.

---

### Running Unit Tests

Run the xUnit tests with:
```bash
dotnet test src/Tor.Tests/Tor.Tests.csproj
```

### Running the Test Harness

Run the test harness application with:
```bash
dotnet run --project src/Tor.TestHarness/Tor.TestHarness.csproj
```