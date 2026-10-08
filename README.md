# Localization Service

A small .NET library that keeps your localization files in sync with a Google Sheet.

## How it works

- **Upload:** reads every `{language}_loc.json` file in a folder and writes them to the first sheet of a spreadsheet.
- **Download:** reads the sheet and writes one `{language}_loc.json` file per language column.
- **Backup:** existing files are copied to a `Backup` folder before they are overwritten.

## Sheet layout

| Key         | en    | pl     |
|-------------|-------|--------|
| menu.play   | Play  | Graj   |
| menu.quit   | Quit  | Wyjdź  |

The first column holds the keys. Each following column is one language, and the header is the language name.

## Usage

```csharp
using var service = new GoogleSheetsLocalizationService(clientId, clientSecret, spreadsheetId);

await service.UploadLocalizationsFromDirectoryAsync("path/to/localizations");
await service.DownloadLocalizationsToDirectoryAsync("path/to/localizations");
```

Both methods return a `Result<string>`, so check for success before using the value.
