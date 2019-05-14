using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using Saasafras;
using System.Collections.Generic;
using Task = System.Threading.Tasks.Task;

namespace VilcapDateAssignTask
{
    public class CloudWatchHandler
    {
       
        private static List<RoutedPodioEvent> _values;
        public async Task FunctionHandler(Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent cwe, ILambdaContext context)
        {
            //var envs = Newtonsoft.Json.JsonConvert.DeserializeObject<EnvsList>(cwe.Detail.ToString());
            const string FUNCTION_NAME = "VilcapDateAssignTask";
            SaasafrasClient saasafrasClient = new SaasafrasClient(
                System.Environment.GetEnvironmentVariable("BBC_SERVICE_URL"),
                System.Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY")
            );
            string lockValue = await saasafrasClient.LockFunction(FUNCTION_NAME, cwe.Time.ToString());
            try
            {
                if (string.IsNullOrEmpty(lockValue))
                {
                    context.Logger.LogLine($"Failed to acquire lock for {FUNCTION_NAME} at time {cwe.Time.ToString()}");
                    return;
                }
                context.Logger.LogLine("---Creating Routed Podio Event");

                RoutedPodioEvent e = new RoutedPodioEvent
                {
                    clientId = "toolkittemplate3",
                    environmentId = "toolkittemplate3",
                    solutionId = "vilcap",
                    version = "0.0"
                };


                var function = new Function();
                context.Logger.LogLine("---Submitting Routed Podio Event");
                await function.FunctionHandler(e, context);

                return;
            }
            catch (System.Exception ex)
            {
                throw ex;
            }
            finally
            {
                await saasafrasClient.UnlockFunction(FUNCTION_NAME, cwe.Time.ToString(), lockValue);
            }
        }
    }
}
