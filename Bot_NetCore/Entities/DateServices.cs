using System;
using System.Collections.Generic;

namespace SeaOfThieves.Entities
{
    public class DateServices
    {
        public DateTime Date;
        public Dictionary<string, int> Services;

        public DateServices(DateTime date, Dictionary<string, int> services)
        {
            Date = date;
            Services = services;
        }
    }
}