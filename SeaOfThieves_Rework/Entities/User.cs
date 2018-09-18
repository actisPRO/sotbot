using System;
using System.Collections.Generic;

namespace SeaOfThieves.Entities
{
    public class User
    {
        public ulong Id { get; }
        public List<Warn> Warns { get; private set; }

        private User(ulong id)
        {
            Id = id;
            Warns = new List<Warn>();
        }

        public static User Create(ulong id)
        {
            UserList.Update(new User(id));
            return new User(id);
        }

        public void AddWarning(ulong moderator, DateTime date, string reason)
        {
            Warns.Add(new Warn(moderator, date, reason));
            UserList.Update(this);
        }
    }
}