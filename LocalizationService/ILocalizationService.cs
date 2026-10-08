using DotNext;

namespace LocalizationService;

public interface ILocalizationService : IDisposable
{
	Task<Result<string>> UploadLocalizationsFromDirectoryAsync(string localizationsDirectoryPath);
	Task<Result<string>> DownloadLocalizationsToDirectoryAsync(string localizationsDirectoryPath);
}