using DotNext;

namespace ResultExtensions;

public static class ResultExtensions
{
	public static Result<T> LogIfSuccessful<T>(this Result<T> result, string successLog = "Result Success")
	{
		if (result.IsSuccessful)
			Console.WriteLine($"\n{successLog}:\n {result.ToString()}");

		return result;
	}

	public static Result<T> LogIfUnsuccessful<T>(this Result<T> result, string failureLog = "Result Failed")
	{
		if (result.IsSuccessful == false)
			Console.WriteLine($"\n{failureLog}:\n {result.ToString()}");

		return result;
	}

	public static bool TryGetValue<T>(this Result<T> result, out T? value)
	{
		value = result.ValueRef;
		return result.IsSuccessful;
	}
}