using DISKUSING;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net;
//КЛАСС БЫЛ РЕАЛИЗОВАН БЕЗ ИСПОЛЬЗОВАНИЯ БИБЛИОТЕК ЯНДЕКСА ДЛЯ ВЗАИМОДЕЙСТВИЯ С API, РАДИ ПРАКТИКИ, ДАЛЕЕ БУДЕТ ПЕРЕДЕЛАНО
namespace DISKUSING
{
    public class YandexDiskService : IDiskService
    {
        private readonly string accessToken;
        private readonly HttpClient httpClient;

        public YandexDiskService(HttpClient httpClient, string accessToken)
        {
            this.accessToken = accessToken;
            this.httpClient = httpClient;
        }
        public async Task<Stream> DownloadFileAsync(string yandexDiskPath)
        {
            if (string.IsNullOrEmpty(yandexDiskPath))
            {
                throw new ArgumentException("yandexDiskPath cannot be null or empty", nameof(yandexDiskPath));
            }
            //API Яндекса устроенно так, что сначало необходимо получить ссылку на скачивание, что собственно тут и происходи (переменная href). И только после этого скачать файл, используя полученную ссылку.
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://cloud-api.yandex.net/v1/disk/resources/download?path={Uri.EscapeDataString(yandexDiskPath)}");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                dynamic data = JObject.Parse(await response.Content.ReadAsStringAsync());
                string href = data.href;

                if (string.IsNullOrEmpty(href))
                {
                    throw new Exception("Download URL not found.");
                }

                request = new HttpRequestMessage(HttpMethod.Get, href);
                response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStreamAsync();
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HttpRequestException during file download: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during file download: {ex.Message}");
                throw;
            }
        }

        public async Task UploadFileAsync(string yandexDiskPath, Stream fileStream)
        {
            if (string.IsNullOrEmpty(yandexDiskPath))
            {
                throw new ArgumentException("yandexDiskPath cannot be null or empty", nameof(yandexDiskPath));
            }
            if (fileStream == null || !fileStream.CanRead)
            {
                throw new ArgumentException("File stream cannot be null and must be readable", nameof(fileStream));
            }

            // Приводим путь к стандарту Yandex Disk
            yandexDiskPath = yandexDiskPath.Replace('\\', '/');

            // Разделяем путь на директорию и имя файла
            var directoryPath = Path.GetDirectoryName(yandexDiskPath);

            // Создаем директорию, если она не существует
            if (!string.IsNullOrEmpty(directoryPath) && directoryPath != "/")
            {
                // Приводим путь к стандарту Yandex Disk
                directoryPath = directoryPath.Replace('\\', '/');
                await CreateDirectoryAsync(directoryPath);
            }

            // Получаем URL для загрузки файла
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://cloud-api.yandex.net/v1/disk/resources/upload?path={Uri.EscapeDataString(yandexDiskPath)}&overwrite=true");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            dynamic data = JObject.Parse(await response.Content.ReadAsStringAsync());
            string href = data.href;

            // Загружаем файл напрямую из потока
            using (var uploadStreamContent = new StreamContent(fileStream))
            {
                uploadStreamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                var uploadResponse = await httpClient.PutAsync(href, uploadStreamContent);
                uploadResponse.EnsureSuccessStatusCode();
            }

            Console.WriteLine($"Файл успешно загружен на {yandexDiskPath}");
        }


        //Метод для конвертации потока бинарных данных в файл
        public async Task SaveFileFromStreamAsync(Stream stream, string localFilePath)
        {
            try
            {
                using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception while saving file from stream: {ex.Message}");
                throw;
            }
        }
        
        public async Task CreateDirectoryAsync(string yandexDiskPath)
        {
            if (string.IsNullOrEmpty(yandexDiskPath))
            {
                throw new ArgumentException("yandexDiskPath cannot be null or empty", nameof(yandexDiskPath));
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Put, $"https://cloud-api.yandex.net/v1/disk/resources?path={Uri.EscapeDataString(yandexDiskPath)}");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);

                var response = await httpClient.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    // Директория уже существует
                    Console.WriteLine($"Directory already exists or conflict occurred: {yandexDiskPath}");
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine($"Directory created successfully: {yandexDiskPath}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HttpRequestException during directory creation: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during directory creation: {ex.Message}");
                throw;
            }
        }

    }
}
