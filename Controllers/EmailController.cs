using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using WSOptimizer7.Models;
using WSOptimizer7.Services;

namespace WSOptimizer7.Controllers
{
    public class EmailController : Controller
    {
        private readonly IEmailService emailService;

        public EmailController(IEmailService emailService)
        {
            this.emailService = emailService;
        }

        [HttpPost]
        [Route("api/correo/prueba")]
        [Route("api/email/prueba")]
        public async Task<IActionResult> EnviarPrueba([FromBody] EmailTestRequestModel request)
        {
            try
            {
                if (request == null)
                    return BadRequest("El cuerpo de la solicitud no es valido.");

                List<string> destinatarios = MergeRecipients(request.Destinatarios, request.DestinatariosTexto);
                if (destinatarios.Count == 0)
                    return BadRequest("Debe indicar al menos un destinatario.");

                string subject = string.IsNullOrWhiteSpace(request.Asunto)
                    ? "Prueba de envio de correo - WSOptimizer7"
                    : request.Asunto.Trim();

                string body = string.IsNullOrWhiteSpace(request.CuerpoHtml)
                    ? BuildDefaultTestBody()
                    : request.CuerpoHtml;

                await emailService.SendAsync(new EmailMessage
                {
                    To = destinatarios,
                    Cc = MergeRecipients(request.Cc, request.CcTexto),
                    Bcc = MergeRecipients(request.Bcc, request.BccTexto),
                    Subject = subject,
                    HtmlBody = body
                });

                return Ok(new
                {
                    ok = true,
                    mensaje = "Correo de prueba enviado correctamente.",
                    destinatarios,
                    asunto = subject,
                    fechaEnvio = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    ok = false,
                    mensaje = "No fue posible enviar el correo de prueba.",
                    error = ex.Message
                });
            }
        }

        private static List<string> MergeRecipients(IEnumerable<string>? recipients, string recipientsText)
        {
            var result = recipients?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList() ?? new List<string>();

            if (!string.IsNullOrWhiteSpace(recipientsText))
            {
                result.AddRange(recipientsText
                    .Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p)));
            }

            return result
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildDefaultTestBody()
        {
            string fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            return "<html><body>" +
                   "<p>Este es un correo de prueba enviado desde WSOptimizer7.</p>" +
                   "<p><strong>Fecha:</strong> " + fecha + "</p>" +
                   "</body></html>";
        }
    }
}
