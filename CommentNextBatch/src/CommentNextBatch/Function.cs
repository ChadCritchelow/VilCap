using Amazon.Lambda.Core;
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
using PodioCore.Comments;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace CommentNextBatch
{
	public class PodioCommentEvent
	{
		public string type { get; set; }
		public string item_id { get; set; }
		public string comment_id { get; set; }
	}
	public class RoutedCommentEvent
	{
		public PodioCommentEvent podioEvent { get; set; }
		public string clientId { get; set; }
		public string version { get; set; }
		public string appId { get; set; }
		public string envId { get; set; }
	}
	public class Function
	{
		static LambdaMemoryStore memoryStore = new LambdaMemoryStore();
		public async System.Threading.Tasks.Task FunctionHandler(RoutedCommentEvent e, ILambdaContext context)
		{
			context.Logger.LogLine(Newtonsoft.Json.JsonConvert.SerializeObject(e));
			var factory = new AuditedPodioClientFactory(e.appId, e.version, e.clientId, e.envId);
			var podio = factory.ForClient(e.clientId, e.envId);
			Item check = await podio.GetItem(Convert.ToInt32(e.podioEvent.item_id));
			SaasafrasClient saasafrasClient = new SaasafrasClient(System.Environment.GetEnvironmentVariable("BBC_SERVICE_URL"), System.Environment.GetEnvironmentVariable("BBC_SERVICE_API_KEY"));
			var dictChild = await saasafrasClient.GetDictionary(e.clientId, e.envId, e.appId, e.version);
			var dictMaster = await saasafrasClient.GetDictionary("vcadministration", "vcadministration", "vilcap", "0.0");
			string lockValue;
			GetIds ids = new GetIds(dictChild, dictMaster, e.envId);
			string functionName="CommentNextBatch";
			lockValue = await saasafrasClient.LockFunction(functionName, check.ItemId.ToString());
			try
			{
				if (string.IsNullOrEmpty(lockValue))
				{
					context.Logger.LogLine($"Failed to acquire lock for {functionName} and id {check.ItemId}");
					return;
				}
				CommentService serve = new CommentService(podio);
				var commentToCheck = await serve.GetComment(int.Parse(e.podioEvent.comment_id));
				
				var commentPieces = commentToCheck.Value.Split(" ");
				var type = commentPieces[0];
				var batch = commentPieces[2];
				bool isNum = Regex.IsMatch(batch, @"^\d+$");
				Item updateMe = new Item() { ItemId = check.ItemId };
				int currentBatch;
				if (isNum)
				{
					try
					{
						int.TryParse(batch, out currentBatch);
					}
					catch (Exception ex)
					{
						context.Logger.LogLine("It seems batch number could not be found");
						throw ex;
					}
					switch (type)
					{
						case "WS":
							context.Logger.LogLine("Type==WS");
							var wsBatchField = updateMe.Field<CategoryItemField>(ids.GetFieldId("Admin|WS Batch"));
                            wsBatchField.OptionText = $"{++currentBatch}"; //(currentBatch++).ToString();

                            break;
						case "TL":
							context.Logger.LogLine("Type==TL");
							var tlBatchField = updateMe.Field<CategoryItemField>(ids.GetFieldId("Admin|TL Batch"));
							tlBatchField.OptionText = $"{++currentBatch}"; //(currentBatch++).ToString();
                            break;
					}
					await podio.UpdateItem(updateMe, true);

				}
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
