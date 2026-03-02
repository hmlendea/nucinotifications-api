using System.ComponentModel.DataAnnotations;
using NuciAPI.Requests;
using NuciSecurity.HMAC;

namespace NuciNotifications.Api.Requests
{
    public class SendEmailRequest : NuciApiRequest
    {
        [HmacOrder(1)]
        public string Sender { get; set; }

        [Required]
        [HmacOrder(2)]
        public string Recipient { get; set; }

        [Required]
        [HmacOrder(5)]
        public string Subject { get; set; }

        [Required]
        [HmacOrder(6)]
        public string Body { get; set; }
    }
}
