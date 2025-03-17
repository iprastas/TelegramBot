using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class Program
{
    static string GetTokenFromFile()
    {
        string filePath = Path.Combine(AppContext.BaseDirectory, ".telegram_bot_token");
        return File.Exists(filePath) ? File.ReadAllText(filePath).Trim() : throw new Exception("Файл с токеном не найден!"); ;
    }

    static async Task Main()
    {
        string botToken = GetTokenFromFile();
        TelegramBotClient botClient = new TelegramBotClient(botToken);

        using var cts = new CancellationTokenSource();

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
            cts.Token
        );

        Console.WriteLine("");
        Console.ReadLine();
        cts.Cancel();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken token)
    {
        if (update.Message is not { } message || message.Text is not { }) 
        {
            long Id = update.Message.Chat.Id;
            Console.WriteLine($"Получено сообщение от {Id}");
            await bot.SendMessage(Id, "Извини, я понимаю только текстовые сообщения :(", cancellationToken: token);
            
            return;
        }

        long chatId = message.Chat.Id;
        string messageText = message.Text;

        Console.WriteLine($"Получено сообщение: {messageText} от {chatId}");

        if (messageText.StartsWith("/"))
        {
            await HandleCommandAsync(bot, chatId, messageText, token);
        }
        else
        {
            await bot.SendMessage(chatId, $"Ты написал: {messageText}", cancellationToken: token);
        }
    }

    static async Task HandleCommandAsync(ITelegramBotClient bot, long chatId, string command, CancellationToken token)
    {
        switch (command.Split(' ')[0])
        {
            case "/start":
                await bot.SendMessage(chatId, "Привет! Я твой бот. Введи /help, чтобы узнать команды.", cancellationToken: token);
                break;

            case "/help":
                await bot.SendMessage(chatId, "Доступные команды:" +
                    "\n/start - Запуск бота" +
                    "\n/help - Помощь" +
                    "\n/addplan - Добавить план" +
                    "\n/myplan - Посмотреть мои планы", cancellationToken: token);
                break;

            case "/addplan":
                await bot.SendMessage(chatId, "Добавление планов еще в разработке, пока что держите их у себя в голове или на листочке...", cancellationToken: token);
                break; //Чтобы добавить план, напиши команду так:\n/addPlan Покупка продуктов

            case "/myplans":
                await bot.SendMessage(chatId, "Твои планы: " +
                    "\n Пока пусто, скоро будут, обещаю", cancellationToken: token);
                break;

            default:
                await bot.SendMessage(chatId, "Неизвестная команда. Введи /help для списка команд.", cancellationToken: token);
                break;
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken token)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}

