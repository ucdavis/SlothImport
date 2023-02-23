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

namespace SlothImport;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // init args parsing
        var baseUrlOption = new Option<string>("--BaseUrl", () => "", "The base url of the sloth api");
        baseUrlOption.AddAlias("-u");
        var apiKeyOption = new Option<string>("--ApiKey", () => "", "The api key to use");
        apiKeyOption.AddAlias("-k");
        var csvFileOption = new Option<FileInfo?>("--CsvFile", () => null, "The csv file to import");
        csvFileOption.AddAlias("-f");

        var rootCommand = new RootCommand("Imports sloth transactions from a csv file");
        rootCommand.AddOption(baseUrlOption);
        rootCommand.AddOption(apiKeyOption);
        rootCommand.AddOption(csvFileOption);

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

            var baseUrl = context.ParseResult.GetValueForOption(baseUrlOption);
            var apiKey = context.ParseResult.GetValueForOption(apiKeyOption);
            var csvFile = context.ParseResult.GetValueForOption(csvFileOption);
            var token = context.GetCancellationToken();
            try
            {
                var host = CreateHostBuilder(baseUrl, apiKey, csvFile).Build();
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

    private static IHostBuilder CreateHostBuilder(string? baseUrl, string? apiKey, FileInfo? csvFile)
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
                apiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : ctx.Configuration["ImportOptions:ApiKey"] ?? "";
                baseUrl = !string.IsNullOrWhiteSpace(baseUrl) ? baseUrl : ctx.Configuration["ImportOptions:BaseUrl"] ?? "";
                var csvFileName = ctx.Configuration["ImportOptions:CsvFile"] ?? "";
                csvFile ??= (string.IsNullOrWhiteSpace(csvFileName) ? null : new FileInfo(csvFileName));

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new Exception("No value supplied for ApiKey");
                }
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    throw new Exception("No value supplied for BaseUrl");
                }
                if (csvFile == null)
                {
                    Log.Information(Directory.GetCurrentDirectory());
                    throw new Exception("No value supplied for CsvFile");
                }

                services.AddOptions<ImportOptions>()
                    .Configure(o =>
                    {
                        o.ApiKey = apiKey;
                        o.BaseUrl = baseUrl;
                        o.CsvFile = csvFile;
                    });

                services.AddSlothApiClient(o =>
                {
                    o.ApiKey = apiKey;
                    o.BaseUrl = baseUrl;
                });
            });
    }
}
