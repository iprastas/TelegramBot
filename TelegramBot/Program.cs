using Npgsql;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static TelegramBot.Services;
using TelegramBot;
using Telegram.Bot.Types.ReplyMarkups;
class Program
{
    private static readonly Dictionary<long, (string? text, bool waitingForDate)> userPlanState = new(); // Хранит статус ожидания даты плана
    private static Dictionary<long, bool> waitingForPlanDeletion = new(); // Хранит статус ожидания номера плана
    private static Dictionary<long, List<Plan>> userActivePlans = new();  // Хранит список активных планов

    static void Main()
    {
        string botToken = GetTokenFromFile();
        TelegramBotClient botClient = new TelegramBotClient(botToken);

        using var cts = new CancellationTokenSource();

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            new ReceiverOptions { 
                AllowedUpdates = new[] {
                    UpdateType.Message,
                    UpdateType.CallbackQuery // Inline кнопки
                }
            },
            cts.Token
        );

        Console.WriteLine("");
        Console.ReadLine();
        cts.Cancel();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken token)
    {
        try
        {
            switch (update.Type) 
            {
                case UpdateType.Message:
                    {
                        if (update.Message is not { } message || message.Text is not { })
                        {
                            long Id = update.Message.Chat.Id;
                            Console.WriteLine($"Получено сообщение от {Id}");
                            await bot.SendMessage(Id, "Извини, я понимаю только текстовые сообщения :(", cancellationToken: token);

                            return;
                        }

                        long chatId = message.Chat.Id;
                        string usname = update.Message.Chat.Username;
                        string messageText = message.Text;

                        Console.WriteLine($"[LOG] Получено сообщение: {messageText} от {usname}({chatId})");

                        if (userPlanState.TryGetValue(chatId, out var state)) // проверка статуса пользователя на ожидание получения плана
                        {
                            if (!state.waitingForDate) //получение текста плана
                            {
                                userPlanState[chatId] = (messageText, true);
                                await bot.SendMessage(chatId, $"📅 Теперь введи дату и время (формат: дд.мм.гггг 14:30) или просто дату:", cancellationToken: token);
                                return;
                            }
                            else // получение даты
                            {
                                if (DateTime.TryParseExact(messageText, "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime planDateTime)) // получение даты плана 
                                {
                                    if (SavePlan(chatId, state.text, planDateTime))
                                    {
                                        await bot.SendMessage(chatId, $"✅ План сохранен: {state.text} на {planDateTime}", cancellationToken: token);
                                        userPlanState.Remove(chatId);

                                        await Reminder(bot, token); // добавить напоминание 
                                        return;
                                    }
                                    else
                                    {
                                        await bot.SendMessage(chatId, $"Произошла ошибка, план не сохранен. Попробуйте использовать команду заново.", cancellationToken: token);
                                        userPlanState.Remove(chatId);
                                        return;
                                    }
                                }
                                else
                                {
                                    await bot.SendMessage(chatId, "Неправильный формат даты. Попробуй снова в формате дд.мм.гггг 14:30", cancellationToken: token);
                                    return;
                                }
                            }
                        }
                        if (waitingForPlanDeletion.TryGetValue(chatId, out bool waiting) && waiting) // проверка статуса пользователя на ожидание удаления плана
                        {
                            if (int.TryParse(messageText, out int planIndex) && planIndex > 0 && userActivePlans.TryGetValue(chatId, out var planList) && planIndex <= planList.Count())
                            {
                                var planToDelete = planList[planIndex - 1];

                                await bot.SendMessage(chatId, $"Подтвердите удаление плана:", cancellationToken: token);
                                var inlineKeyboard = new InlineKeyboardMarkup( // inline клавиатура
                                    new List<InlineKeyboardButton[]>()
                                    {
                                        new InlineKeyboardButton[]
                                        {
                                            InlineKeyboardButton.WithCallbackData("Удалить", "deleteplan"),
                                            InlineKeyboardButton.WithCallbackData("Изменить номер", "othernum"),
                                        },
                                        new InlineKeyboardButton[]
                                        {
                                            InlineKeyboardButton.WithCallbackData("Отменить", "cancel")
                                        } 
                                    });
                                await bot.SendMessage(chatId, $"{planIndex}. {planList[planIndex - 1].TextPlan}", 
                                    cancellationToken: token, replyMarkup: inlineKeyboard);

                                return;
                            }
                            else
                            {
                                await bot.SendMessage(chatId, "Неправильный формат. Попробуй снова отправить номер.", cancellationToken: token);
                                return;
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

                        return;
                    }
                case UpdateType.CallbackQuery:
                    {
                        var callbackQuery = update.CallbackQuery;
                        var user = callbackQuery.From;

                        Console.WriteLine($"[LOG] {user.Username} ({user.Id}) нажал на кнопку: {callbackQuery.Data}");

                        long chatId = callbackQuery.Message.Chat.Id;

                        switch(callbackQuery.Data)
                        {
                            case "deleteplan":
                                {
                                    await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: token);

                                    var planList = userActivePlans[chatId];
                                    int planIndex = int.Parse(callbackQuery.Message.Text.Split('.')[0]);
                                    var planToDelete = planList[planIndex - 1];

                                    if (DeletePlan(chatId, planToDelete))
                                    {
                                        await bot.SendMessage(chatId, $"✅ План удален.", cancellationToken: token);
                                        waitingForPlanDeletion.Remove(chatId);
                                        userActivePlans.Remove(chatId);

                                        return;
                                    }
                                    else
                                    {
                                        await bot.SendMessage(chatId, $"Произошла ошибка, план не удален. Попробуйте использовать команду заново.", cancellationToken: token);
                                        userPlanState.Remove(chatId);
                                        return;
                                    }
                                }
                            case "othernum":
                                {
                                    await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: token);

                                    await bot.SendMessage(chatId, $"✏ Введите новый номер плана для удаления:", cancellationToken: token);

                                    return;
                                }
                            case "cancel":
                                {
                                    await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: token);
                                    waitingForPlanDeletion.Remove(chatId);
                                    userActivePlans.Remove(chatId);

                                    await bot.SendMessage(chatId, $"Операция удаления отменена.", cancellationToken: token);

                                    return;
                                }
                        }
                        return;
                    }
            }
        }
        catch (Exception ex) 
        {
            Console.WriteLine(ex.ToString());
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
                    "\n/deleteplan - Удалить план" +
                    "\n/myplans - Посмотреть мои планы", cancellationToken: token);
                break;

            case "/addplan":
                userPlanState[chatId] = (null, false);
                await bot.SendMessage(chatId, "📝 Напиши свой план", cancellationToken: token);
                break;

            case "/myplans":
                await GetAllPlans(bot, chatId, token);
                break;

            case "/deleteplan":
                waitingForPlanDeletion[chatId] = true;

                await GetAllPlans(bot, chatId, token);
                await bot.SendMessage(chatId, "\n✏ Введите номер плана для удаления:", cancellationToken: token);
                break;

            default:
                await bot.SendMessage(chatId, "Неизвестная команда. Введи /help для списка команд.", cancellationToken: token);
                break;
        }
    }

    static bool SavePlan(long chatId, string planText, DateTime planDateTime)
    {
        using NpgsqlConnection conn = new(GetConnectionString());
        conn.Open();
        NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"insert into public.plans(user_id, plan_text, plan_date) values({chatId}, \'{planText}\', \'{planDateTime}\');";

        try
        {
            cmd.ExecuteNonQuery();

            cmd.Dispose();
            conn.Close();

            Console.WriteLine($"[LOG] Сохранили план от {chatId}: {planText} на {planDateTime}");

            return true;
        }
        catch (Exception ex) 
        {
            Console.WriteLine($"[LOG] Не смогли сохранить план от {chatId} по причине: {ex}");
            return false;
        }
    }

    static async Task GetAllPlans(ITelegramBotClient bot, long chatId, CancellationToken token)
    {
        using NpgsqlConnection conn = new(GetConnectionString());
        conn.Open();

        NpgsqlCommand cnt = conn.CreateCommand(); 
        cnt.CommandText = $"SELECT count(*) FROM public.plans where user_id = {chatId} "; //and plan_date>=current_timestamp
        int plansCount = 0;

        NpgsqlDataReader counter = cnt.ExecuteReader();
        while (counter.Read())
        {
            if (!counter.IsDBNull(0))
                plansCount = counter.GetInt32(0);
        }
        cnt.Dispose();
        conn.Close();

        if (plansCount == 0)
        {
            if (waitingForPlanDeletion[chatId])
            {
                await bot.SendMessage(chatId, "У вас нет актуальных планов для удаления.", cancellationToken: token);
                waitingForPlanDeletion.Remove(chatId);
            }
            else
            {
                await bot.SendMessage(chatId, "Актуальных планов еще нет. Самое время их записать!", cancellationToken: token);
            }
            return;
        }
        
        String plans = "Твои планы: \n";
        int ind = 1;
        List<Plan> PlanList = new();

        conn.Open();
        NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"select id, plan_date, plan_text from public.plans where user_id = {chatId};"; // and plan_date>=current_timestamp

        NpgsqlDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Plan plan = new Plan();
            if (!reader.IsDBNull(0))
                plan.Id = reader.GetInt32(0);
            if (!reader.IsDBNull(1))
                plan.DateTimePlan = reader.GetDateTime(1);
            if (!reader.IsDBNull(2))
                plan.TextPlan = reader.GetString(2);

            plans += $"{ind}. {plan.DateTimePlan} - {plan.TextPlan}. \n";
            PlanList.Add(plan);
            ind++;
        }
        userActivePlans[chatId] = PlanList;

        cmd.Dispose();
        conn.Close();

        await bot.SendMessage(chatId, plans, cancellationToken: token);
    }

    static bool DeletePlan(long chatId, Plan plan)
    {
        using NpgsqlConnection conn = new(GetConnectionString());
        conn.Open();
        NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"delete from public.plans where id = {plan.Id};";

        try
        {
            cmd.ExecuteNonQuery();

            cmd.Dispose();
            conn.Close();

            Console.WriteLine($"[LOG] Удалили план от {chatId}\n\n");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LOG] Не смогли удалить план от {chatId} по причине: {ex}\n\n");
            return false;
        }
    }

    static async Task Reminder(ITelegramBotClient bot, CancellationToken token) 
    {
        while (true)
        {
            using NpgsqlConnection conn = new(GetConnectionString());
            conn.Open();
            NpgsqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT user_id, plan_text FROM plans WHERE plan_date = date_trunc('minute', NOW());";

            NpgsqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                long chatId = reader.GetInt64(0);
                string planText = reader.GetString(1);

                var imageUrl = "https://cataas.com/cat"; // случайный котик

                using var httpClient = new HttpClient();
                var imageStream = await httpClient.GetStreamAsync(imageUrl);

                await bot.SendPhoto(
                    chatId: chatId,
                    photo: new InputFileStream(imageStream, "cat.jpg"),
                    caption: $"🔔 Напоминание: {planText} 🐱",
                    cancellationToken: token
                );
            }
            await Task.Delay(TimeSpan.FromMinutes(1), token); // Проверяем каждые 60 сек
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken token)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}

