using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Drive.v3.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DISKUSING;
using System.Threading;
using System.Linq;

public class GoogleDriveService : IDiskService
{
    private readonly DriveService _driveService;

    public GoogleDriveService(string clientSecretFilePath)
    {
        var credential = GetCredentials(clientSecretFilePath).Result;

        _driveService = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "DiscUsing",
        });
    }

    public async Task UploadFileAsync(string onDiscPath, Stream fileStream)
    {
        var (fileName, folderId) = await GetFileNameAndParentFolderIdAsync(onDiscPath);
        var fileMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = fileName,
            Parents = folderId != null ? new List<string> { folderId } : null
        };

        var request = _driveService.Files.Create(fileMetadata, fileStream, GetMimeType(fileName));
        request.Fields = "id";
        await request.UploadAsync();
    }

    public async Task<Stream> DownloadFileAsync(string onDiscPath)
    {
        var (fileName, _) = await GetFileNameAndParentFolderIdAsync(onDiscPath);
        var request = _driveService.Files.List();
        request.Q = $"name='{fileName}'";
        request.Fields = "files(id)";

        var result = await request.ExecuteAsync();
        if (result.Files.Count == 0)
        {
            throw new FileNotFoundException("File not found on Google Drive.");
        }

        var fileId = result.Files[0].Id;
        var getRequest = _driveService.Files.Get(fileId);
        var memoryStream = new MemoryStream();
        await getRequest.DownloadAsync(memoryStream);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task SaveFileFromStreamAsync(Stream stream, string localFilePath)
    {
        using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
        {
            await stream.CopyToAsync(fileStream);
        }
    }

    public async Task CreateDirectoryAsync(string path)
    {
        // Создаем директории по заданному пути
        await GetOrCreateFoldersAsync(path.Trim('/').Split('/'));
    }

    private async Task<UserCredential> GetCredentials(string clientSecretFilePath)
    {
        using (var stream = new FileStream(clientSecretFilePath, FileMode.Open, FileAccess.Read))
        {
            var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
            return await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                new[] { DriveService.Scope.Drive },
                "user",
                CancellationToken.None);
        }
    }

    private string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        string mimeType;

        switch (extension)
        {
            case ".jpg":
            case ".jpeg":
                mimeType = "image/jpeg";
                break;
            case ".png":
                mimeType = "image/png";
                break;
            case ".gif":
                mimeType = "image/gif";
                break;
            case ".bmp":
                mimeType = "image/bmp";
                break;
            case ".tiff":
                mimeType = "image/tiff";
                break;
            case ".pdf":
                mimeType = "application/pdf";
                break;
            case ".doc":
            case ".docx":
                mimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                break;
            case ".xls":
            case ".xlsx":
                mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                break;
            case ".ppt":
            case ".pptx":
                mimeType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                break;
            case ".txt":
                mimeType = "text/plain";
                break;
            case ".csv":
                mimeType = "text/csv";
                break;
            case ".mp3":
                mimeType = "audio/mpeg";
                break;
            case ".wav":
                mimeType = "audio/wav";
                break;
            case ".mp4":
                mimeType = "video/mp4";
                break;
            case ".avi":
                mimeType = "video/x-msvideo";
                break;
            case ".mov":
                mimeType = "video/quicktime";
                break;
            case ".zip":
                mimeType = "application/zip";
                break;
            case ".rar":
                mimeType = "application/x-rar-compressed";
                break;
            case ".7z":
                mimeType = "application/x-7z-compressed";
                break;
            default:
                mimeType = "application/octet-stream";
                break;
        }

        return mimeType;
    }

    private async Task<(string fileName, string folderId)> GetFileNameAndParentFolderIdAsync(string path)
    {
        var parts = path.Trim('/').Split('/');
        if (parts.Length == 0)
        {
            throw new ArgumentException("Invalid path format.", nameof(path));
        }

        string fileName = parts[parts.Length - 1];
        string folderId = await GetOrCreateFoldersAsync(parts.Take(parts.Length - 1).ToArray());

        return (fileName, folderId);
    }

    private async Task<string> GetOrCreateFoldersAsync(IEnumerable<string> folderNames)
    {
        string parentFolderId = null;

        foreach (var folderName in folderNames)
        {
            parentFolderId = await GetOrCreateFolderAsync(folderName, parentFolderId);
        }

        return parentFolderId;
    }

    private async Task<string> GetOrCreateFolderAsync(string folderName, string parentFolderId)
    {
        var request = _driveService.Files.List();
        request.Q = $"mimeType='application/vnd.google-apps.folder' and name='{folderName}'" +
                    $"{(parentFolderId != null ? $" and '{parentFolderId}' in parents" : "")}" +
                    " and trashed=false";
        request.Fields = "files(id)";
        var result = await request.ExecuteAsync();

        if (result.Files.Count > 0)
        {
            return result.Files[0].Id;
        }

        var fileMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = parentFolderId != null ? new List<string> { parentFolderId } : null
        };

        var createRequest = _driveService.Files.Create(fileMetadata);
        createRequest.Fields = "id";
        var folder = await createRequest.ExecuteAsync();
        return folder.Id;
    }
}
