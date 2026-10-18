using System.Net.Http;

/// <summary>
/// For server calls that block their caller. iOS does not support HttpClient.Send ("Operation is
/// not supported on this platform"), so they wait on SendAsync, which every platform supports.
/// </summary>
public static class BlockingHttp
{
	public static HttpResponseMessage SendAndWait(this HttpClient client, HttpRequestMessage request) =>
		client.SendAsync(request).GetAwaiter().GetResult();
}
