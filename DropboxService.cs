using Dropbox.Api;
using Dropbox.Api.Files;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using DISKUSING;

public class DropboxService : IDiskService
{
    private DropboxClient _dropboxClient;
    private string accessToken;
    private string refreshToken;
    private readonly string appKey;
    private readonly string appSecret;
    private readonly string redirectUri;
    private readonly string tokenFilePath = "dropbox_tokens.json";
    //При создании сервера происходит авторизация токена, если файл dropbox_tokens.json, находится в директории проекта, то автоматизация не нужна.
    //Иначе потребуется авторизировать клиент. (Далее будет реализованно удаление файла dropbox_tokens.json, при истечении срока действия токен, пока что это необходимо делать вручную)
    public DropboxService(string appSecret, string appKey, string redirectUri)
    {
        this.appKey = appKey;
        this.appSecret = appSecret;
        this.redirectUri = redirectUri;

        if (LoadTokens())
        {
            _dropboxClient = new DropboxClient(accessToken);
        }
        else
        {
            Authorize().Wait();
        }
    }

    private async Task Authorize()
    {
        string authorizeUri = GetAuthorizeUri(appKey, redirectUri);
        Console.WriteLine($"Go to the following URL to authorize the application: {authorizeUri}");

        // Переход по ссылке для авторизации
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = authorizeUri,
            UseShellExecute = true
        });

        // Создание отдельного сервера для приема кода, необходимого для получения токена
        var httpServer = new DropBoxAuthorizationServer(redirectUri);
        httpServer.Start();

        string code = await httpServer.WaitForCodeAsync();
        httpServer.Stop();

        await GetAccessTokenAsync(code);
        SaveTokens();
    }
    //Метод для получения ссылки для авторизации
    private string GetAuthorizeUri(string appKey, string redirectUri)
    {
        return $"https://www.dropbox.com/oauth2/authorize?client_id={appKey}&response_type=code&redirect_uri={redirectUri}";
    }
    //Метод для получения токена
    private async Task GetAccessTokenAsync(string code)
    {
        using (var httpClient = new HttpClient())
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/oauth2/token");
            var keyValueContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("client_id", appKey),
                new KeyValuePair<string, string>("client_secret", appSecret),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
            });

            request.Content = keyValueContent;

            var response = await httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error getting access token: {responseString}");
            }

            var jsonResponse = JObject.Parse(responseString);
            accessToken = jsonResponse["access_token"].ToString();
            refreshToken = jsonResponse["refresh_token"]?.ToString();

            _dropboxClient = new DropboxClient(accessToken);
        }
    }
    //Метод для сохранения токенов в файл
    private void SaveTokens()
    {
        var tokens = new Dictionary<string, string>
        {
            { "access_token", accessToken },
            { "refresh_token", refreshToken }
        };

        File.WriteAllText(tokenFilePath, JObject.FromObject(tokens).ToString());
    }
    //Метод для загрузки токенов
    private bool LoadTokens()
    {
        if (File.Exists(tokenFilePath))
        {
            var tokens = JObject.Parse(File.ReadAllText(tokenFilePath));
            accessToken = tokens["access_token"]?.ToString();
            refreshToken = tokens["refresh_token"]?.ToString();
            return true;
        }
        return false;
    }
    private async Task RefreshAccessTokenAsync()
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new InvalidOperationException("No refresh token available.");
        }

        using (var httpClient = new HttpClient())
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/oauth2/token");
            var keyValueContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("client_id", appKey),
                new KeyValuePair<string, string>("client_secret", appSecret)
            });

            request.Content = keyValueContent;

            var response = await httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error refreshing access token: {responseString}");
            }

            var jsonResponse = JObject.Parse(responseString);
            accessToken = jsonResponse["access_token"].ToString();

            SaveTokens();
            _dropboxClient = new DropboxClient(accessToken);
        }
    }

    public async Task UploadFileAsync(string onDiscPath, Stream fileStream)
    {
        if (string.IsNullOrEmpty(onDiscPath))
        {
            throw new ArgumentException("onDiscPath cannot be null or empty", nameof(onDiscPath));
        }
        if (fileStream == null || !fileStream.CanRead)
        {
            throw new ArgumentException("File stream cannot be null and must be readable", nameof(fileStream));
        }
        //Разделение полного пути на путь и имя файла
        var (fileName, folderPath) = await GetFileNameAndParentFolderPathAsync(onDiscPath);
        var uploadPath = FormatDropboxPath(folderPath, fileName);

        try
        {
            if (!string.IsNullOrEmpty(folderPath) && folderPath != "/")
            {
                await CreateDirectoryAsync(folderPath);
            }
            //Использование DropboxClient'а для загрузки потока переданного в метод
            var response = await _dropboxClient.Files.UploadAsync(
                uploadPath,
                WriteMode.Overwrite.Instance,
                body: fileStream
            );
            Console.WriteLine($"File uploaded successfully to {uploadPath}");
        }
        catch (Exception ex)
        {
            // Ловим все исключения и обрабатываем их
            Console.WriteLine($"An error occurred during file upload: {ex.Message}");
            throw;
        }
    }

    public async Task<Stream> DownloadFileAsync(string onDiscPath)
    {
        //Разделение полного пути на путь и имя файла
        var (fileName, folderPath) = await GetFileNameAndParentFolderPathAsync(onDiscPath);
        var downloadPath = FormatDropboxPath(folderPath, fileName);
        var encodedPath = EncodeToUnicodeEscape(downloadPath);

        Console.WriteLine($"Downloading from path: {encodedPath}");

        try
        {
            if (_dropboxClient == null)
            {
                throw new InvalidOperationException("DropboxClient is not initialized.");
            }
            //Скачивание файла, копирования потока бинарных данных из ответа.
            var response = await _dropboxClient.Files.DownloadAsync(encodedPath);
            var memoryStream = new MemoryStream();
            using (var contentStream = await response.GetContentAsStreamAsync())
            {
                await contentStream.CopyToAsync(memoryStream);
            }

            memoryStream.Position = 0; 
            return memoryStream;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            throw new Exception("An error occurred while downloading the file.", ex);
        }
    }
    //Метод для разделения полного пути на имя и директорию
    private  Task<(string fileName, string folderPath)> GetFileNameAndParentFolderPathAsync(string path)
    {
        var parts = path.Trim('/').Split('/');
        if (parts.Length == 0)
        {
            throw new ArgumentException("Invalid path format.", nameof(path));
        }

        string fileName = parts[parts.Length - 1];
        string folderPath = string.Join("/", parts.Take(parts.Length - 1));

        return  Task.FromResult((fileName, folderPath));
    }
    public async Task SaveFileFromStreamAsync(Stream stream, string localFilePath)
    {
        using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
        {
            await stream.CopyToAsync(fileStream);
        }
    }

    private string FormatDropboxPath(string folderPath, string fileName)
    {
        var formattedPath ="/"+ $"/{folderPath.Trim('/').Trim('/')}/{fileName}".Trim('/');
        return formattedPath;
    }
    //В связи с тем, что Dropbox API подразумевает передачу данных, через заголовоки, необходимо кодировать русские символы(например названия папок) в Unicode Escape.
    private string EncodeToUnicodeEscape(string path)
    {
        return Regex.Replace(path, @"[\u0080-\uFFFF]", m =>
        {
            return $"\\u{(int)m.Value[0]:X4}";
        });
    }
    public async Task CreateDirectoryAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Path cannot be null or empty", nameof(path));
        }

        path = "/" + path.TrimStart('/'); // Убедимся, что путь начинается с `/`

        try
        {
            await _dropboxClient.Files.CreateFolderV2Async(path);
            Console.WriteLine($"Directory created successfully: {path}");
        }
        catch (Exception ex)
        {
            // Ловим все исключения и проверяем, является ли ошибка конфликта с папкой
            if (ex.Message.Contains("conflict"))
            {
                Console.WriteLine($"Directory already exists or conflict occurred: {path}");
            }
            else
            {
                Console.WriteLine($"Exception during directory creation: {ex.Message}");
                throw;
            }
        }
    }


}
