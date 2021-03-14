using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

namespace Bot_NetCore.Entities
{
    public static class DonatorPriceListSQL
    {
        private static Dictionary<DateTime, DateServices> prices { get; set; }
        public static Dictionary<DateTime, DateServices> Prices { 
            get 
            {
                if (prices == null || prices.Count == 0)
                {
                    prices = new Dictionary<DateTime, DateServices>();

                    using var connection = new MySqlConnection(Bot.ConnectionString);
                    using var cmd = new MySqlCommand
                    {
                        CommandText = $"SELECT * FROM donators_prices;",
                        Connection = connection
                    };

                    cmd.Connection.Open();

                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var services = JsonConvert.DeserializeObject<DateServices>(reader.GetString("services"));
                        services.Date = reader.GetDateTime("date");

                        prices[services.Date] = services;
                    }
                }

                return prices;
            }
            private set { prices = value; }
        }

        public static void SavePrices()
        {
            var services = JsonConvert.SerializeObject(Prices[DateTime.Today]);

            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand();
            cmd.CommandText = "INSERT INTO donators_prices(date, services) " +
                "VALUES(@date, @services)" +
                "ON DUPLICATE KEY UPDATE services = @services;";

            cmd.Parameters.AddWithValue("@date", DateTime.Today.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@services", services);

            cmd.Connection = connection;
            cmd.Connection.Open();
            var reader = cmd.ExecuteNonQuery();
        }

        public static DateTime GetLastDate(DateTime forDate)
        {
            using var connection = new MySqlConnection(Bot.ConnectionString);
            using var cmd = new MySqlCommand
            {
                CommandText = $"SELECT date FROM donators_prices WHERE date <= @date ORDER BY date DESC LIMIT 1;",
                Connection = connection
            };

            cmd.Parameters.AddWithValue("@date", forDate);
            cmd.Connection.Open();

            var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return reader.GetDateTime("date");
            }
            return DateTime.Today;
        }
    }
}
