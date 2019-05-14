using Amazon.Lambda.Core;
using Amazon.Lambda.CloudWatchEvents;
using PodioCore;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using PodioCore.Items;
using BrickBridge.Lambda.VilCap;
using newVilcapCopyFileToGoogleDrive;
using Saasafras;
using System.Text.RegularExpressions;
using PodioCore.Models.Request;
using PodioCore.Services;
using Task = System.Threading.Tasks.Task;
using Newtonsoft.Json;

namespace VilcapDateAssignTask
{
    public class CloudWatchHandler
    {
        public async Task FunctionHandler(Amazon.Lambda.CloudWatchEvents.ScheduledEvents.ScheduledEvent cwe, ILambdaContext context)
        {
            //var t = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(cwe.Detail.ToString());
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
    }
}
