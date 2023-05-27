using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramShopBot
{
    internal class CategoriesMenu : MenuWithAbilityToReturn
    {
        private readonly string goToPaymentButtonText = "Перейти к оплате";
        private readonly string returnToMainMenuButtonText = "⬆️Вернуться в главное меню⬆️";
        private readonly int maxKeyboardLineLenght;



        public CategoriesMenu(string name, int maxKeyboardLineLenght) : base(name)
        {
            this.maxKeyboardLineLenght = maxKeyboardLineLenght;
        }



        public override async Task CreateMenuGovernMessageAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            int controlMessageId = 0;

            using (DatabaseContext db = new DatabaseContext())
            {
                ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                shopClient.State = ShopClientState.CategoryChoising;
                controlMessageId = (int)shopClient.ControlMessageId;

                await db.SaveChangesAsync();
            };

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: controlMessageId,
                text: "Выберите категорию:",
                replyMarkup: CreateCategoriesMenuKeyboard());
        }
        private InlineKeyboardMarkup CreateCategoriesMenuKeyboard()
        {
            List<Category> allCategories = new();

            using (DatabaseContext db = new DatabaseContext())
            {
                db.Categories.Include(c => c.ProductsTypes).ThenInclude(pt => pt.Products).ToList();
                allCategories = db.Categories.Where(c => c.ProductsTypes.Count > 0 && c.ProductsTypes.Where(pt => pt.Products.Count > 0).Select(pts => pts.Products).Count() > 0).ToList();
            }

            List<InlineKeyboardButton[]> keyboardButtons = new();
            for (int i = 0; i < (double)allCategories.Count / maxKeyboardLineLenght; i++)
            {
                List<InlineKeyboardButton> buttonsLine = new();

                for (int j = 0; j < maxKeyboardLineLenght; j++)
                {
                    int buttonNum = i * maxKeyboardLineLenght + j;
                    if (allCategories.Count > buttonNum)
                    {
                        buttonsLine.Add(InlineKeyboardButton.WithCallbackData(allCategories[buttonNum].Name));
                    }
                }

                keyboardButtons.Add(buttonsLine.ToArray());
            }

            keyboardButtons.Add(new InlineKeyboardButton[] { InlineKeyboardButton.WithCallbackData(returnButtonText) });

            InlineKeyboardMarkup keyboard = new(keyboardButtons);
            return keyboard;
        }

        public async Task CategoriesMenuCallBackHanlerAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                if (update.CallbackQuery.Data == returnButtonText)
                {
                    await returnToMainMenuAction.Invoke(botClient, update, chatId);
                }
                else
                {
                    await CreateCategoryMenuGovernMessageAsync(botClient, update, chatId);
                }
            }
        }

        private async Task ReturnToCategoriesMenuAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            using(DatabaseContext db = new DatabaseContext())
            {
                ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);
                db.ShopClients.Where(sc => sc.ChatId == chatId).Include(sc1 => sc1.BeingCreatedProductOrder).ToList();

                db.ProductsOrders.Remove(shopClient.BeingCreatedProductOrder);

                await db.SaveChangesAsync();
            }

            await CreateMenuGovernMessageAsync(botClient, update, chatId);
        }



        private async Task CreateCategoryMenuGovernMessageAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            Category chosenCategory = new Category();
            int controlMessageId = 0;

            using (DatabaseContext db = new DatabaseContext())
            {
                ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                chosenCategory = db.Categories.FirstOrDefault(c => c.Name == update.CallbackQuery.Data);

                db.Categories.Where(c => c.Id == chosenCategory.Id)
                    .Include(c1 => c1.ProductsTypes)
                    .ThenInclude(pt => pt.Products)
                    .ToList();
                

                shopClient.State = ShopClientState.ProductsTypeChoising;
                shopClient.BeingCreatedProductOrder = new ProductOrder();
                shopClient.BeingCreatedProductOrder.Category = chosenCategory;

                controlMessageId = (int)shopClient.ControlMessageId;

                await db.SaveChangesAsync();
            }

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: controlMessageId,
                text: CreateCategoryMessageText(chosenCategory),
                replyMarkup: CreateCategoryKeyboad(chosenCategory));
        }
        private InlineKeyboardMarkup CreateCategoryKeyboad(Category category)
        {
            List<InlineKeyboardButton[]> keyboardButtons = new();

            var productsTypesWithAvailableToBuyProduct = category.ProductsTypes.Where(pt => pt.IsAnyOfProductsAvailableToBuy()).ToList();

            if (category.ProductsTypes != null)
            {
                for (int i = 0; i < (double)productsTypesWithAvailableToBuyProduct.Count / maxKeyboardLineLenght; i++)
                {
                    List<InlineKeyboardButton> buttonsLine = new();

                    for (int j = 0; j < maxKeyboardLineLenght; j++)
                    {
                        int buttonNum = i * maxKeyboardLineLenght + j;
                        if (productsTypesWithAvailableToBuyProduct.Count > buttonNum)
                        {
                            buttonsLine.Add(InlineKeyboardButton.WithCallbackData(productsTypesWithAvailableToBuyProduct[buttonNum].Name));
                        }
                    }

                    keyboardButtons.Add(buttonsLine.ToArray());
                }
            }

            keyboardButtons.Add(new InlineKeyboardButton[] { InlineKeyboardButton.WithCallbackData(returnButtonText) });

            InlineKeyboardMarkup keyboard = new(keyboardButtons);
            return keyboard;
        }
        private string CreateCategoryMessageText(Category category)
        {
            string text =
                $"📕Категория : {category.Name} \n\n" +
                $"📃Описание : {category.Description} \n";

            return text;
        }

        public async Task CategoryMenuCallBackHandlerAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                if (update.CallbackQuery.Data == returnButtonText)
                {
                    await ReturnToCategoriesMenuAsync(botClient, update, chatId);
                }
                else
                {
                    List<IGrouping<string, Product>> availableProducts = new();
                    ProductsType shopClientProductsType = new();

                    using (DatabaseContext db = new DatabaseContext())
                    {
                        string chosenProductsTypeName = update.CallbackQuery.Data;
                        ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                        db.ShopClients
                            .Where(sc => sc.ChatId == shopClient.ChatId)
                            .Include(sc1 => sc1.BeingCreatedProductOrder)
                            .ThenInclude(o => o.Category)
                            .ThenInclude(c => c.ProductsTypes)
                            .ThenInclude(pt => pt.Products)
                            .ToList();

                        shopClient.BeingCreatedProductOrder.ProductType = shopClient.BeingCreatedProductOrder.Category.ProductsTypes.FirstOrDefault(pt => pt.Name == chosenProductsTypeName);

                        shopClientProductsType = shopClient.BeingCreatedProductOrder.ProductType;

                        if (shopClientProductsType.Products != null)
                        {
                            availableProducts = shopClientProductsType.Products.Where(p => p.IsEmployed == false).GroupBy(pt => pt.SubscribeTime).ToList();
                        }

                        await db.SaveChangesAsync();
                    }

                    if (availableProducts.Count == 0)
                    {
                        await CreateNoProductsErrorMessageAsync(botClient, update, chatId);
                    }
                    else
                    {
                        if (availableProducts.Count == 1)
                        {
                            await CreateProductMenuGovernMessageAsync(botClient, update, chatId, shopClientProductsType, availableProducts[0].ElementAt(0));
                        }
                        else
                        {
                            await CreateProductsTypeMenuGovernMessageAsync(botClient, update, chatId, availableProducts);
                        }
                    }
                }
            }
        }

        private async Task ReturnToCategoryMenuAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            ShopClient shopClient;

            using (DatabaseContext db = new DatabaseContext())
            {
                shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                db.ShopClients.Where(sc => sc.ChatId == chatId).Include(sc1 => sc1.BeingCreatedProductOrder).ToList();
                db.ProductsOrders.Where(o => o.Id == shopClient.BeingCreatedProductOrder.Id)
                    .Include(o1 => o1.Product)
                    .Include(o2 => o2.ProductType)
                    .Include(o3 => o3.Category).ThenInclude(c => c.ProductsTypes).ThenInclude(pt => pt.Products).ToList();

                shopClient.BeingCreatedProductOrder.ProductType = null;

                if (shopClient.BeingCreatedProductOrder.Product != null)
                {
                    shopClient.BeingCreatedProductOrder.Product.IsEmployed = false;
                    shopClient.BeingCreatedProductOrder.Product = null;
                }

                shopClient.State = ShopClientState.ProductsTypeChoising;

                await db.SaveChangesAsync();
            }

            await botClient.EditMessageTextAsync(
               chatId: chatId,
               messageId: (int)shopClient.ControlMessageId,
               text: CreateCategoryMessageText(shopClient.BeingCreatedProductOrder.Category),
               replyMarkup: CreateCategoryKeyboad(shopClient.BeingCreatedProductOrder.Category));
        }



        private async Task CreateNoProductsErrorMessageAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            int controlMessageId = 0;

            using (DatabaseContext db = new DatabaseContext())
            {
                ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                shopClient.State = ShopClientState.ProductInfoReading;
                controlMessageId = (int)shopClient.ControlMessageId;

                await db.SaveChangesAsync();
            }

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: controlMessageId,
                text: "🚫Упс, товары закочились !",
                replyMarkup: CreateNoProductsErrorKeyboard());
        }
        private InlineKeyboardMarkup CreateNoProductsErrorKeyboard()
        {
            InlineKeyboardMarkup keyboard = new InlineKeyboardMarkup(
                new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(returnButtonText)
                    }

                });

            return keyboard;
        }



        private async Task CreateProductsTypeMenuGovernMessageAsync(ITelegramBotClient botClient, Update update, long chatId, List<IGrouping<string, Product>> availableProducts)
        {
            int controlMessageId = 0;

            using (DatabaseContext db = new DatabaseContext())
            {
                ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                shopClient.State = ShopClientState.ProductChoising;
                controlMessageId = (int)shopClient.ControlMessageId;

                await db.SaveChangesAsync();
            }

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: controlMessageId,
                text: "⏳Выберете время подписки:",
                replyMarkup: CreateProductsTypeMenuKeyboard(availableProducts));
        }
        private InlineKeyboardMarkup CreateProductsTypeMenuKeyboard(List<IGrouping<string, Product>> groupedProducts)
        {
            List<InlineKeyboardButton[]> keyboardButtons = new();

            for (int i = 0; i < (double)groupedProducts.Count / maxKeyboardLineLenght; i++)
            {
                List<InlineKeyboardButton> buttonsLine = new();

                for (int j = 0; j < maxKeyboardLineLenght; j++)
                {
                    int buttonNum = i * maxKeyboardLineLenght + j;
                    if (groupedProducts.Count > buttonNum)
                    {
                        buttonsLine.Add(InlineKeyboardButton.WithCallbackData(groupedProducts[buttonNum].Key));
                    }
                }

                keyboardButtons.Add(buttonsLine.ToArray());
            }

            keyboardButtons.Add(new InlineKeyboardButton[] { InlineKeyboardButton.WithCallbackData(returnButtonText) });

            InlineKeyboardMarkup keyboard = new(keyboardButtons);
            return keyboard;
        }

        public async Task ProductTypeMenuCallBackHandlerAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                if (update.CallbackQuery.Data == returnButtonText)
                {
                    await ReturnToCategoryMenuAsync(botClient, update, chatId);
                }
                else
                {
                    ShopClient shopClient;
                    List<Product> availableProducts = new();

                    using (DatabaseContext db = new DatabaseContext())
                    {
                        shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                        db.ShopClients.Where(sc => sc.ChatId == chatId)
                            .Include(sc1 => sc1.BeingCreatedProductOrder)
                            .ThenInclude(o => o.ProductType)
                            .ThenInclude(pt => pt.Products)
                            .ToList();

                        availableProducts = shopClient.BeingCreatedProductOrder.ProductType.Products.Where(p => p.IsEmployed == false && p.SubscribeTime == update.CallbackQuery.Data).ToList();
                    }

                    if (availableProducts.Count == 0)
                    {
                        await CreateNoProductsErrorMessageAsync(botClient, update, chatId);
                    }
                    else
                    {
                        await CreateProductMenuGovernMessageAsync(botClient, update, chatId, shopClient.BeingCreatedProductOrder.ProductType, availableProducts[0]);
                    }
                }
            }
        }



        private async Task CreateProductMenuGovernMessageAsync(ITelegramBotClient botClient, Update update, long chatId, ProductsType productType, Product product)
        {
            int controlMessageId = 0;

            using (DatabaseContext db = new DatabaseContext())
            {
                ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);
                db.ProductsOrders.Where(o => o.ShopClientIdOnCreating == shopClient.ChatId).Load();

                shopClient.State = ShopClientState.ProductInfoReading;

                shopClient.BeingCreatedProductOrder.ProductType = productType;
                shopClient.BeingCreatedProductOrder.Product = product;
                product.IsEmployed = true;

                controlMessageId = (int)shopClient.ControlMessageId;

                await db.SaveChangesAsync();
            }

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: controlMessageId,
                text: CreateProductMenuMessageText(productType, product),
                replyMarkup: CreateProductMenuKeyboard());
        }
        private string CreateProductMenuMessageText(ProductsType productType, Product product)
        {
            string text = $"💬Товар : {productType.Name} \n" +
                $"⏳Время подписки : {product.SubscribeTime} \n\n" +
                $"📃Описание: {productType.Description}";

            return text;
        }
        private InlineKeyboardMarkup CreateProductMenuKeyboard()
        {
            InlineKeyboardMarkup keyboard = new(
                new[]
                {
                    new[]
                    {
                         InlineKeyboardButton.WithCallbackData(goToPaymentButtonText),
                         InlineKeyboardButton.WithCallbackData(returnButtonText)
                    }
                });

            return keyboard;
        }

        public async Task ProductMenuCallBackHandlerAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                if (update.CallbackQuery.Data == returnButtonText)
                {
                    await ReturnToCategoryMenuAsync(botClient, update, chatId);
                }
                else if(update.CallbackQuery.Data == goToPaymentButtonText)
                {
                    await CreateAfterPaymentMenuGovernMessageAsync(botClient, update, chatId);
                }
            }
        }



        public async Task CreateAfterPaymentMenuGovernMessageAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            int controlMessageId = 0;

            using(DatabaseContext db = new DatabaseContext())
            {
                ShopClient shopClient = db.ShopClients.FirstOrDefault(sc => sc.ChatId == chatId);

                db.ShopClients.Where(sc => sc.ChatId == chatId)
                    .Include(sc1 => sc1.BeingCreatedProductOrder)
                    .Include(sc2 => sc2.ProductOrders)
                    .ToList();

                shopClient.BeingCreatedProductOrder.TimeOfCreating = DateTime.Now;
                shopClient.State = ShopClientState.AfterPayingForTheProduct;

                shopClient.ProductOrders.Add(shopClient.BeingCreatedProductOrder);
                shopClient.BeingCreatedProductOrder = null;
                
                controlMessageId = (int)shopClient.ControlMessageId;

                await db.SaveChangesAsync();
            }

            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: controlMessageId,
                text: "✅Товар успешно оплачен! С товаром можно ознакомиться в меню Аккаунт \n\n " +
                      "P.s: здесь пропущено меню с оплатой так как у меня нет возможности ее реализовать",
                replyMarkup: CreateAfterPaymentMenuKeyboard());
        }
        private InlineKeyboardMarkup CreateAfterPaymentMenuKeyboard()
        {
            InlineKeyboardMarkup keyboard = new(
                new[]
                {
                    new[]
                    {
                         InlineKeyboardButton.WithCallbackData(returnToMainMenuButtonText)
                    }
                });

            return keyboard;
        }

        public async Task AfterPaymentMenuCallBackHandlerAsync(ITelegramBotClient botClient, Update update, long chatId)
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                if (update.CallbackQuery.Data == returnToMainMenuButtonText)
                {
                    await returnToMainMenuAction.Invoke(botClient, update, chatId);
                }
            }
        }
    }
}
