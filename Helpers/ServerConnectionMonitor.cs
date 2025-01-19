﻿using Microsoft.Extensions.Configuration;
using Renci.SshNet;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramVPNBot.Services;

namespace TelegramVPNBot.Helpers
{
    public class ServerConnectionMonitor
    {
        private readonly string _serverIp;
        private readonly string _serverUser;
        private readonly string _serverPassword;
        private readonly long _adminChatId;
        private readonly ITelegramBotClient _botClient;

        public ServerConnectionMonitor(ITelegramBotClient botClient)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            _serverIp = configuration.GetValue<string>("Server:Ip");
            _serverUser = configuration.GetValue<string>("Server:User");
            _serverPassword = configuration.GetValue<string>("Server:Password");

            _adminChatId = configuration.GetValue<long>("Telegram:OwnerId");

            _botClient = botClient;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("ServerConnectionMonitor started.");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorAllKeysAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during monitoring: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            }

            Console.WriteLine("ServerConnectionMonitor stopped.");
        }

        private async Task MonitorAllKeysAsync()
        {
            var keys = await OutlineVpnService.GetKeysAsync();

            var inlineKeyboard = new InlineKeyboardMarkup(
                keys.Select(key => new[]
                {
                    new InlineKeyboardButton($"Ban {key.id}") { CallbackData = $"ban:{key.id}" }
                })
            );

            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"📊 *Connection Report*");
            messageBuilder.AppendLine($"Generated: {DateTime.Now:g}");
            messageBuilder.AppendLine();

            using (var client = new SshClient(_serverIp, _serverUser, _serverPassword))
            {
                try
                {
                    client.Connect();

                    foreach (var key in keys)
                    {
                        string command =
                            $"netstat -anp | grep {key.port} | grep ESTABLISHED | awk '{{print $5}}' | cut -d: -f1 | sort | uniq";
                        var result = client.CreateCommand(command).Execute();

                        var connectedIps = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                        var usageKey = await OutlineVpnService.GetUsageByKeyIdAsync(key.id);

                        messageBuilder.AppendLine($"🔑 *Key ID*: `{key.id}`");
                        messageBuilder.AppendLine($"📛 *Name*: `{key.name}`");
                        messageBuilder.AppendLine($"📟 *Port*: `{key.port}`");
                        messageBuilder.AppendLine($"📊 *Usage*: `{string.Format("{0:F2}", usageKey / (double)(1024 * 1024 * 1024))} GB`");
                        messageBuilder.AppendLine($"🌐 *Active Connections*: `{connectedIps.Length}`");

                        if (connectedIps.Length > 0)
                        {
                            messageBuilder.AppendLine("🌍 *Connected IPs*:");
                            messageBuilder.AppendLine("```");
                            foreach (var ip in connectedIps)
                            {
                                messageBuilder.AppendLine(ip);
                            }
                            messageBuilder.AppendLine("```");
                        }
                        else
                        {
                            messageBuilder.AppendLine("No active connections.");
                        }
                        messageBuilder.AppendLine();
                    }

                    client.Disconnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while connecting to Outline server: {ex.Message}");
                    messageBuilder.AppendLine($"❌ Error during monitoring: {ex.Message}");
                }
            }

            try
            {
                var finalMessage = messageBuilder.ToString();
                await _botClient.SendMessage(
                    chatId: _adminChatId,
                    text: finalMessage,
                    replyMarkup: inlineKeyboard,
                    parseMode: ParseMode.Markdown
                );

                Console.WriteLine("Connection report successfully sent to Telegram.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while sending the report to Telegram: {ex.Message}");
            }
        }
    }
}