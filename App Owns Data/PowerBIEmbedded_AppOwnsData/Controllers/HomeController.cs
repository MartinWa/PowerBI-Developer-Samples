using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.PowerBI.Api.V2;
using Microsoft.PowerBI.Api.V2.Models;
using Microsoft.Rest;
using PowerBIEmbedded_AppOwnsData.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace PowerBIEmbedded_AppOwnsData.Controllers
{
    public class HomeController : Controller
    {
        private static readonly string Username = ConfigurationManager.AppSettings["Username"];
        private static readonly string Password = ConfigurationManager.AppSettings["Password"];
        private static readonly string ResourceUrl = ConfigurationManager.AppSettings["ResourceUrl"];
        private static readonly string ApplicationId = ConfigurationManager.AppSettings["ApplicationId"];
        private static readonly string ApiUrl = ConfigurationManager.AppSettings["ApiUrl"];
        public string CustomerId = ConfigurationManager.AppSettings["CustomerId"];
        public string ReportName = ConfigurationManager.AppSettings["ReportName"];
        public string GroupName = ConfigurationManager.AppSettings["GroupName"];

        public async Task<ActionResult> Index()
        {
            var result = new EmbedConfig();
            try
            {
                var credential = new UserPasswordCredential(Username, Password);
                var authenticationContext = new AuthenticationContext("https://login.windows.net/common/oauth2/authorize/");
                var authenticationResult = await authenticationContext.AcquireTokenAsync(ResourceUrl, ApplicationId, credential);
                if (authenticationResult == null)
                {
                    throw new UnauthorizedAccessException();
                }

                var tokenCredentials = new TokenCredentials(authenticationResult.AccessToken, "Bearer");
                using (var client = new PowerBIClient(new Uri(ApiUrl), tokenCredentials))
                {
                    var groups = await client.Groups.GetGroupsAsync();
                    var group = groups.Value.FirstOrDefault(r => r.Name == GroupName);
                    if (group == null)
                    {
                        throw new Exception("Group not found");
                    }

                    var groupId = group.Id;
                    var reports = await client.Reports.GetReportsInGroupAsync(groupId);
                    var report = reports.Value.FirstOrDefault(r => r.Name == ReportName);
                    if (report == null)
                    {
                        throw new Exception("Report not found");
                    }

                    var rls = new EffectiveIdentity(CustomerId, new List<string> { report.DatasetId }, new List<string> { "Regular" });
                    var generateTokenRequestParameters = new GenerateTokenRequest("view", identities: new List<EffectiveIdentity> { rls });
                    var embedToken = await client.Reports.GenerateTokenInGroupAsync(groupId, report.Id, generateTokenRequestParameters);
                    result.Id = report.Id;
                    result.EmbedUrl = report.EmbedUrl;
                    result.EmbedToken = embedToken;
                }
            }
            catch (HttpOperationException exc)
            {
                result.ErrorMessage = string.Format("Status: {0} ({1})\r\nResponse: {2}\r\nRequestId: {3}", exc.Response.StatusCode, (int)exc.Response.StatusCode, exc.Response.Content, exc.Response.Headers["RequestId"].FirstOrDefault());
            }
            catch (Exception exc)
            {
                result.ErrorMessage = exc.ToString();
            }
            return View(result);
        }
    }
}
