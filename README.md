# SlothImport
App to import Sloth transactions from a csv file.

See [ExampleData.csv](SlothImport/data/ExampleData.csv) for an example of the csv file format.


```
Description:
  Imports sloth transactions from a csv file

  All options can be specified via corresponding environment variables prefixed with
  "SlothImport__", or via user secrets if in a development environment

Usage:
  SlothImport [options]

Options:
  -u, -url, --BaseUrl <BaseUrl>   The base url of the sloth api (required)
  -k, -key, --ApiKey <ApiKey>     The api key to use (required)
  -f, -file, --CsvFile <CsvFile>  The csv file to import (required)
  -v, -validate, --ValidateCoA    Have Sloth perform validation of Chart of Accounts
  -a, -approve, --AutoApprove     Have Sloth auto-approve imported transactions
  --version                       Show version information
  -?, -h, --help                  Show help and usage information
```