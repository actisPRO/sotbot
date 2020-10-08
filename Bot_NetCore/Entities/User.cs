using System;
using System.Collections.Generic;

namespace Bot_NetCore.Entities
{
    [Obsolete]
    public class User
    {
        private User(ulong id)
        {
            Id = id;
            Warns = new List<Warn>();
        }

        public ulong Id { get; }
        public List<Warn> Warns { get; }

        public static User Create(ulong id)
        {
            UserList.Update(new User(id));
            return new User(id);
        }

        public void AddWarning(ulong moderator, DateTime date, string reason, string id, ulong logMessage)
        {
            Warns.Add(new Warn(moderator, date, reason, id, logMessage));
            UserList.Update(this);
        }

        public void RemoveWarning(string id)
        {
            for (var i = 0; i < Warns.Count; i++)
                if (Warns[i].Id == id)
                {
                    Warns.Remove(Warns[i]);
                    break;
                }

            UserList.Update(this);
        }
    }
}
