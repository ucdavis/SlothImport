using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using SlothApi.Services;
using System.CommandLine;
using SlothImport.Models;
using System.CommandLine.Invocation;

namespace SlothImport;

class Program
{
    private static Option<string> _baseUrlOption = null!;
    private static Option<string> _apiKeyOption = null!;
    private static Option<FileInfo?> _csvFileOption = null!;
    private static Option<bool?> _validateCoAOption = null!;
    private static Option<bool?> _autoApproveOption = null!;

    static async Task<int> Main(string[] args)
    {
        // init args parsing
        _baseUrlOption = new Option<string>("--BaseUrl", "The base url of the sloth api (required)");
        _baseUrlOption.AddAlias("-u");
        _baseUrlOption.AddAlias("-url");
        _apiKeyOption = new Option<string>("--ApiKey", "The api key to use (required)");
        _apiKeyOption.AddAlias("-k");
        _apiKeyOption.AddAlias("-key");
        _csvFileOption = new Option<FileInfo?>("--CsvFile", "The csv file to import (required)");
        _csvFileOption.AddAlias("-f");
        _csvFileOption.AddAlias("-file");
        _validateCoAOption = new Option<bool?>("--ValidateCoA", "Have Sloth perform validation of Chart of Accounts");
        _validateCoAOption.AddAlias("-v");
        _validateCoAOption.AddAlias("-validate");
        _autoApproveOption = new Option<bool?>("--AutoApprove", "Have Sloth auto-approve imported transactions");
        _autoApproveOption.AddAlias("-a");
        _autoApproveOption.AddAlias("-approve");

        var rootCommand = new RootCommand("Imports sloth transactions from a csv file" + Environment.NewLine + Environment.NewLine
        + "All options can be specified via corresponding environment variables prefixed with " + Environment.NewLine
        + "\"SlothImport__\", or via user secrets if in a development environment");
        rootCommand.AddOption(_baseUrlOption);
        rootCommand.AddOption(_apiKeyOption);
        rootCommand.AddOption(_csvFileOption);
        rootCommand.AddOption(_validateCoAOption);
        rootCommand.AddOption(_autoApproveOption);

        // init logging
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithExceptionDetails()
            .Enrich.WithProperty("Application", "SlothImport")
            .WriteTo.Console();
        Log.Logger = loggerConfig.CreateLogger();

        rootCommand.SetHandler(async (context) =>
        {

            var token = context.GetCancellationToken();
            try
            {
                // using IHost to make use of all the DI, ServiceCollection and Configuration goodness
                var host = CreateHostBuilder(context).Build();
                await host.RunAsync(token);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        });

        return await rootCommand.InvokeAsync(args);
    }

    private static IHostBuilder CreateHostBuilder(InvocationContext context)
    {
        return Host.CreateDefaultBuilder().ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            })
            .UseSerilog()
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                if (hostingContext.HostingEnvironment.IsDevelopment())
                {
                    config.AddUserSecrets<Program>();
                }
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddHostedService<Worker>();

                services.AddScoped<IImporter, Importer>();

                // Register ImportOptions, giving cli arguments precedence
                // Not sure if there's a better way. There is a CommandLineConfigurationProvider, but it's too restrictive
                var apiKeyArg = context.ParseResult.GetValueForOption(_apiKeyOption);
                var apiKey = !string.IsNullOrWhiteSpace(apiKeyArg) ? apiKeyArg : ctx.Configuration["SlothImport:ApiKey"] ?? "";
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new Exception("No value supplied for ApiKey");
                }

                var baseUrlArg = context.ParseResult.GetValueForOption(_baseUrlOption);
                var baseUrl = !string.IsNullOrWhiteSpace(baseUrlArg) ? baseUrlArg : ctx.Configuration["SlothImport:BaseUrl"] ?? "";
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    throw new Exception("No value supplied for BaseUrl");
                }

                var csvFileArg = context.ParseResult.GetValueForOption(_csvFileOption);
                var csvFileName = ctx.Configuration["SlothImport:CsvFile"] ?? "";
                var csvFile = csvFileArg != null ? csvFileArg : (string.IsNullOrWhiteSpace(csvFileName) ? null : new FileInfo(csvFileName));
                if (csvFile == null)
                {
                    Log.Information(Directory.GetCurrentDirectory());
                    throw new Exception("No value supplied for CsvFile");
                }

                var validateCoAArg = context.ParseResult.GetValueForOption(_validateCoAOption);
                var validateCoA = validateCoAArg.HasValue ? validateCoAArg.Value : ctx.Configuration.GetValue<bool>("SlothImport:ValidateCoA");

                var autoApproveArg = context.ParseResult.GetValueForOption(_autoApproveOption);
                var autoApprove = autoApproveArg.HasValue ? autoApproveArg.Value : ctx.Configuration.GetValue<bool>("SlothImport:AutoApprove");

                services.AddOptions<ImportOptions>()
                    .Configure(o =>
                    {
                        o.ApiKey = apiKey;
                        o.BaseUrl = baseUrl;
                        o.CsvFile = csvFile;
                        o.ValidateCoA = validateCoA;
                        o.AutoApprove = autoApprove;
                    });

                services.AddSlothApiClient(o =>
                {
                    o.ApiKey = apiKey;
                    o.BaseUrl = baseUrl;
                });
            });
    }
}
