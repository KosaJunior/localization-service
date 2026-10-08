using System.Globalization;

namespace LocalizationService;

internal sealed record LanguageStrings(string Language, IReadOnlyDictionary<string, string> Entries);

internal static class LocalizationSheetMapper
{
	public const string KEY_HEADER = "Key";

	private const int _HEADER_ROW_INDEX = 0;
	private const int _KEY_COLUMN_INDEX = 0;
	private const int _FIRST_LANGUAGE_COLUMN_INDEX = 1;

	public static IList<IList<object>> ToRows(IReadOnlyList<LanguageStrings> languages)
	{
		foreach (LanguageStrings language in languages)
		{
			ValidateLanguageName(language.Language);
		}

		List<string> keys = [];
		HashSet<string> knownKeys = new(StringComparer.Ordinal);
		
		foreach (LanguageStrings language in languages)
		{
			foreach (string key in language.Entries.Keys)
			{
				if (knownKeys.Add(key))
					keys.Add(key);
			}
		}
		
		List<IList<object>> rows = new(keys.Count + 1);
		List<object> header = new(languages.Count + 1) { KEY_HEADER };
		header.AddRange(languages.Select(language => language.Language));
		rows.Add(header);
		
		foreach (string key in keys)
		{
			List<object> row = new(languages.Count + 1) { key };
			
			foreach (LanguageStrings language in languages)
			{
				string rowValue = language.Entries.TryGetValue(key, out string? value)
					? value
					: string.Empty;
				row.Add(rowValue);
			}
			
			rows.Add(row);
		}
		
		return rows;
	}

	public static IReadOnlyList<LanguageStrings> FromRows(IList<IList<object>>? rows)
	{
		if (rows is null or { Count: 0 })
			throw new InvalidDataException("The sheet is empty.");

		List<(int ColumnIndex, string Language)> languageColumns = ReadLanguageColumns(rows[_HEADER_ROW_INDEX]);
		if (languageColumns.Count == 0)
			throw new InvalidDataException($"The header row has no language columns (expected: {KEY_HEADER} | <language> | ...).");

		StringComparer _DICTS_COMPARER = StringComparer.Ordinal;
		List<Dictionary<string, string>> entriesPerColumn = languageColumns
			.Select(_ => new Dictionary<string, string>(_DICTS_COMPARER))
			.ToList();
		Dictionary<string, int> sheetRowOfKey = new(_DICTS_COMPARER);

		for (int rowIndex = _HEADER_ROW_INDEX + 1; rowIndex < rows.Count; rowIndex++)
		{
			IList<object> row = rows[rowIndex];
			string key = GetCell(row, _KEY_COLUMN_INDEX);

			if (string.IsNullOrWhiteSpace(key))
				continue;

			int sheetRowNumber = rowIndex + 1;
			if (!sheetRowOfKey.TryAdd(key, sheetRowNumber))
				throw new InvalidDataException($"Duplicate key '{key}' in sheet rows {sheetRowOfKey[key]} and {sheetRowNumber}.");

			for (int i = 0; i < languageColumns.Count; i++)
			{
				string cellValue = GetCell(row, languageColumns[i].ColumnIndex);
				entriesPerColumn[i].Add(key, cellValue);
			}
		}

		return languageColumns
			.Select((column, i) => new LanguageStrings(column.Language, entriesPerColumn[i]))
			.ToList();
	}
	
	private static void ValidateLanguageName(string language)
	{
		if (string.IsNullOrWhiteSpace(language))
			throw new InvalidDataException("A language name must be not empty");
		
		if (language.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
			throw new InvalidDataException($"Language name '{language}' contains characters that are not allowed in file names.");
	}

	private static List<(int ColumnIndex, string Language)> ReadLanguageColumns(IList<object> headerRow)
	{
		List<(int ColumnIndex, string Language)> columns = [];
		HashSet<string> knownLanguages = new(StringComparer.OrdinalIgnoreCase);

		for (int columnIndex = _FIRST_LANGUAGE_COLUMN_INDEX; columnIndex < headerRow.Count; columnIndex++)
		{
			string language = GetCell(headerRow, columnIndex).Trim();
			if (language.Length == 0)
				continue;
			
			ValidateLanguageName(language);
			if (!knownLanguages.Add(language))
				throw new InvalidDataException($"Duplicate language column '{language}'.");
			
			columns.Add((columnIndex, language));
		}

		return columns;
	}

	private static string GetCell(IList<object> row, int columnIndex)
	{
		if (columnIndex >= row.Count)
			return string.Empty;
		
		return Convert.ToString(row[columnIndex], CultureInfo.InvariantCulture) ?? string.Empty;
	}
}
