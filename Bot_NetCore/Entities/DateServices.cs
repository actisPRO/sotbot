using System;

namespace Bot_NetCore.Entities
{
    public class DateServices
    {
        public DateTime Date;
        public int ColorPrice;
        public int WantedPrice;
        public int RoleNamePrice;
        public int FriendsPrice;

        public DateServices(DateTime date, int colorPrice, int wantedPrice, int roleNamePrice, int friendsPrice)
        {
            Date = date;
            ColorPrice = colorPrice;
            WantedPrice = wantedPrice;
            RoleNamePrice = roleNamePrice;
            FriendsPrice = friendsPrice;
        }
    }
}
