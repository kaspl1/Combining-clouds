using System;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;

namespace DISKUSING
{
    class Program
    {
        static async Task Main(string[] args)
        {
            
            // Чтение префикса из конфигурации или использования значения по умолчанию
            string prefix = ConfigurationManager.AppSettings["HostUrl"];

            // Создание экземпляра MainController
            var mainController = new MainController(prefix);

            // Запуск сервера
            try
            {
                await mainController.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

        }
    }
}