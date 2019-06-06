using Amazon.Lambda.Core;
using PodioCore.Models;
using System;
using PodioCore.Items;
using newVilcapCopyFileToGoogleDrive;
using Saasafras;
using PdfSharp;
using Saasafras.Lambda.Google;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using PdfSharp.Drawing;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapApplicationPdf
{
    public class Function
    {
        [Obsolete]
        public async System.Threading.Tasks.Task FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
        {
            #region >> Setup <<
            var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
            var podio = factory.ForClient(e.clientId, e.environmentId);
            //Item item = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
            Item item = await podio.GetItem(Convert.ToInt32("1131694213"));
            SaasafrasClient saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
            var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
            string lockValue;
            GetIds ids = new GetIds(dictChild, dictMaster, e.environmentId);

            string functionName = "VilcapApplicationPdf";
            lockValue = await saasafrasClient.LockFunction(functionName, item.ItemId.ToString());
            #endregion
            try
            {
                if (string.IsNullOrEmpty(lockValue))
                {
                    context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {item.ItemId}");
                    return;
                }

                // TRIGGER DESCRIPTION ...
                var exId = item.ExternalId;
                context.Logger.LogLine($"--- Making PDFdoc for item with XID={exId}");

                var pdf = new PdfSharp.Pdf.PdfDocument();
                context.Logger.LogLine($"--- Created PDFdoc");
                var page = pdf.AddPage();
                context.Logger.LogLine($"--- Created PDFpage");
                var graphics = XGraphics.FromPdfPage(page);
                context.Logger.LogLine($"--- Created Xgraphic");
                //var font = new System.Drawing.Font(System.Drawing.FontFamily.GenericMonospace, 10.0f);
                var font = new System.Drawing.Font("Arial", 10.0f);
                context.Logger.LogLine($"--- Created Font");
                var xfont = new XFont(font);
                context.Logger.LogLine($"--- Created Xfont");

                graphics.DrawString("Hello, World!", font, XBrushes.Black, new XRect(0, 0, page.Width, page.Height), XStringFormat.Center);
                context.Logger.LogLine($"--- Drew Something");
                var filename = "TEST_FILE.pdf";
                
                var fileService = new PodioCore.Services.FileService(podio);
                var attachment = await fileService.UploadFile(filename, pdf.AcroForm.Stream.Value, "application/pdf");
                item.Files.Add(attachment);

                // END CONTENT
                #region >> Closeout <<
            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"!!! Outer Exception: {ex.Message}");
                throw ex;
            }
            finally
            {
                await saasafrasClient.UnlockFunction(functionName, item.ItemId.ToString(), lockValue);
            }
            #endregion
        }
    }
}
