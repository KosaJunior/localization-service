using Google.Apis.Auth.OAuth2;

namespace LocalizationService;

public static class Utils
{
	public static UserCredential GetCredentials(string clientId, string clientSecret, IEnumerable<string> scopes)
	{
		var clientSecrets = new ClientSecrets
		{
			ClientId = clientId,
			ClientSecret = clientSecret
		};

		return GoogleWebAuthorizationBroker.AuthorizeAsync(
			clientSecrets: clientSecrets,
			scopes: scopes,
			user: "user",
			taskCancellationToken: CancellationToken.None)
			.Result;
	}
}