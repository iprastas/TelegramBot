using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

class Program
{
    static string botToken = ;
    static TelegramBotClient botClient = new TelegramBotClient(botToken);

    static async Main()
    {
        Console.WriteLine("Hello, World!");
    }
}

