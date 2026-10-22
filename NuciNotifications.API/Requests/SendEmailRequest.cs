using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

using NuciAPI.Requests;

using NuciSecurity.HMAC;

namespace NuciNotifications.API.Requests
{
    public sealed class SendEmailRequest : NuciApiRequest, IValidatableObject
    {
        [HmacOrder(1)]
        public string Sender { get; set; }

        [Required]
        [HmacOrder(2)]
        public string Recipient { get; set; }

        [Required]
        [HmacOrder(3)]
        public string Subject { get; set; }

        [Required]
        [HmacOrder(4)]
        public string Body { get; set; }

        [HmacIgnore]
        public EmailAttachmentRequest[] Attachments { get; set; }

        [JsonIgnore]
        [HmacOrder(5)]
        public string AttachmentsSignatureContent
        {
            get
            {
                if (Attachments is null || Attachments.Length.Equals(0))
                {
                    return string.Empty;
                }

                return JsonSerializer.Serialize(Attachments, JsonSerializerOptions.Web);
            }
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Attachments is null)
            {
                yield break;
            }

            foreach (EmailAttachmentRequest attachment in Attachments)
            {
                if (attachment is null)
                {
                    yield return new ValidationResult(
                        "Attachments must not contain null entries.",
                        [nameof(Attachments)]);
                }
                else
                {
                    List<ValidationResult> results = [];
                    Validator.TryValidateObject(attachment, new ValidationContext(attachment), results, true);

                    foreach (ValidationResult result in results)
                    {
                        yield return new ValidationResult(result.ErrorMessage, [nameof(Attachments)]);
                    }
                }
            }
        }
    }
}
