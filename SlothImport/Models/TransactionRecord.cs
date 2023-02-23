using System.ComponentModel.DataAnnotations;
using SlothApi.Models;

namespace SlothImport.Models;

public class TransactionRecord
{
    [MaxLength(128)]
    public string MerchantTrackingNumber { get; set; } = "";
    public string MerchantTrackingUrl { get; set; } = "";
    [MaxLength(128)]
    public string ProcessorTrackingNumber { get; set; } = "";
    [MaxLength(10)]
    public string KfsTrackingNumber { get; set; } = "";
    [Required]
    public string Source { get; set; } = "";
    [Required]
    public string SourceType { get; set; } = "";
    public string TxnDescription { get; set; } = "";


    // required Transfer
    [Range(typeof(decimal), "0.01", "1000000000")]
    [Required]
    public decimal Amount0 { get; set; }
    [Required]
    public string CoA0 { get; set; } = "";
    [MaxLength(40)]
    [Required]
    public string Description0 { get; set; } = "";
    [Required]
    public Transfer.CreditDebit Direction0 { get; set; }

    // required Transfer
    [Range(typeof(decimal), "0.01", "1000000000")]
    [Required]
    public decimal Amount1 { get; set; }
    [Required]
    public string CoA1 { get; set; } = "";
    [MaxLength(40)]
    [Required]
    public string Description1 { get; set; } = "";
    [Required]
    public Transfer.CreditDebit Direction1 { get; set; }

    // optional Transfer
    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal? Amount2 { get; set; }
    public string CoA2 { get; set; } = "";
    [MaxLength(40)]
    public string Description2 { get; set; } = "";
    public Transfer.CreditDebit? Direction2 { get; set; }

    // optional Transfer
    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal? Amount3 { get; set; }
    public string CoA3 { get; set; } = "";
    [MaxLength(40)]
    public string Description3 { get; set; } = "";
    public Transfer.CreditDebit? Direction3 { get; set; }

    // MetaData
    [MaxLength(128)]
    public string MetaDataName { get; set; } = "";
    public string MetaDataValue { get; set; } = "";
}