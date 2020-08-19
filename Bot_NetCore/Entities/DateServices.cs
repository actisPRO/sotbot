using System;

namespace Bot_NetCore.Entities
{
    public class DateServices
    {
        public DateTime Date;
        public int ColorPrice;
        public int WantedPrice;
        public int RolePrice;
        public int FriendsPrice;

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
