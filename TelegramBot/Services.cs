using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramBot
{
    public class Services
    {
        public static string GetTokenFromFile()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), ".telegram_bot_token");
            return File.Exists(filePath) ? File.ReadAllText(filePath).Trim() : throw new Exception("Файл с токеном не найден!"); ;
        }

        public static string GetConnectionString()
        {
            // Настройка конфигурации
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // Указываем путь к текущей директории
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true); // Загружаем appsettings.json

            // Сборка конфигурации
            IConfigurationRoot configuration = builder.Build();

            // Получение строки соединения по имени
            string connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string DefaultConnection not found in appsettings.json.");
            }

            return connectionString;
        }
    }
}
