# Power Query connector coverage measurement

**Scope:** point-in-time investigation after report-side PBIR schema observations.

## Question

Does the available Power Query evidence show a missing connector function important enough to justify a
narrow extension to `MConnectorExtractor`?

## Method and privacy

This measurement used PBI Assure's existing local scanner over the redistribution-safe committed fixtures
and the repository's existing `samples-local` project area. It examined only table-partition M expressions
and named-expression/helper-query M expressions. A small temporary helper removed normal strings and M
comments, found dotted call shapes with the same lexical boundary as the production extractor, and
compared function names with the current registry.

Only aggregate technical evidence was retained here: function names, anonymous counts and source type.
It does not retain project names, query names, paths, arguments, server/database names, URLs, values or
credentials. The temporary helper was not committed.

| Evidence source | Projects | Models | M expressions | Dotted calls | Recognised calls | Unrecognised calls | Scan failures |
|---|---:|---:|---:|---:|---:|---:|---:|
| Committed fixtures | 11 | 11 | 10 | 32 | 0 | 32 | 0 |
| Existing local samples | 29 | 29 | 67 | 236 | 32 | 204 | 0 |
| **Total** | **40** | **40** | **77** | **268** | **32** | **236** | **0** |

The 32 recognised calls were `File.Contents` and `Excel.Workbook` (16 occurrences each in the local
sample scope). No recognised connector call occurred in the committed-fixture M expressions.

## Current recognised coverage

The current registry contains 29 source functions, grouped as follows:

| Family group | Functions currently recognised |
|---|---|
| Files and documents | `File.Contents`, `Folder.Files`, `Folder.Contents`, `Excel.Workbook`, `Csv.Document`, `Pdf.Tables` |
| Web and SharePoint | `Web.Contents`, `OData.Feed`, `SharePoint.Files`, `SharePoint.Contents` |
| Relational and enterprise databases | `Sql.Database`, `Odbc.DataSource`, `Odbc.Query`, `OleDb.DataSource`, `Oracle.Database`, `PostgreSQL.Database`, `MySQL.Database`, `Snowflake.Databases`, `GoogleBigQuery.Database`, `AmazonRedshift.Database`, `SapHana.Database`, `AnalysisServices.Database` |
| Microsoft/Fabric services | `CommonDataService.Database`, `PowerPlatform.Dataflows`, `AzureStorage.Blobs`, `AzureStorage.DataLake`, `Lakehouse.Contents`, `Warehouse.Contents`, `Spark.Tables` |

Location classification is already specific for local, network and relative file paths; web/OData/SharePoint
addresses; and named database/ODBC/OLE DB servers. Other recognised functions are deliberately reported
as dynamic or unspecified rather than guessed.

## Unrecognised dotted calls

All unrecognised calls were in table partitions; none occurred in named expressions.

| Function | Classification | Occurrences | Projects | Likely role | Location classification | Recommendation |
|---|---|---:|---:|---|---|---|
| `Binary.Decompress` | C — ordinary M library | 59 | 36 | Decompresses an in-memory binary value | Not applicable | Ignore |
| `Binary.FromText` | C — ordinary M library | 59 | 36 | Decodes text to an in-memory binary value | Not applicable | Ignore |
| `Json.Document` | B — source-adjacent reader | 59 | 36 | Reads JSON supplied as text or binary | Depends on the supplying expression | Do not add as a connector |
| `Table.FromRows` | C — ordinary M library | 59 | 36 | Creates a table from in-expression rows | Not applicable | Ignore |

The four calls appeared together in the same repeated pattern. The observed call-count distribution shows
that `Json.Document` was paired with binary conversion/decompression rather than a missing source-provider
call [inferred from aggregate call sequences]. It therefore does not establish an external source omitted
from PBI Assure's inventory.

Microsoft documents `Json.Document` as a reader of supplied text or binary; a real source is introduced
by its input, such as `File.Contents` or `Web.Contents`. It documents `Binary.FromText` and
`Binary.Decompress` as binary conversion/library functions, and `Table.FromRows` as in-memory table
construction. These descriptions support treating the observed calls as readers/transforms rather than
connector gaps. [Json.Document](https://learn.microsoft.com/en-us/powerquery-m/json-document),
[Binary functions](https://learn.microsoft.com/en-us/powerquery-m/binary-functions),
[Table.FromRows](https://learn.microsoft.com/en-us/powerquery-m/table-fromrows).

## Result

No measured function belongs to class A (likely external data source). The only borderline function,
`Json.Document`, is class B: it can consume the result of a real connector, but it does not by itself
identify a source family or location. Adding it to `MConnectorExtractor` would create a false data-source
inventory entry for embedded JSON/text and would not improve location classification.

There is no measured evidence for a missing connector family, no new parser/correctness defect, and no
justification for a connector-support implementation. The current registry covered every observed external
source call in this evidence set.

## Recommendation

**Do not add connector support now.** Keep the current registry unchanged.

Re-run this aggregate measurement when a redistribution-safe fixture or an existing local project reveals
an unrecognised class-A source function. Only then assess the function's connector family and argument
shape against Microsoft documentation before proposing a narrow addition. A future, separate investigation
could assess generic unknown-source visibility, but this evidence does not justify designing or implementing
that feature.
