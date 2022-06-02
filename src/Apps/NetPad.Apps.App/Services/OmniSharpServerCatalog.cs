using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetPad.Compilation;
using NetPad.Events;
using NetPad.Scripts;
using NetPad.Utilities;
using OmniSharp;

namespace NetPad.Services;

/// <summary>
/// Manages and keeps a collection of created OmniSharp servers.
/// </summary>
public class OmniSharpServerCatalog
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OmniSharpServerCatalog> _logger;
    private readonly Dictionary<Guid, AppOmniSharpServer> _servers;

    public OmniSharpServerCatalog(IServiceProvider serviceProvider, ILogger<OmniSharpServerCatalog> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _servers = new Dictionary<Guid, AppOmniSharpServer>();
    }

    public AppOmniSharpServer? GetOmniSharpServer(Guid scriptId)
    {
        if (_servers.TryGetValue(scriptId, out var server))
            return server;

        // This can occur if omnisharp server is still initializing/starting and is not yet ready
        return null;
    }

    public async Task StartOmniSharpServerAsync(ScriptEnvironment environment)
    {
        if (_servers.ContainsKey(environment.Script.Id))
        {
            throw new InvalidOperationException($"An OmniSharp server already exists for script ID: {environment.Script.Id}");
        }

        var server = new AppOmniSharpServer(
            environment,
            _serviceProvider.GetRequiredService<IOmniSharpServerFactory>(),
            _serviceProvider.GetRequiredService<ICodeParser>(),
            _serviceProvider.GetRequiredService<IEventBus>(),
            _serviceProvider.GetRequiredService<IConfiguration>(),
            _serviceProvider.GetRequiredService<ILogger<AppOmniSharpServer>>(),
            _serviceProvider.GetRequiredService<ILogger<ScriptProject>>()
        );

        _logger.LogDebug("Initialized a new {Type} for script {Script}",
            nameof(AppOmniSharpServer),
            environment.Script);

        try
        {
            bool started = await server.StartAsync();

            _logger.LogDebug("Attempted to start {Type}. Succeeded: {Success}",
                nameof(AppOmniSharpServer),
                started);

            if (started)
            {
                _servers.Add(environment.Script.Id, server);
                _logger.LogDebug("Added OmniSharp server for script {Script}", environment.Script);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred starting OmniSharp server");
        }
    }

    public async Task StopOmniSharpServerAsync(ScriptEnvironment environment)
    {
        _logger.LogDebug("Finding OmniSharp server to stop for script {Script}", environment.Script);

        // Continuously try to find an OmniSharp server for the script for a few seconds.
        // An OmniSharp server could still be starting and so we want to do multiple checks to ensure
        // we find it if it was slow to start.
        AppOmniSharpServer? server = null;
        int findCounter = 0;

        while (++findCounter <= 10)
        {
            if (_servers.TryGetValue(environment.Script.Id, out server))
            {
                _logger.LogDebug("Found OmniSharp server to stop for script {Script} on attempt {Attempt}",
                    environment.Script,
                    findCounter);
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        if (server == null)
        {
            _logger.LogDebug("No OmniSharp server found for script {Script} after {Attempts} attempts",
                environment.Script,
                findCounter);
            return;
        }

        _servers.Remove(environment.Script.Id);

        try
        {
            await Retry.ExecuteAsync(5, TimeSpan.FromSeconds(1), async () => { await server.StopAsync(); });
            _logger.LogDebug("Stopped OmniSharp server for script {Script}", environment.Script);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping OmniSharp server for script {Script}", environment.Script);
        }
    }
}