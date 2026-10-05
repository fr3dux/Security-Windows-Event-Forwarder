using System.Net;
using Darktrace.WindowsEventForwarder.Configuration;
using Darktrace.WindowsEventForwarder.Events;
using Darktrace.WindowsEventForwarder.Formatting;
using Darktrace.WindowsEventForwarder.Infrastructure;
using Darktrace.WindowsEventForwarder.Queue;
using Darktrace.WindowsEventForwarder.Services;
using Darktrace.WindowsEventForwarder.Transport;

var paths = new AgentPaths();
paths.EnsureDirectories();

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "agentsettings.json"), optional: false, reloadOnChange: false)
    .AddJsonFile(paths.Configuration, optional: true, reloadOnChange: true)
    .AddEnvironmentVariables("DTEF_");

builder.Services.AddWindowsService(options => options.ServiceName = "Darktrace Event Forwarder");
builder.Logging.AddProvider(new FileLoggerProvider(paths.Log));

builder.Services.AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .Validate(options => IPAddress.TryParse(options.SourceAddress, out _), "Agent:SourceAddress must be an IP address.")
    .Validate(options => options.SourceAddressMode.Equals("Configured", StringComparison.OrdinalIgnoreCase)
                         || options.SourceAddressMode.Equals("ResolveEventComputer", StringComparison.OrdinalIgnoreCase),
        "Agent:SourceAddressMode must be Configured or ResolveEventComputer.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Channel), "Agent:Channel is required.")
    .Validate(options => options.EventIds.Length > 0, "Agent:EventIds must not be empty.")
    .ValidateOnStart();
builder.Services.AddOptions<SyslogOptions>()
    .Bind(builder.Configuration.GetSection(SyslogOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Syslog:Host is required.")
    .Validate(options => options.Port is > 0 and <= 65535, "Syslog:Port must be valid.")
    .Validate(options => options.Protocol.Equals("Tcp", StringComparison.OrdinalIgnoreCase)
                         || options.Protocol.Equals("Udp", StringComparison.OrdinalIgnoreCase),
        "Syslog:Protocol must be Tcp or Udp.")
    .ValidateOnStart();
builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<BookmarkStore>();
#pragma warning disable CA1416 // This executable is published and installed only for Windows.
builder.Services.AddSingleton<IWindowsEventSource, WindowsEventSource>();
#pragma warning restore CA1416
builder.Services.AddSingleton<DarktraceMessageFormatter>();
builder.Services.AddSingleton<ISourceAddressResolver, SourceAddressResolver>();
builder.Services.AddSingleton<IMessageQueue, DiskMessageQueue>();
builder.Services.AddSingleton<ISyslogSender, SyslogSender>();
builder.Services.AddHostedService<EventCollectorService>();
builder.Services.AddHostedService<QueueSenderService>();

await builder.Build().RunAsync();
