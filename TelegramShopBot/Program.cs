using System;

namespace TelegramShopBot
{
    //CODENAME : OYA_Project

    internal class Program
    {
        private const string token = "TOKEN";



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
            Console.WriteLine("End");
        }
    }
}
