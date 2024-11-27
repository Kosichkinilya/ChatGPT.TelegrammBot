using System;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using RestSharp;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;

class Program
{
    private static string telegramBotToken = "7845318118:AAGs2QGkj22yxGOgElg0EushRpV-CmSt0w0";
    private static string openAiApiKey = "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";
    private static TelegramBotClient botClient;

    static async Task Main(string[] args)
    {
        botClient = new TelegramBotClient(telegramBotToken);

        // Настраиваем обработчик обновлений
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // Получать все типы обновлений
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions
        );

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Bot {me.Username} is running...");
        Console.ReadLine();
    }

    // Обработка обновлений от Telegram
    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { Text: { } messageText } message)
            return;

        long chatId = message.Chat.Id;

        Console.WriteLine($"Received a message from {chatId}: {messageText}");

        // Отправляем запрос к ChatGPT
        string response = await GetResponseFromChatGPT(messageText);

        // Отправляем ответ обратно в Telegram
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: response,
            cancellationToken: cancellationToken
        );
    }

    // Обработка ошибок
    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }

    // Запрос к OpenAI ChatGPT
    static async Task<string> GetResponseFromChatGPT(string userInput)
    {
        var client = new RestClient("https://api.openai.com/v1/chat/completions");
        var request = new RestRequest("https://api.openai.com/v1/chat/completions", Method.Post);

        request.AddHeader("Authorization", $"Bearer {openAiApiKey}");
        request.AddHeader("Content-Type", "application/json");

        // Формируем тело запроса
        var body = new
        {
            model = "gpt-4",
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "user", content = userInput }
            }
        };
        request.AddJsonBody(body);

        // Отправляем запрос и получаем ответ
        var response = await client.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            Console.WriteLine($"Error: {response.StatusCode} - {response.Content}");
            return "Ошибка при обращении к ChatGPT. Попробуйте позже.";
        }

        // Разбираем ответ JSON
        var json = JObject.Parse(response.Content);
        var chatGptResponse = json["choices"]?[0]?["message"]?["content"]?.ToString();

        return chatGptResponse ?? "Не удалось получить ответ от ChatGPT.";
    }
}
