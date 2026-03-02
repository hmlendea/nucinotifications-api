using System;
using System.Collections.Generic;

namespace NuciNotifications.Api.Service.Models
{
    public class PersonalLog(DateOnly date, TimeOnly? time, string timeZone)
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public DateOnly Date { get; set; } = date;

        public TimeOnly? Time { get; set; } = time;

        public string TimeZone { get; set; } = timeZone ?? "UTC";

        public DateTime CreatedDateTime { get; set; } = DateTime.Now;

        public DateTime? UpdatedDateTime { get; set; } = DateTime.Now;

        public PersonalLogTemplate Template { get; set; } = PersonalLogTemplate.Text;

        public Dictionary<string, string> Data { get; set; } = [];

        public PersonalLog(DateOnly date) : this(date, null, null) { }

        public PersonalLog(DateOnly date, TimeOnly time) : this(date, time, "UTC") { }
    }
}
