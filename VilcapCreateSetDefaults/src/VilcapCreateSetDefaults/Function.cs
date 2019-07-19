using Amazon.Lambda.Core;
using newVilcapCopyFileToGoogleDrive;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using Saasafras;
using System;
using System.Collections.Generic;
using System.Linq;
using Task = System.Threading.Tasks.Task;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapCreateSetDefaults
{
    public class Function
    {
        /// <summary>
        /// Company Profile|item.create -->
        /// Pull data from Application & Create Entrepreneurs
        /// </summary>
		public async Task FunctionHandler(RoutedPodioEvent e, ILambdaContext context)
        {
            #region // Generic Setup //
            var factory = new AuditedPodioClientFactory(e.solutionId, e.version, e.clientId, e.environmentId);
            var podio = factory.ForClient(e.clientId, e.environmentId);
            var check = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
            var saasafrasClient = new SaasafrasClient(Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
            var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.environmentId, e.solutionId, e.version);
            var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
            string lockValue;
            var ids = new GetIds(dictChild, dictMaster, e.environmentId);
            var functionName = "VilcapCreateSetDefaults";
            lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
            if (string.IsNullOrEmpty(lockValue))
            {
                context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
                return;
            }
            #endregion
            try
            {
                // On Creation of a Company Profile:

                #region // Get referenced items from applications app //
                var checkApp = new Item();
                var itemRef = check.Field<AppItemField>(ids.Get("Company Profiles|Application"));
                var updateApp = new Item() { ItemId = itemRef.Items.First().ItemId };
                updateApp.Field<CategoryItemField>(ids.Get("Applications|Application Status")).OptionText = "Company Profile Created";
                await podio.UpdateItem(updateApp, true);
                #endregion

                checkApp = await podio.GetItem(itemRef.Items.First().ItemId);
                context.Logger.LogLine($"Item ID in foreach: {itemRef.Items.First().ItemId}");
                context.Logger.LogLine($"Application Item ID: {checkApp.ItemId}");
                var updateCompanyProfile = new Item() { ItemId = check.ItemId };
                updateCompanyProfile.Field<PhoneItemField>(ids.Get("Company Profiles|Phone")).Value =
                    checkApp.Field<PhoneItemField>(ids.Get("Applications|Phone")).Value;
                updateCompanyProfile.Field<EmailItemField>(ids.Get("Company Profiles|Email")).Value =
                    checkApp.Field<EmailItemField>(ids.Get("Applications|Email")).Value;

                if (checkApp.Field<DateItemField>(ids.Get("Applications|Company Founding Date")).Start != null)
                {
                    updateCompanyProfile.Field<DateItemField>(ids.Get("Company Profiles|Company Founding Date")).Start =
                        checkApp.Field<DateItemField>(ids.Get("Applications|Company Founding Date")).Start;
                }
                var emails = checkApp.Field<EmailItemField>(ids.Get("Applications|Email")).Value;
                foreach (var email in emails)
                {
                    var entrepreneur = new Item();
                    entrepreneur.Field<AppItemField>(ids.Get("Entrepreneurs|Company")).ItemId = check.ItemId;
                    entrepreneur.Field<EmailItemField>(ids.Get("Entrepreneurs|Entrepreneur Email")).Value = new List<EmailPhoneFieldResult> { email };
                    await podio.CreateItem(entrepreneur, ids.Get("Entrepreneurs"), true);
                }
                await podio.UpdateItem(updateCompanyProfile, true);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                await saasafrasClient.UnlockFunction(functionName, check.ItemId.ToString(), lockValue);
            }
        }
    }
}
