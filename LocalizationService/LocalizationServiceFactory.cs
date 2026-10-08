using DotNext;

namespace LocalizationService;

public static class LocalizationServiceFactory
{
	public static Result<ILocalizationService> Create(string clientId, string clientSecret, string spreadsheetId)
	{
		return new GoogleSheetsLocalizationService(clientId, clientSecret, spreadsheetId);
	}
}