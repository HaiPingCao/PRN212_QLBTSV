using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Svr3.Shared;

namespace Svr3.Client;

public class ApiResult
{
    public ApiStatus Status { get; set; }

    public int HttpStatus { get; set; }

    public string? Error { get; set; }

    public bool IsOk
    {
        get { return Status == ApiStatus.Ok; }
    }

    public override string ToString()
    {
        return Status + " (" + HttpStatus + ")" + (Error == null ? "" : ": " + Error);
    }
}

public sealed class ApiResult<TBody> : ApiResult
{
    public TBody? Body { get; set; }
}

public sealed class DownloadedFile
{
    public string Path { get; set; } = "";

    public string FileName { get; set; } = "";

    public long Length { get; set; }
}

/// <summary>
/// One client per logged-in user. Keeps the SessionId and sends it as a Bearer token.
/// Per Plan.md: an Unauthorized answer or a Logout always clears the SessionId.
/// </summary>
public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http;

    public string? SessionId { get; private set; }

    public ApiClient(Uri baseAddress)
    {
        _http = new HttpClient();
        _http.BaseAddress = baseAddress;
        _http.Timeout = Timeout.InfiniteTimeSpan; // large files
    }

    /// <summary>Resumes a session id obtained earlier (e.g. saved between app runs).</summary>
    public void UseSession(string sessionId)
    {
        SessionId = sessionId;
    }

    public async Task<ApiResult<LoginResponse>> Login(string username, string password)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Login);
        request.Content = JsonContent.Create(new LoginRequest { Username = username, Password = password }, options: ApiJson.Options);
        ApiResult<LoginResponse> result = await SendJson<LoginResponse>(request);
        if (result.IsOk && result.Body != null)
        {
            SessionId = result.Body.SessionId;
        }
        return result;
    }

    public async Task<ApiResult<object>> Logout()
    {
        try
        {
            return await SendJson<object>(new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Logout));
        }
        finally
        {
            SessionId = null;
        }
    }

    public Task<ApiResult<TBody>> Get<TBody>(string path)
    {
        return SendJson<TBody>(new HttpRequestMessage(HttpMethod.Get, path));
    }

    /// <summary>PUTs <paramref name="length"/> bytes of <paramref name="source"/> as the raw body.</summary>
    public Task<ApiResult<TBody>> Upload<TBody>(string path, string fileName, Stream source, long length)
    {
        StreamContent content = new StreamContent(source, 81920);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentLength = length;

        string url = path + "?" + ApiRoutes.FileNameQuery + "=" + Uri.EscapeDataString(fileName);
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Content = content;
        // The server checks permissions and rules first; if it refuses, the file bytes are never sent.
        request.Headers.ExpectContinue = true;
        return SendJson<TBody>(request);
    }

    public async Task<ApiResult<TBody>> UploadFile<TBody>(string path, string localFile, string fileName)
    {
        using (FileStream source = File.OpenRead(localFile))
        {
            return await Upload<TBody>(path, fileName, source, source.Length);
        }
    }

    /// <summary>Saves the file into <paramref name="folder"/> under the name from Content-Disposition.</summary>
    public async Task<ApiResult<DownloadedFile>> Download(string path, string folder)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, path);
        AddSession(request);
        using (HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
        {
            if (!response.IsSuccessStatusCode)
            {
                return await ReadError<DownloadedFile>(response);
            }

            ContentDispositionHeaderValue? disposition = response.Content.Headers.ContentDisposition;
            string fileName = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"') ?? "download.bin";
            Directory.CreateDirectory(folder);
            string target = Path.Combine(folder, Path.GetFileName(fileName));

            using (Stream body = await response.Content.ReadAsStreamAsync())
            using (FileStream file = File.Create(target))
            {
                await body.CopyToAsync(file);
            }

            ApiResult<DownloadedFile> result = Ok<DownloadedFile>(response);
            result.Body = new DownloadedFile { Path = target, FileName = fileName, Length = new FileInfo(target).Length };
            return result;
        }
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    private async Task<ApiResult<TBody>> SendJson<TBody>(HttpRequestMessage request)
    {
        AddSession(request);
        using (HttpResponseMessage response = await _http.SendAsync(request))
        {
            if (!response.IsSuccessStatusCode)
            {
                return await ReadError<TBody>(response);
            }

            ApiResult<TBody> result = Ok<TBody>(response);
            if (response.Content.Headers.ContentLength != 0)
            {
                result.Body = await response.Content.ReadFromJsonAsync<TBody>(ApiJson.Options);
            }
            return result;
        }
    }

    private void AddSession(HttpRequestMessage request)
    {
        if (SessionId != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SessionId);
        }
    }

    private static ApiResult<TBody> Ok<TBody>(HttpResponseMessage response)
    {
        ApiResult<TBody> result = new ApiResult<TBody>();
        result.Status = ApiStatus.Ok;
        result.HttpStatus = (int)response.StatusCode;
        return result;
    }

    private async Task<ApiResult<TBody>> ReadError<TBody>(HttpResponseMessage response)
    {
        ApiResult<TBody> result = new ApiResult<TBody>();
        result.HttpStatus = (int)response.StatusCode;
        result.Status = FromHttpStatus(response.StatusCode);

        try
        {
            ApiError? error = await response.Content.ReadFromJsonAsync<ApiError>(ApiJson.Options);
            if (error != null)
            {
                result.Status = error.Status;
                result.Error = error.Error;
            }
        }
        catch (JsonException)
        {
        }
        catch (NotSupportedException)
        {
            // Not JSON (e.g. Kestrel's own 413 page); keep the status from the HTTP code.
        }

        if (result.Status == ApiStatus.Unauthorized)
        {
            SessionId = null;
        }
        return result;
    }

    private static ApiStatus FromHttpStatus(HttpStatusCode code)
    {
        switch (code)
        {
            case HttpStatusCode.Unauthorized:
                return ApiStatus.Unauthorized;
            case HttpStatusCode.Forbidden:
                return ApiStatus.Forbidden;
            case HttpStatusCode.NotFound:
                return ApiStatus.NotFound;
            case HttpStatusCode.Conflict:
                return ApiStatus.Rejected;
            default:
                return ApiStatus.Error;
        }
    }
}
