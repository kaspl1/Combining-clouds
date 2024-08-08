using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace DISKUSING
{
    public class MainController
    {
        private readonly HttpClient httpClient;
        private readonly HttpListener httpListener;
        private readonly DropboxService dropboxService;
        private readonly GoogleDriveService googleDriveService;
        private readonly YandexDiskService yandexDiskService;
        //При создании объекта MainContorller происходит инициализация всех необходимых сервисов
        public MainController(string prefix)
        {
            httpClient = new HttpClient();
            httpListener = new HttpListener();
            httpListener.Prefixes.Add(prefix);
            dropboxService = new DropboxService(
                ConfigurationManager.AppSettings["DropBoxAppSecret"],
                ConfigurationManager.AppSettings["DropboxAppKey"],
                ConfigurationManager.AppSettings["DropBoxRedirectUri"]);
            googleDriveService = new GoogleDriveService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "client_secret.json"));
            yandexDiskService = new YandexDiskService(
                httpClient,
                ConfigurationManager.AppSettings["YandexDiskAccessToken"]);
        }
        //Запуск прослушивания HTTP запросов
        public async Task StartAsync()
        {
            httpListener.Start();
            Console.WriteLine("Service started and listening for requests...");

            while (true)
            {
                try
                {
                    var context = await httpListener.GetContextAsync();
                    await ProcessRequestAsync(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred while processing requests: {ex.Message}");
                }
            }
        }
        //Метод для обработки полученных запросов
        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            string responseData = string.Empty;
            byte[] buffer;

            try
            {
                switch (request.HttpMethod)
                {
                    case "GET":
                        responseData = await HandleGetRequestAsync(request);
                        break;
                    case "POST":
                        responseData = await HandlePostRequestAsync(request);
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        responseData = "Method not allowed";
                        break;
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                responseData = $"An error occurred: {ex.Message}";
            }
            finally
            {
                buffer = Encoding.UTF8.GetBytes(responseData);
                response.ContentLength64 = buffer.Length;
                using (var output = response.OutputStream)
                {
                    await output.WriteAsync(buffer, 0, buffer.Length);
                }
                response.Close();
            }
        }
        //Метод для обработки GET запросов
        private async Task<string> HandleGetRequestAsync(HttpListenerRequest request)
        {
            string directoryPath = ConfigurationManager.AppSettings["DirectoryPathToDownload"];
            string responseMessage = "GET request received\n";
            var queryParams = HttpUtility.ParseQueryString(request.Url.Query);
            string name = queryParams["name"];
            string path = queryParams["path"];
            bool getFromDB = queryParams["getFromDB"] == "true";
            bool getFromGD = queryParams["getFromGD"] == "true";
            bool getFromYD = queryParams["getFromYD"] == "true";
            //Последовательная переадресация запроса на необходимые сервисы
            if (getFromDB)
            {
                responseMessage += await DownloadFileFromServiceAsync(dropboxService, path, directoryPath, "DB_", name);
            }
            if (getFromGD)
            {
                responseMessage += await DownloadFileFromServiceAsync(googleDriveService, path, directoryPath, "GD_", name);
            }
            if (getFromYD)
            {
                responseMessage += await DownloadFileFromServiceAsync(yandexDiskService, path, directoryPath, "YD_", name);
            }

            return responseMessage;
        }
        //Метод для обработки POST запросов
        private async Task<string> HandlePostRequestAsync(HttpListenerRequest request)
        {
            string responseMessage = "POST request received\n";
            var queryParams = HttpUtility.ParseQueryString(request.Url.Query);
            string name = queryParams["name"];
            string path = queryParams["path"];
            bool postToDB = queryParams["postToDB"] == "true";
            bool postToGD = queryParams["postToGD"] == "true";
            bool postToYD = queryParams["postToYD"] == "true";
            bool createInDB = queryParams["createInDB"] == "true";
            bool createInGD = queryParams["createInGD"] == "true";
            bool createInYD = queryParams["createInYD"] == "true";

            //Создание директорий на дисках
            if (createInDB)
            {
                await CreateFolderInServiceAsync(dropboxService, path, "Dropbox");
            }
            if (createInGD)
            {
                await CreateFolderInServiceAsync(googleDriveService, path, "Google Drive");
            }
            if (createInYD)
            {
                await CreateFolderInServiceAsync(yandexDiskService, path, "Yandex Disk");
            }
            //Загрузка данных на диски
            using (var memoryStream = new MemoryStream())
            {
                await request.InputStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0; 

                if (postToDB)
                {
                    responseMessage += await UploadFileToServiceAsync(dropboxService, path, memoryStream, name, "Dropbox");
                }

                if (postToGD)
                {
                    responseMessage += await UploadFileToServiceAsync(googleDriveService, path, memoryStream, name, "Google Drive");
                }

                if (postToYD)
                {
                    responseMessage += await UploadFileToServiceAsync(yandexDiskService, path, memoryStream, name, "Yandex Disk");
                }
            }

            return responseMessage;
        }
        //Мето для скачивания файла с дисков
        private async Task<string> DownloadFileFromServiceAsync(IDiskService service, string path, string directoryPath, string prefix, string name)
        {
            try
            {
                var stream = await service.DownloadFileAsync(path);
                await service.SaveFileFromStreamAsync(stream, Path.Combine(directoryPath, prefix + name));
                return $"File {name} downloaded successfully from {prefix}.\n";
            }
            catch (Exception ex)
            {
                return $"An error occurred while downloading file from {prefix}: {ex.Message}\n";
            }
        }
        //Метод для загрузки файла на диски
        private async Task<string> UploadFileToServiceAsync(IDiskService service, string path, MemoryStream fileStream, string name, string serviceName)
        {
            try
            {
                var uploadStream = new MemoryStream(fileStream.ToArray());
                await service.UploadFileAsync(path, uploadStream);
                return $"File {name} uploaded successfully to {serviceName}.\n";
            }
            catch (Exception ex)
            {
                return $"An error occurred while uploading file to {serviceName}: {ex.Message}\n";
            }
        }
        //Метод для создания директории на дисках
        private async Task CreateFolderInServiceAsync(IDiskService service, string path, string serviceName)
        {
            try
            {
                await service.CreateDirectoryAsync(path);
                Console.WriteLine($"Directory created successfully on {serviceName}: {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while creating directory on {serviceName}: {ex.Message}");
            }
        }
    }
}
