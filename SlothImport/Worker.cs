using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SlothImport;

public class Worker : BackgroundService
{
    private readonly IHostApplicationLifetime _hostLifetime;
    private readonly IServiceProvider _serviceProvider;
    private int? _exitCode;

    public Worker(IServiceProvider serviceProvider, IHostApplicationLifetime hostLifetime)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(hostLifetime);
        _serviceProvider = serviceProvider;
        _hostLifetime = hostLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Need to create a scope because workers are registered as singletons
        using var scope = _serviceProvider.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<IImporter>();
        try
        {
            await importer.Import(cancellationToken);
            _exitCode = 0;
        }
        catch (OperationCanceledException)
        {
            Log.Information("Operation cancelled");
            _exitCode = -1;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception");
            _exitCode = 1;
        }
        finally
        {
            _hostLifetime.StopApplication();
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Environment.ExitCode = _exitCode.GetValueOrDefault(-1);
        Log.Information("Exiting with code {exitCode}", Environment.ExitCode);
        return Task.CompletedTask;
    }
}