using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using BrickBridge.Lambda.VilCap;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using newVilcapCopyFileToGoogleDrive;
using Saasafras.Lambda.Google;
using Saasafras.Lambda.Google.Interfaces;


[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapSendMentorPrep
{
    public class Function
    {
        static readonly string[] Scopes = { GmailService.Scope.GmailCompose };
        ///static readonly string ApplicationName = "BrickBridgeVilcapGmail";
        private IGmailService _saasyGmail;
        public Function()
        {
            _saasyGmail = new SaasafrasGmailService();
        }

        public void FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
        {
            //string serviceAcccount = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT");
            //var cred = GoogleCredential.FromJson(serviceAcccount).CreateScoped(Scopes).UnderlyingCredential;
            //var google = new GoogleIntegration();
            //var gmail = new GmailService(new BaseClientService.Initializer()
            //{
            //    HttpClientInitializer = cred,
            //    ApplicationName = ApplicationName,
            //});

            try
            {
                var gdriveLinks = "BODY: " + "https://docs.google.com/document/d/1tkVbKR0f4w-JKTGExNvNnWHwy2hPpFlDK_-ho30gtc4/export";             
                var mailMessage = _saasyGmail.BuildMessage("toolkit@vilcap.com", new string[]{"john@brickbridgeconsulting.com"}, "~1010101~", gdriveLinks);
                var success = _saasyGmail.SendEmail(mailMessage);
                var result = success.Result;

                context.Logger.LogLine($"--- Email sent: {success}");

            }
            //
            catch (System.Exception ex)
            {
                context.Logger.LogLine($"{e.clientId} - {ex.Message}");
                throw ex;
            }
            //
        }
    }
}
