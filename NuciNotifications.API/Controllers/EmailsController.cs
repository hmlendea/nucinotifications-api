using Microsoft.AspNetCore.Mvc;

using NuciAPI.Controllers;

using NuciNotifications.API.Configuration;
using NuciNotifications.API.Requests;
using NuciNotifications.API.Service;

namespace NuciNotifications.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class EmailController(
        IEmailService service,
        SecuritySettings securitySettings) : NuciApiController
    {
        private readonly NuciApiAuthorisation authorisation = NuciApiAuthorisation.ApiKey(securitySettings.ApiKey);

        [HttpPost]
        public ActionResult Send([FromBody] SendEmailRequest request)
            => ProcessRequest(
                request,
                () => service.Send(request),
                authorisation);
    }
}
