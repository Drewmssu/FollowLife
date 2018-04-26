using Mailjet.Client;
using Mailjet.Client.Resources;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Configuration;
using System.Web;
using System.Text;

namespace FollowLifeService.MailJet
{
    public static class MailService
    {
        public static async Task<string> SendMembershipEmail(string toEmail, string toName, string membershipCode)
        {
            var client = new MailjetClient(ConfigurationManager.AppSettings["MailJetPublicApiKey"], ConfigurationManager.AppSettings["MailJetPrivateApiKey"])
            {
                Version = ApiVersion.V3_1
            };

            var request = new MailjetRequest
            {
                Resource = Send.Resource,
            }
            .Property(Send.Messages, new JArray
            {
                new JObject
                {
                    { "From", new JObject
                    {
                        { "Email", "avanced.apple@gmail.com" },
                        { "Name", "FollowLife" }
                    }},
                    { "To", new JArray
                    {
                        new JObject
                        {
                            { "Email", toEmail },
                            { "Name", toName }
                        }
                    }},
                    { "TemplateID", 370199 },
                    { "TemplateLanguage", true },
                    { "Subject", "FollowLife - Activation Code" },
                    { "Variables", new JObject
                    {
                        {"code", membershipCode }
                    }}
                }
            });

            var response = await client.PostAsync(request);

            return response.IsSuccessStatusCode ? "success" : "error";
        }
    }
}
