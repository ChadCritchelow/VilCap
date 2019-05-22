using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using Saasafras;
using System.Collections.Generic;
using Task = System.Threading.Tasks.Task;
using Newtonsoft.Json;

namespace VilcapDateAssignTask
{
    public class CloudWatchHandler
    {
        private class JsonHolder
        {
            public RoutedPodioEvent[] Values { get; set; }
        }
       
        public async Task FunctionHandler(Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent cwe, ILambdaContext context)
        {

            //var envs = Newtonsoft.Json.JsonConvert.DeserializeObject<EnvsList>(cwe.Detail.ToString());
            const string FUNCTION_NAME = "VilcapDateAssignTask";
            SaasafrasClient saasafrasClient = new SaasafrasClient(
                System.Environment.GetEnvironmentVariable("BBC_SERVICE_URL"),
                System.Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY")
            );

            string vilcapEnvar = System.Environment.GetEnvironmentVariable("VILCAP_ENVS");
            var vilcapEnvs= JsonConvert.DeserializeObject<JsonHolder>(vilcapEnvar).Values;

            string lockValue = await saasafrasClient.LockFunction(FUNCTION_NAME, cwe.Account);
            try
            {
                if (string.IsNullOrEmpty(lockValue))
                {
                    context.Logger.LogLine($"Failed to acquire lock for {FUNCTION_NAME} at time {cwe.Time.ToString()}");
                    return;
                }

                foreach (RoutedPodioEvent e in vilcapEnvs)
                {
                    context.Logger.LogLine($"--- Created events : {e.clientId}/{e.clientId}/{e.solutionId}/{e.version}");
                }

                foreach (RoutedPodioEvent e in vilcapEnvs)
                {
                    var function = new Function();
                    await function.FunctionHandler(e, context);
                }

                return;
            }
            catch (System.Exception ex)
            {
                throw ex;
            }
            finally
            {
                await saasafrasClient.UnlockFunction(FUNCTION_NAME, cwe.Account, lockValue);
            }
        }
    }
}
