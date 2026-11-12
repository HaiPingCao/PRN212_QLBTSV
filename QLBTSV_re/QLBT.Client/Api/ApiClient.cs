using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using QLBT.Shared;

namespace QLBT.Client.Api;

public class ApiResult
{
    public ApiStatus Status { get; set; }

    public int HttpStatus { get; set; }

    public string? Error { get; set; }

    public bool IsOk
    {
        get { return Status == ApiStatus.Ok; }
    }

    /// <summary>The server's error text, or a generic one built from the status.</summary>
    public string Message
    {
        get { return string.IsNullOrWhiteSpace(Error) ? Status.ToString() : Error; }
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
/// HTTP wrapper (ported from Svr3.Client). Keeps the SessionId and sends it as a Bearer token.
/// Per Plan.md: an Unauthorized answer or a Logout always clears the SessionId.
/// Network failures never throw: they come back as Status = Error with a "Cannot reach server" message.
/// </summary>
public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http;

    public string? SessionId { get; private set; }

    public Uri BaseAddress { get; }

    public ApiClient(Uri baseAddress)
    {
        BaseAddress = baseAddress;
        SocketsHttpHandler handler = new SocketsHttpHandler();
        handler.ConnectTimeout = TimeSpan.FromSeconds(5); // fail fast when the server is down
        _http = new HttpClient(handler);
        _http.BaseAddress = baseAddress;
        _http.Timeout = Timeout.InfiniteTimeSpan; // large files
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

    /// <summary>Always clears the SessionId, even when the server does not answer.</summary>
    public async Task Logout()
    {
        try
        {
            await SendJson<object>(new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Logout));
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

    public Task<ApiResult<TBody>> Post<TBody>(string path, object body)
    {
        return SendJson<TBody>(WithJson(HttpMethod.Post, path, body));
    }

    public Task<ApiResult<object>> Put(string path, object body)
    {
        return SendJson<object>(WithJson(HttpMethod.Put, path, body));
    }

    public Task<ApiResult<object>> Delete(string path)
    {
        return SendJson<object>(new HttpRequestMessage(HttpMethod.Delete, path));
    }

    /// <summary>PUTs the local file as the raw body, original name in ?fileName=.</summary>
    public async Task<ApiResult<TBody>> UploadFile<TBody>(string path, string localFile)
    {
        FileStream source;
        try
        {
            source = File.OpenRead(localFile);
        }
        catch (IOException ex)
        {
            return Fail<TBody>(ApiStatus.Error, "Cannot read the file: " + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Fail<TBody>(ApiStatus.Error, "Cannot read the file: " + ex.Message);
        }

        using (source)
        {
            StreamContent content = new StreamContent(source, 81920);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.ContentLength = source.Length;

            string fileName = Path.GetFileName(localFile);
            string url = path + "?" + ApiRoutes.FileNameQuery + "=" + Uri.EscapeDataString(fileName);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Content = content;
            // The server checks permissions and rules first; if it refuses, the file bytes are never sent.
            request.Headers.ExpectContinue = true;
            return await SendJson<TBody>(request);
        }
    }

    /// <summary>
    /// GETs a file. Once the headers arrive, <paramref name="chooseTarget"/> gets the original name (Content-Disposition)
    /// and returns where to save it, or null to cancel. Ok with a null Body means the user cancelled.
    /// </summary>
    public async Task<ApiResult<DownloadedFile>> Download(string path, Func<string, string?> chooseTarget)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, path);
        AddSession(request);
        string? target = null;
        try
        {
            using (HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                if (!response.IsSuccessStatusCode)
                {
                    return await ReadError<DownloadedFile>(response);
                }

                ContentDispositionHeaderValue? disposition = response.Content.Headers.ContentDisposition;
                string fileName = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"') ?? "download.bin";
                fileName = Path.GetFileName(fileName);

                ApiResult<DownloadedFile> result = Ok<DownloadedFile>(response);
                target = chooseTarget(fileName);
                if (target == null)
                {
                    return result;
                }

                using (Stream body = await response.Content.ReadAsStreamAsync())
                using (FileStream file = File.Create(target))
                {
                    await body.CopyToAsync(file);
                }

                result.Body = new DownloadedFile { Path = target, FileName = fileName, Length = new FileInfo(target).Length };
                return result;
            }
        }
        catch (HttpRequestException ex)
        {
            DeletePartial(target);
            return Unreachable<DownloadedFile>(ex);
        }
        catch (IOException ex)
        {
            DeletePartial(target);
            return Fail<DownloadedFile>(ApiStatus.Error, "Download failed: " + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Fail<DownloadedFile>(ApiStatus.Error, "Cannot write the file: " + ex.Message);
        }
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    private static HttpRequestMessage WithJson(HttpMethod method, string path, object body)
    {
        HttpRequestMessage request = new HttpRequestMessage(method, path);
        request.Content = JsonContent.Create(body, body.GetType(), options: ApiJson.Options);
        return request;
    }

    private async Task<ApiResult<TBody>> SendJson<TBody>(HttpRequestMessage request)
    {
        AddSession(request);
        try
        {
            using (HttpResponseMessage response = await _http.SendAsync(request))
            {
                if (!response.IsSuccessStatusCode)
                {
                    return await ReadError<TBody>(response);
                }

                ApiResult<TBody> result = Ok<TBody>(response);
                // Results.Ok() without a value sends an empty body: only parse when there is something.
                string text = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Body = JsonSerializer.Deserialize<TBody>(text, ApiJson.Options);
                }
                return result;
            }
        }
        catch (HttpRequestException ex)
        {
            return Unreachable<TBody>(ex);
        }
        catch (IOException ex)
        {
            return Unreachable<TBody>(ex);
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

    private static ApiResult<TBody> Fail<TBody>(ApiStatus status, string error)
    {
        ApiResult<TBody> result = new ApiResult<TBody>();
        result.Status = status;
        result.Error = error;
        return result;
    }

    private ApiResult<TBody> Unreachable<TBody>(Exception ex)
    {
        return Fail<TBody>(ApiStatus.Error, "Cannot reach server " + BaseAddress + "\n" + ex.GetBaseException().Message);
    }

    private static void DeletePartial(string? path)
    {
        if (path == null)
        {
            return;
        }
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
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
            case HttpStatusCode.RequestEntityTooLarge:
                return ApiStatus.Rejected;
            default:
                return ApiStatus.Error;
        }
    }
}
