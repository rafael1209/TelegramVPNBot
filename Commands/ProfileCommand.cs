﻿using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramVPNBot.Helpers;
using TelegramVPNBot.Interfaces;
using InputFile = Telegram.Bot.Types.InputFile;

namespace TelegramVPNBot.Commands
{
    public class ProfileCommand(IAuthorizationService authorizationService) : ICommand
    {
        public async Task ExecuteAsync(Update update, ITelegramBotClient botClient)
        {
            if (update.CallbackQuery == null)
                return;

            var userData = update.CallbackQuery.From;
            var user = await authorizationService.GetAuthorizedUserAsync(userData);

            var startMessage = LanguageHelper.GetLocalizedMessage(user.Settings.Language, "ProfileMessage");
            var startImg = LanguageHelper.GetLocalizedMessage(user.Settings.Language, "AccessImg");
            var menuKeys = LanguageHelper.GetLocalizedMessage(user.Settings.Language, "KeyboardProfile").Split('|');
            var status = SubscriptionStatusHelper.GetSubscriptionStatusMessage(user.SubscriptionEndDateUtc, user.Settings.Language);

            var message = string.Format(startMessage,
                user.FullName,
                user.Username,
                user.TelegramId,
                user.CreatedAtUtc.ToString("dd.MM.yyyy"), status,
                user.SubscriptionEndDateUtc?.ToString("dd.MM.yyyy HH:mm") ?? 
                user.CreatedAtUtc.ToString("dd.MM.yyyy HH:mm"));

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    new InlineKeyboardButton(menuKeys[0]) { CallbackData = "subscription" }
                },
                [
                    new InlineKeyboardButton(menuKeys[1]) { CallbackData = "start" }
                ]
            });

            try
            {
                var media = new InputMediaPhoto(new InputFileUrl(startImg))
                {
                    Caption = message,
                    ParseMode = ParseMode.Markdown
                };

                await botClient.EditMessageMedia(
                    chatId: userData.Id,
                    messageId: update.CallbackQuery.Message.MessageId,
                    media: media,
                    replyMarkup: inlineKeyboard
                );
            }
            catch (ApiRequestException ex)
            {
                await botClient.SendPhoto(
                    chatId: user.TelegramId,
                    photo: InputFile.FromUri(startImg),
                    caption: message,
                    replyMarkup: inlineKeyboard,
                    parseMode: ParseMode.Markdown
                );
            }
        }
    }
}
