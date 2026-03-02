using System.ComponentModel.DataAnnotations;
using NuciAPI.Requests;
using NuciSecurity.HMAC;

namespace NuciNotifications.Api.Models
{
    public class SendEmailRequest : NuciApiRequest
    {
        [Required]
        [HmacOrder(0)]
        public string Recipient { get; set; }

        [HmacOrder(10)]
        public string Subject { get; set; }

        [HmacOrder(20)]
        public string Body { get; set; }
    }
}
