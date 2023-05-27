using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramShopBot
{
    internal class AccountMenu : MenuWithAbilityToReturn
    {
        private const string productOrdersButtonText = "🛒Заказы🛒";
        private const string aboutAccountButtonText = "🔐О аккаунте🔐";
        private const string topUpBalanceButtonText = "💵Пополнить баланс💵";
        private const string deleteAccountButtonText = "🚫Удалить аккаунт🚫";
        private const string returnToMainAccountMenuButtonText = "⬆️Вернуться в меню аккаунта⬆️";


        public AccountMenu(string name) : base(name) { }
        


        public override async Task CreateMenuGovernMessageAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            int controlMessageId = 0;

            using(DatabaseContext db = new DatabaseContext())
            {
                ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                shopClient.State = ShopClientState.AccountMenuChoising;
                controlMessageId = (int)shopClient.ControlMessageId;

                await db.SaveChangesAsync();
            }

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: controlMessageId,
                text: "В этом меню вы можете просмотреть свои покупки, а также пополнить баланс",
                replyMarkup: CreateAccountMenuKeyboard());
        }
        private InlineKeyboardMarkup CreateAccountMenuKeyboard()
        {
            InlineKeyboardMarkup keyboard = new(
                new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(productOrdersButtonText),
                        InlineKeyboardButton.WithCallbackData(aboutAccountButtonText)
                    },

                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(topUpBalanceButtonText),
                        InlineKeyboardButton.WithCallbackData(deleteAccountButtonText)
                    },

                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(returnButtonText)
                    }
                });

            return keyboard;
        }

        public async Task AccountMenuCallBackHandlerAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                switch (update.CallbackQuery.Data)
                {
                    case productOrdersButtonText:
                        await CreateProductOrdersMenuGovernMessageAsync(botClient, update, chatId);
                        break;

                    case topUpBalanceButtonText:
                        await CreateTopUpBalanceMenuGovernMessageAsync(botClient, update, chatId);
                        break;

                    case deleteAccountButtonText:
                        await CreateDeleteAccountWarningMessageAsync(botClient, update, chatId);
                        break;

                    case aboutAccountButtonText:
                        await CreateAboutAccountMessageAsync(botClient, update, chatId);
                        break;

                    case returnButtonText:
                        await returnToMainMenuAction.Invoke(botClient, update, chatId);
                        break;
                }
            }
        }

        

        private async Task CreateTopUpBalanceMenuGovernMessageAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            int controlMessageId = 0;

            using (DatabaseContext db = new DatabaseContext())
            {
                ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                shopClient.State = ShopClientState.EnteringTheDepositAmount;
                controlMessageId = (int)shopClient.ControlMessageId;

                await db.SaveChangesAsync();
            }

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: controlMessageId,
                text: "Введите сумму пополнения:",
                replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(returnButtonText)));
        }

        public async Task TopUpBalanceMenuCallBackHanlerAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery.Data == returnButtonText)
            {        
                 await CreateMenuGovernMessageAsync(botClient, update, chatId);
            }
            else if (update.Type == UpdateType.Message)
            {
                if (int.TryParse(update.Message.Text, out int depositAmount))
                {
                    using (DatabaseContext db = new DatabaseContext())
                    {
                        ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId); 
                        
                        shopClient.Balance += depositAmount;

                        await db.SaveChangesAsync();
                    }

                    await CreateAfterTopUpBalanceMessageAsync(botClient, update, chatId, depositAmount);
                }
            }
        }


        private async Task CreateAfterTopUpBalanceMessageAsync(ITelegramBotClient botClient, Update update, long chatId, int depositAmount)
        {
            using (DatabaseContext db = new DatabaseContext())
            {
                ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                int controlMessageId = (int)shopClient.ControlMessageId;

                try
                {
                    await botClient.DeleteMessageAsync(
                    chatId: chatId,
                    messageId: controlMessageId);
                }
                catch (Exception)
                {
                    await botClient.EditMessageTextAsync(
                        chatId: chatId,
                        messageId: controlMessageId,
                        text: "УДАЛЕНО",
                        replyMarkup: InlineKeyboardMarkup.Empty());
                }

                Message newMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: CreateAfterTopUpBalanceMessageText(shopClient, depositAmount),
                    replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(returnButtonText)));

                shopClient.State = ShopClientState.AfterTopUpBalance;
                shopClient.ControlMessageId = newMessage.MessageId;

                await db.SaveChangesAsync();
            }
        }
        private string CreateAfterTopUpBalanceMessageText(ShopClient shopClient, int depositAmount)
        {
            string text = $"Баланс пополнен на: {depositAmount}₽ \n" +
                $"Текущий баланс: {shopClient.Balance}₽";

            return text;
        }

        public async Task AfterTopUpBalanceMessageCallBackHandlerAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery.Data == returnButtonText)
            {
                await CreateMenuGovernMessageAsync(botClient, update, chatId);
            }
        }



        private async Task CreateProductOrdersMenuGovernMessageAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            ShopClient shopClient = new();
            int controlMessageId = 0;

            using (DatabaseContext db = new DatabaseContext())
            {
                shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                db.ShopClients.Where(sc => sc.ChatId == chatId)
                    .Include(sc1 => sc1.ProductOrders)
                    .ThenInclude(o1 => o1.Product)
                    .Include(sc2 => sc2.ProductOrders)
                    .ThenInclude(o2 => o2.ProductType)
                    .Include(sc3 => sc3.ProductOrders)
                    .ThenInclude(o3 => o3.Category)
                    .ToList();

                shopClient.State = ShopClientState.ProductOrderChoising;
                controlMessageId = (int)shopClient.ControlMessageId;

                await db.SaveChangesAsync();
            }

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: controlMessageId,
                text: CreateProductOrdersMenuMessageText(shopClient),
                replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(returnButtonText)));
        }
        private string CreateProductOrdersMenuMessageText(ShopClient shopClient)
        {
            StringBuilder text = new();

            text.Append("🗂Ваша история заказов🗂\n\n\n");

            for (int i = 0; i < shopClient.ProductOrders.Count; i++)
            {
                ProductOrder order = shopClient.ProductOrders[i];
                text.Append($"{i+1}. 🕐Дата заказа: {order.TimeOfCreating.Value.Date.Day}/{order.TimeOfCreating.Value.Date.Month}/{order.TimeOfCreating.Value.Date.Year} \n " +
                    $"📕Категория: {order.Category.Name} \n" +
                    $"💬Название товара: {order.ProductType.Name} \n" +
                    $"⏳Время подписки: {order.Product.SubscribeTime} \n\n");
            }

            text.Append("\nЧтобы просмотреть подробности товара, напишите его номер");

            return text.ToString();
        }

        public async Task ProductOrdersMenuCallBackHandlerAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery.Data == returnButtonText)
            {
                await CreateMenuGovernMessageAsync(botClient, update, chatId);
            }
            else if (update.Type == UpdateType.Message)
            {
                if (int.TryParse(update.Message.Text, out int numOfProductOrder))
                {
                    ShopClient shopClient = new();
                         
                    using (DatabaseContext db = new DatabaseContext())
                    {
                        shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                        db.ShopClients.Where(sc => sc.ChatId == chatId)
                            .Include(sc1 => sc1.ProductOrders)
                            .ThenInclude(o1 => o1.Product)
                            .Include(sc2 => sc2.ProductOrders)
                            .ThenInclude(o2 => o2.ProductType)
                            .Include(sc3 => sc3.ProductOrders)
                            .ThenInclude(o3 => o3.Category)
                            .ToList();

                        if (shopClient.ProductOrders.Count < numOfProductOrder)
                        {
                            shopClient.State = ShopClientState.ProductOrderInfoReading;
                        }

                        await db.SaveChangesAsync();
                    }

                    if (shopClient.ProductOrders.Count < numOfProductOrder)
                    {
                        await CreateProductOrderInfoMessageAsync(botClient, chatId, (int)shopClient.ControlMessageId, shopClient.ProductOrders[numOfProductOrder - 1]);
                    }
                }
            }
        }



        private async Task CreateProductOrderInfoMessageAsync(ITelegramBotClient botClient, long chatId, int controlMessageId, ProductOrder productOrder)
        {
            await botClient.EditMessageTextAsync(
               chatId: chatId,
               messageId: controlMessageId,
               text: CreateProductOrderInfoText(productOrder),
               replyMarkup: CreateProductOrderInfoMessageKeyboard());
        }
        private string CreateProductOrderInfoText(ProductOrder productOrder)
        {
            string text = $"📕Категория: {productOrder.Category.Name} \n" +
                $"💬Имя товара: {productOrder.ProductType.Name} \n" +
                $"⏳Время подписки: {productOrder.Product.SubscribeTime} \n" +
                $"💵Цена: {productOrder.Product.Price} \n\n" +
                $"💬Товар:\n" +
                $"✉️Почта: {productOrder.Product.Mail} \n" +
                $"🔐Пароль от почты: {productOrder.Product.MailPassword} \n " +
                $"🔐Пароль от аккаунта: {productOrder.Product.AccountPassword}";

            return text;
        }
        private InlineKeyboardMarkup CreateProductOrderInfoMessageKeyboard()
        {
            InlineKeyboardMarkup keyboard = new(
                new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(returnButtonText),
                        InlineKeyboardButton.WithCallbackData(returnToMainAccountMenuButtonText)
                    }
                });

            return keyboard;
        }

        public async Task ProductOrderInfoMessageCallBackHandlerAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                if (update.CallbackQuery.Data == returnToMainAccountMenuButtonText)
                {
                    await CreateMenuGovernMessageAsync(botClient, update, chatId);
                }
                else if (update.CallbackQuery.Data 
                    == returnButtonText)
                {
                    await CreateProductOrdersMenuGovernMessageAsync(botClient, update, chatId);
                }
            }
        }
        


        private async Task CreateDeleteAccountWarningMessageAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            int controlMessageId = 0;

            using (DatabaseContext db = new DatabaseContext())
            {
                ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                shopClient.State = ShopClientState.DeleteAccountWarningMessageReading;
                controlMessageId = (int)shopClient.ControlMessageId;

                await db.SaveChangesAsync();
            }

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: controlMessageId,
                text: CreateWarningMessageText(),
                replyMarkup: CreateWarningMessageKeyboard());
        }
        private string CreateWarningMessageText()
        {
            string text = "Вы уверены что хотите удалить свой аккаунт ? \n" +
                "Удаление аккаунта приведет: \n" +
                "♦️ к удалению всех покупок \n" +
                "♦️ к обнулению баланса \n" +
                "♦️ к удалению информации о вас в бд \n" +
                "♦️ к добровольной передаче души к разработчику этого бота (а он еще тот дьявол)";

            return text;
        }
        private InlineKeyboardMarkup CreateWarningMessageKeyboard()
        {
            InlineKeyboardMarkup keyboard = new(
                new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(deleteAccountButtonText),
                        InlineKeyboardButton.WithCallbackData(returnButtonText)
                    }
                });

            return keyboard;
        }

        public async Task DeleteAccountWarningMessageCallBackHandlerAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                if (update.CallbackQuery.Data == returnButtonText)
                {
                    await CreateMenuGovernMessageAsync(botClient, update, chatId);
                }
                else if(update.CallbackQuery.Data == deleteAccountButtonText)
                {
                    int controlMessageId = 0;

                    using (DatabaseContext db = new DatabaseContext())
                    {
                        ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                        db.ShopClients.Where(sc => sc.ChatId == chatId)
                            .Include(sc1 => sc1.ProductOrders)
                            .ThenInclude(o1 => o1.Product).ToList();

                        controlMessageId = (int)shopClient.ControlMessageId;

                        db.Products.RemoveRange(shopClient.ProductOrders.Select(o => o.Product));
                        db.ShopClients.Remove(shopClient);
                      
                        await db.SaveChangesAsync();
                    }

                    await botClient.EditMessageTextAsync(
                        chatId: chatId,
                        messageId: controlMessageId,
                        text: "Для того чтобы запустить бота напишите /start",
                        replyMarkup: InlineKeyboardMarkup.Empty());
                }
            }
        }



        private async Task CreateAboutAccountMessageAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            ShopClient shopClient = new();
            int controlMessageId = 0;
                
            using (DatabaseContext db = new DatabaseContext())
            {
                shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                shopClient.State = ShopClientState.AboutAccountMessageReading;
                controlMessageId = (int)shopClient.ControlMessageId;

                await db.SaveChangesAsync();
            }

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: controlMessageId,
                text: CreateAboutAccountText(shopClient),
                replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(returnButtonText)));
        }
        private string CreateAboutAccountText(ShopClient shopClient)
        {
            string text = $"ChatId: {shopClient.ChatId} \n" +
                $"Баланс: {shopClient.Balance}₽ \n";

            return text;            
        }

        public async Task AboutAccountMessageCallBackHandler(ITelegramBotClient botClient, Update update, long chatId)
        {
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery.Data == returnButtonText)
            {
                await CreateMenuGovernMessageAsync(botClient, update, chatId);
            }
        }
    }
}
