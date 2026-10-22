using System.ComponentModel.DataAnnotations;

namespace NuciNotifications.API.Requests
{
    public sealed class EmailAttachmentRequest
    {
        [Required]
        [StringLength(255)]
        [RegularExpression(@"\A[^/\\\p{Cc}]+\z")]
        public string FileName { get; set; }

        [Required]
        [Base64String]
        public string Content { get; set; }
    }
}