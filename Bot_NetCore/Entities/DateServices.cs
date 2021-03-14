using System;
using Newtonsoft.Json;

namespace Bot_NetCore.Entities
{
    public class DateServices
    {
        [JsonIgnore]
        public DateTime Date { get; set; }
        public int ColorPrice { get; set; }
        public int WantedPrice { get; set; }
        public int RolePrice { get; set; }
        public int FriendsPrice { get; set; }

        public DateServices(DateTime date, int colorPrice, int wantedPrice, int rolePrice, int friendsPrice)
        {
            Date = date;
            ColorPrice = colorPrice;
            WantedPrice = wantedPrice;
            RolePrice = rolePrice;
            FriendsPrice = friendsPrice;
        }
    }
}
