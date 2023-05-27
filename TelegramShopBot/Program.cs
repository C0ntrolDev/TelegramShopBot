using System;

namespace TelegramShopBot
{
    //CODENAME : OYA_Project

    internal class Program
    {
        private const string token = "6020160119:AAE9lE9GFY772o4rTJ8Jmn5kzlkAmANcNhA";



        static void Main(string[] args)
        {
            using(DatabaseContext db = new DatabaseContext())
            {
                db.Database.EnsureCreated();
            }
            Console.WriteLine("База данных запущена успешно");


            TelegramBotManager telegramBotManager = new(token);
            telegramBotManager.StartTelegramBot();
            Console.WriteLine("TelegramBotManager запущен успешно");


            Console.ReadKey();

            using (DatabaseContext db = new DatabaseContext())
            {
                db.Database.EnsureDeleted();
            }
            Console.WriteLine("End");
        }
    }
}