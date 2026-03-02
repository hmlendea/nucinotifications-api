using Microsoft.AspNetCore.Mvc;
using NuciAPI.Controllers;
using NuciNotifications.Api.Requests;
using NuciNotifications.Api.Service;

namespace NuciNotifications.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class EmailController(
        IEmailService service) : NuciApiController
    {
        readonly NuciApiAuthorisation authorisation = NuciApiAuthorisation.None;

        [HttpPost]
        public ActionResult Send([FromBody] SendEmailRequest request)
            => ProcessRequest(request, () => service.Send(request), authorisation);
    }
}
