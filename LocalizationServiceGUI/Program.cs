using LocalizationService;
using ResultExtensions;

Console.WriteLine("Localization Service 1.0");

Console.WriteLine("\nEnter client id:");
string? clientId = Console.ReadLine();
if (string.IsNullOrWhiteSpace(clientId))
	return;

Console.WriteLine("\nEnter client secret:");
string? clientSecret = Console.ReadLine();
if (string.IsNullOrWhiteSpace(clientSecret))
	return;

Console.WriteLine("\nEnter localizations directory:");
string? localizationsDirectoryPath = Console.ReadLine();
if (string.IsNullOrWhiteSpace(localizationsDirectoryPath))
	return;

Console.WriteLine("\nEnter spreadsheet id:");
string? spreadsheetId = Console.ReadLine();
if (string.IsNullOrWhiteSpace(spreadsheetId))
	return;

if (LocalizationServiceFactory.Create(clientId, clientSecret, spreadsheetId)
	    .LogIfUnsuccessful()
	    .TryGetValue(out var service) == false)
	return;

while (true)
{
	ConsoleKey input = WaitForInput();
	switch (input)
	{
		case ConsoleKey.U:
			Console.WriteLine("\nUploading...");
			await HandleUploadAsync();
			break;

		case ConsoleKey.D:
			Console.WriteLine("\nDownloading...");
			await HandleDownloadAsync();
			break;

		default:
			Console.WriteLine("\nExiting...");
			service.Dispose();
			return;
	}
}

async Task HandleUploadAsync()
{
	var result = await service.UploadLocalizationsFromDirectoryAsync(localizationsDirectoryPath);
	result.LogIfSuccessful();
	result.LogIfUnsuccessful();
}

async Task HandleDownloadAsync()
{
	var result = await service.DownloadLocalizationsToDirectoryAsync(localizationsDirectoryPath);
	result.LogIfSuccessful();
	result.LogIfUnsuccessful();
}

ConsoleKey WaitForInput()
{
	Console.WriteLine("""

	                  Press key to:
	                  d - download all localizations,
	                  u - upload all localizations,
	                  any other - exit
	                  """);
	return Console.ReadKey().Key;
}