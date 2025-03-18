using Microsoft.Extensions.Primitives;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class Program
{
    private static readonly Dictionary<long, (string? text, bool waitingForDate)> userPlanState = new();

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

        if (userPlanState.TryGetValue(chatId, out var state)) // проверка статуса пользователя
        {
            if (!state.waitingForDate) //получение текста плана
            {
                userPlanState[chatId] = (messageText, true);
                await bot.SendMessage(chatId, $"📅 Теперь введи дату и время (формат: дд.мм.гггг 14:30) или просто дату:", cancellationToken: token);
                return;
            }
            else
            {
                if (DateTime.TryParse(messageText, out DateTime planDateTime)) // получение даты плана 
                {
                    await SavePlan(chatId, state.text, planDateTime);
                    await bot.SendMessage(chatId, $"✅ План сохранен: {state.text} на {planDateTime}", cancellationToken: token);
                    userPlanState.Remove(chatId);
                }
                else
                {
                    await bot.SendMessage(chatId, "Неправильный формат даты. Попробуй снова в формате дд.мм.гггг 14:30", cancellationToken: token);
                }
            }
        }

        if (messageText.StartsWith("/")) // работа с командами
        {
            await HandleCommandAsync(bot, chatId, messageText, token);
        }
        else
        {
            await bot.SendMessage(chatId, $"К сожалению, я не смогу поддержать диалог с тобой :(", cancellationToken: token);
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
                    "\n/myplans - Посмотреть мои планы", cancellationToken: token);
                break;

            case "/addplan":
                userPlanState[chatId] = (null, false);
                await bot.SendMessage(chatId, "📝 Напиши свой план", cancellationToken: token);
                break;

            case "/myplans":
                await GetAllPlans(bot, chatId, token); // чтение с базы данных
                break;

            default:
                await bot.SendMessage(chatId, "Неизвестная команда. Введи /help для списка команд.", cancellationToken: token);
                break;
        }
    }

    static async Task SavePlan(long chatId, string planText, DateTime planDateTime)
    {


        Console.WriteLine($"[LOG] Сохранили план от {chatId}: {planText} на {planDateTime}");

        //await Reminder(bot) // добавить напоминание 
        await Task.CompletedTask;
    }

    static async Task GetAllPlans(ITelegramBotClient bot, long chatId, CancellationToken token)
    {
        String plans = "Твои планы: \n";

        // чтение с базы данных

        await bot.SendMessage(chatId, plans, cancellationToken: token);
    }

    //async Task Reminder(ITelegramBotClient bot) {}

    static Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken token)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}

