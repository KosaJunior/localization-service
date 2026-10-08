using DotNext;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Newtonsoft.Json;

namespace LocalizationService;

public sealed class GoogleSheetsLocalizationService : ILocalizationService
{
	private const string _APPLICATION_NAME = "Google Sheets Localization Service";
	private const string _LOCALIZATION_FILE_SUFFIX = "_loc";
	private const string _LOCALIZATION_FILE_EXTENSION = ".json";
	private const string _LOCALIZATION_FILE_PATTERN = $"*{_LOCALIZATION_FILE_SUFFIX}{_LOCALIZATION_FILE_EXTENSION}";
	private const string _BACKUP_DIRECTORY_NAME = "backup";

	private readonly SheetsService _service;
	private readonly string _spreadsheetId;

	public GoogleSheetsLocalizationService(string clientId, string clientSecret, string spreadsheetId)
		: this(CreateSheetsService(clientId, clientSecret), spreadsheetId)
	{ }

	public GoogleSheetsLocalizationService(SheetsService service, string spreadsheetId)
	{
		ArgumentNullException.ThrowIfNull(service);
		ArgumentException.ThrowIfNullOrWhiteSpace(spreadsheetId);
		
		_service = service;
		_spreadsheetId = spreadsheetId;
	}

	private static SheetsService CreateSheetsService(string clientId, string clientSecret)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
		ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

		return new SheetsService(new BaseClientService.Initializer
		{
			ApplicationName = _APPLICATION_NAME,
			HttpClientInitializer = Utils.GetCredentials(clientId, clientSecret, [SheetsService.Scope.Spreadsheets])
		});
	}
	
	public async Task<Result<string>> UploadLocalizationsFromDirectoryAsync(string localizationsDirectoryPath)
	{
		try
		{
			IReadOnlyList<LanguageStrings> languages = await ReadLocalizationFilesAsync(localizationsDirectoryPath);
			if (languages.Count == 0)
			{
				return new Result<string>(new FileNotFoundException(
					$"No '{_LOCALIZATION_FILE_PATTERN}' files found in '{localizationsDirectoryPath}'."));
			}

			IList<IList<object>> rows = LocalizationSheetMapper.ToRows(languages);
			string sheetReference = await GetFirstSheetReferenceAsync();
			BatchUpdateValuesResponse response = await BatchUpdateValuesAsync(sheetReference, rows);
			
			return new Result<string>($"""
			                           {nameof(response.TotalUpdatedCells)}: {response.TotalUpdatedCells}
			                           {nameof(response.TotalUpdatedColumns)}: {response.TotalUpdatedColumns}
			                           {nameof(response.TotalUpdatedRows)}: {response.TotalUpdatedRows}
			                           """);
		}
		catch (Exception e)
		{
			return new Result<string>(e);
		}
	}

	public async Task<Result<string>> DownloadLocalizationsToDirectoryAsync(string localizationsDirectoryPath)
	{
		try
		{
			// By design, all localization data lives on the first sheet.
			string sheetReference = await GetFirstSheetReferenceAsync();
			IList<IList<object>>? sheetValues = await GetSheetValuesAsync(sheetReference);
			IReadOnlyList<LanguageStrings> languages = LocalizationSheetMapper.FromRows(sheetValues);
			
			// Back up only after the sheet was fetched and validated,
			// so a failed download can never overwrite the previous backup.
			Directory.CreateDirectory(localizationsDirectoryPath);
			CreateBackup(localizationsDirectoryPath);
			
			foreach (LanguageStrings language in languages)
			{
				await WriteLocalizationFileAsync(localizationsDirectoryPath, language);
			}
			
			return new Result<string>($"Downloaded {languages.Count} localization file(s) to '{localizationsDirectoryPath}'.");
		}
		catch (Exception e)
		{
			return new Result<string>(e);
		}
	}
	
	public void Dispose()
	{
		_service.Dispose();
	}
	
	private static async Task<IReadOnlyList<LanguageStrings>> ReadLocalizationFilesAsync(string directoryPath)
	{
		List<LanguageStrings> languages = [];

		foreach (string filePath in EnumerateLocalizationFiles(directoryPath))
		{
			string json = await File.ReadAllTextAsync(filePath);
			string languageName = GetLanguageName(filePath);
			IReadOnlyDictionary<string,string> entries = ParseEntries(filePath, json);
			languages.Add(new LanguageStrings(languageName, entries));
		}

		return languages;
	}

	private static IEnumerable<string> EnumerateLocalizationFiles(string directoryPath)
	{
		return Directory
			.EnumerateFiles(directoryPath, _LOCALIZATION_FILE_PATTERN, SearchOption.TopDirectoryOnly)
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
	}

	private static string GetLanguageName(string filePath)
	{
		string fileName = Path.GetFileNameWithoutExtension(filePath);
		return fileName[..^_LOCALIZATION_FILE_SUFFIX.Length];
	}

	private static Dictionary<string, string> ParseEntries(string filePath, string json)
	{
		string fileName = Path.GetFileName(filePath);

		try
		{
			return JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
			       ?? throw new InvalidDataException($"'{fileName}' does not contain a JSON object.");
		}
		catch (JsonException e)
		{
			throw new InvalidDataException($"'{fileName}' is not a valid localization file: {e.Message}", e);
		}
	}

	private async Task<string> GetFirstSheetReferenceAsync()
	{
		SpreadsheetsResource.GetRequest? request = _service.Spreadsheets.Get(_spreadsheetId);
		request.Fields = "sheets.properties.title"; // metadata only; skip everything else
		Spreadsheet? spreadsheet = await request.ExecuteAsync();
		
		string title = spreadsheet.Sheets?.FirstOrDefault()?.Properties?.Title
		               ?? throw new InvalidOperationException("The spreadsheet does not contain any sheets.");

		return $"'{title.Replace("'", "''")}'";
	}

	private async Task<BatchUpdateValuesResponse> BatchUpdateValuesAsync(string sheetReference, IList<IList<object>> rows)
	{
		BatchUpdateValuesRequest request = new()
		{
			ValueInputOption = "RAW",
			Data = new List<ValueRange>
			{
				new()
				{
					Range = $"{sheetReference}!A1",
					Values = rows
				}
			}
		};

		return await _service.Spreadsheets.Values
			.BatchUpdate(request, _spreadsheetId)
			.ExecuteAsync();
	}

	private async Task<IList<IList<object>>?> GetSheetValuesAsync(string sheetReference)
	{
		ValueRange? response = await _service.Spreadsheets.Values
			.Get(_spreadsheetId, sheetReference)
			.ExecuteAsync();
		return response?.Values;
	}

	private static void CreateBackup(string directoryPath)
	{
		string backupDirectoryPath = Path.Combine(directoryPath, _BACKUP_DIRECTORY_NAME);
		Directory.CreateDirectory(backupDirectoryPath);
		
		foreach (string filePath in EnumerateLocalizationFiles(directoryPath))
		{
			string backupFileName = $"{Path.GetFileNameWithoutExtension(filePath)}.backup{Path.GetExtension(filePath)}";
			File.Copy(filePath, Path.Combine(backupDirectoryPath, backupFileName), overwrite: true);
		}
	}

	private static async Task WriteLocalizationFileAsync(string directoryPath, LanguageStrings language)
	{
		string fileName = CreateFileName(language.Language);
		string filePath = Path.Combine(directoryPath, fileName);
		string tempPath = $"{filePath}.tmp";
		string json = JsonConvert.SerializeObject(language.Entries, Formatting.Indented);
		
		await File.WriteAllTextAsync(tempPath, json);
		File.Move(tempPath, filePath, overwrite: true);
	}

	private static string CreateFileName(string language)
	{
		return $"{language}{_LOCALIZATION_FILE_SUFFIX}{_LOCALIZATION_FILE_EXTENSION}";
	}
}
