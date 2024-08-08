using System.IO;
using System.Threading.Tasks;

public interface IDiskService
{
    /// <summary>
    /// Загружает файл на диск.
    /// </summary>
    /// <param name="onDiscPath">Путь к файлу на диске.</param>
    /// <param name="fileStream">Поток, содержащий данные файла для загрузки.</param>
    Task UploadFileAsync(string onDiscPath, Stream fileStream);

    /// <summary>
    /// Загружает файл с диска.
    /// </summary>
    /// <param name="onDiscPath">Путь к файлу на диске.</param>
    /// <returns>Поток, содержащий данные файла.</returns>
    Task<Stream> DownloadFileAsync(string onDiscPath);

    /// <summary>
    /// Сохраняет файл из потока в локальный файл.
    /// </summary>
    /// <param name="stream">Поток, содержащий данные файла.</param>
    /// <param name="localFilePath">Локальный путь для сохранения файла.</param>
    Task SaveFileFromStreamAsync(Stream stream, string localFilePath);

    /// <summary>
    /// Создает директорию на диске.
    /// </summary>
    /// <param name="path">Путь к директории, которую нужно создать.</param>
    Task CreateDirectoryAsync(string path);
}
