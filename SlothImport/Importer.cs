using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using SlothApi.Models;
using SlothApi.Services;
using SlothImport.Models;

namespace SlothImport;

public interface IImporter
{
    Task Import(CancellationToken cancellationToken);
}

public class Importer : IImporter
{
    private readonly FileInfo _csvFile;
    private readonly ISlothApiClient _slothClient;

    public Importer(IOptions<ImportOptions> importOptions, ISlothApiClient slothClient)
    {
        _csvFile = importOptions.Value.CsvFile;
        _slothClient = slothClient;
    }

    public async Task Import(CancellationToken cancellationToken)
    {
        Log.Information("Importing");
        try
        {
            if (!ValidateData(cancellationToken))
            {
                Log.Error("Validation errors found in CSV file");
                return;
            }

            await ImportData(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Operation cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error importing");
            throw;
        }
    }

    private async Task ImportData(CancellationToken cancellationToken)
    {
        using var reader = _csvFile.OpenText();
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });
        var record = new TransactionRecord();
        var i = 0;

        foreach (var r in csv.EnumerateRecords(record))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Log.Information("Importing row {CsvRow}", i);
            await ImportRecord(r, i);
            i++;
        }
    }

    private bool ValidateData(CancellationToken cancellationToken)
    {
        using var reader = _csvFile.OpenText();
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });
        var record = new TransactionRecord();
        var i = 0;

        bool hasErrors = false;
        foreach (var r in csv.EnumerateRecords(record))
        {
            cancellationToken.ThrowIfCancellationRequested();
            hasErrors |= !TryValidate(r, i);
        }
        return !hasErrors;
    }

    private static bool TryValidate(TransactionRecord record, int recordIndex)
    {
        var validationContext = new ValidationContext(record);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(record, validationContext, validationResults, true);
        if (!isValid)
        {
            var errorMessages = validationResults.Where(r => !string.IsNullOrEmpty(r.ErrorMessage)).Select(r => r.ErrorMessage);
            Log.Error("Validation errors found in row {CsvRow}. ErrorMessages: {ErrorMessages}", recordIndex, errorMessages);
        }

        return isValid;
    }

    private async Task ImportRecord(TransactionRecord record, int recordIndex)
    {
        var createTransaction = new CreateTransactionViewModel(record.Source, record.SourceType)
        {
            MerchantTrackingNumber = record.MerchantTrackingNumber,
            MerchantTrackingUrl = record.MerchantTrackingUrl,
            ProcessorTrackingNumber = record.ProcessorTrackingNumber,
            KfsTrackingNumber = record.KfsTrackingNumber,
            Description = record.TxnDescription,
            ValidateFinancialSegmentStrings = false,
            AutoApprove = false,
            Transfers = new List<CreateTransferViewModel>()
            {
                new CreateTransferViewModel(
                    amount: record.Amount0,
                    financialSegmentString: record.CoA0,
                    description: record.Description0,
                    direction: record.Direction0
                ),
                new CreateTransferViewModel(
                    amount: record.Amount1,
                    financialSegmentString: record.CoA1,
                    description: record.Description1,
                    direction: record.Direction1
                )
            },
        };

        if (record.Amount2 != null && !string.IsNullOrWhiteSpace(record.CoA2) && record.Direction2 != null)
        {
            createTransaction.Transfers.Add(new CreateTransferViewModel(
                amount: record.Amount2.Value,
                financialSegmentString: record.CoA2,
                description: record.Description2,
                direction: record.Direction2.Value
            ));
        }
        if (record.Amount3 != null && !string.IsNullOrWhiteSpace(record.CoA3) && record.Direction3 != null)
        {
            createTransaction.Transfers.Add(new CreateTransferViewModel(
                amount: record.Amount3.Value,
                financialSegmentString: record.CoA3,
                description: record.Description3,
                direction: record.Direction3.Value
            ));
        }
        if (!string.IsNullOrWhiteSpace(record.MetaDataName) && !string.IsNullOrWhiteSpace(record.MetaDataValue))
        {
            createTransaction.Metadata = new List<MetadataEntry>
            {
                new MetadataEntry
                {
                    Name = record.MetaDataName,
                    Value = record.MetaDataValue
}
            };
        }

        try
        {
            var result = await _slothClient.CreateTransaction(createTransaction);
            if (result.Success && result.Data != null)
            {
                Log.Information("Transaction id {TransactionId} imported from record {RecordIndex} successfully", result.Data.Id, recordIndex);
            }
            else
            {
                Log.Error("Transaction imported from record {RecordIndex} failed with status code {StatusCode}, message {Message}", recordIndex, result.StatusCode, result.Message);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error sending import request for record {RecordIndex}", recordIndex);
            throw;
        }

    }
}