using System.Data;
using System.Net;
using System.Net.Mail;
using WSOptimizer7.App_Data;

namespace WSOptimizer7.Services
{
    public class EmailService : IEmailService
    {
        public async Task SendAsync(EmailMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            List<string> recipients = NormalizeRecipients(message.To);

            if (recipients.Count == 0)
                throw new InvalidOperationException("No se configuraron destinatarios para el correo.");

            List<string> ccRecipients = NormalizeRecipients(message.Cc);
            List<string> bccRecipients = NormalizeRecipients(message.Bcc);
            List<string> attachmentPaths = message.AttachmentPaths?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList() ?? new List<string>();

            foreach (string attachmentPath in attachmentPaths)
            {
                if (!File.Exists(attachmentPath))
                    throw new FileNotFoundException("No se encontro el archivo adjunto para enviar por correo.", attachmentPath);

            }

            EmailConfiguration config = LoadEmailConfiguration();

            using var smtpClient = new SmtpClient(config.SmtpHost)
            {
                Port = config.SmtpPort,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(config.EmailUser, config.EmailPassword),
                EnableSsl = config.RequireSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(config.EmailFrom, config.EmailFromName),
                Subject = message.Subject,
                Body = message.HtmlBody,
                IsBodyHtml = true
            };

            foreach (string recipient in recipients)
                mailMessage.To.Add(recipient);

            foreach (string recipient in ccRecipients)
                mailMessage.CC.Add(recipient);

            foreach (string recipient in bccRecipients)
                mailMessage.Bcc.Add(recipient);

            foreach (string attachmentPath in attachmentPaths)
                mailMessage.Attachments.Add(new Attachment(attachmentPath));

            await smtpClient.SendMailAsync(mailMessage);
        }

        private static List<string> NormalizeRecipients(IEnumerable<string>? recipients)
        {
            return recipients?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
        }

        private static EmailConfiguration LoadEmailConfiguration()
        {
            DataTable dtEmail = Database.execQuery("SELECT TOP 1 * FROM Email_Parametros WHERE CveParametro = 1");
            if (dtEmail == null || dtEmail.Rows.Count == 0)
                throw new InvalidOperationException("No se encontrÃ³ configuraciÃ³n de correo en Email_Parametros.");

            DataRow row = dtEmail.Rows[0];
            string smtpHost = GetString(row, "SmtpHost");
            if (string.IsNullOrWhiteSpace(smtpHost))
                throw new InvalidOperationException("Email_Parametros.SmtpHost no estÃ¡ configurado.");

            string emailUser = GetString(row, "EMailUsr");
            string emailPassword = GetString(row, "EMailPw");
            string emailFrom = GetString(row, "EMailFrom");
            string emailFromName = GetString(row, "EMailFromName");

            if (string.IsNullOrWhiteSpace(emailFrom))
                emailFrom = emailUser;

            if (string.IsNullOrWhiteSpace(emailFrom))
                throw new InvalidOperationException("Email_Parametros.EMailFrom o EMailUsr no estÃ¡ configurado.");

            if (string.IsNullOrWhiteSpace(emailFromName))
                emailFromName = "Nukaxan";

            int smtpPort = 587;
            if (int.TryParse(GetString(row, "SmtpPort"), out int parsedPort))
                smtpPort = parsedPort;

            bool requireSsl = true;
            string reqSsl = GetString(row, "ReqSSL");
            if (!string.IsNullOrWhiteSpace(reqSsl))
            {
                if (bool.TryParse(reqSsl, out bool parsedBool))
                    requireSsl = parsedBool;
                else if (int.TryParse(reqSsl, out int parsedInt))
                    requireSsl = parsedInt != 0;
                else
                    requireSsl = reqSsl.Equals("S", StringComparison.OrdinalIgnoreCase);
            }

            return new EmailConfiguration
            {
                SmtpHost = smtpHost,
                SmtpPort = smtpPort,
                RequireSsl = requireSsl,
                EmailUser = emailUser,
                EmailPassword = emailPassword,
                EmailFrom = emailFrom,
                EmailFromName = emailFromName
            };
        }

        private static string GetString(DataRow row, string columnName)
        {
            if (row == null || row.Table == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
                return "";

            return row[columnName]?.ToString() ?? "";
        }

        private class EmailConfiguration
        {
            public string SmtpHost { get; set; } = "";
            public int SmtpPort { get; set; }
            public bool RequireSsl { get; set; }
            public string EmailUser { get; set; } = "";
            public string EmailPassword { get; set; } = "";
            public string EmailFrom { get; set; } = "";
            public string EmailFromName { get; set; } = "";
        }
    }
}

