using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using Google.Apis.Drive.v3;
using PodioCore;
using PodioCore.Exceptions;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Models.Request;
using PodioCore.Services;
using PodioCore.Utils.ItemFields;
using System.Linq;
using System.Text.RegularExpressions;
using PodioCore.Utils;
using PodioCore.Comments;

namespace newVilcapCopyFileToGoogleDrive
{
    class WorkshopModules2
	{

		PodioCollection<Item> filter;
		public static string StripHTML(string input)
		{
			return Regex.Replace(input, "<.*?>", String.Empty);
		}

		public async System.Threading.Tasks.Task CreateWorkshopModules2(ILambdaContext context, Podio podio, Item check, RoutedPodioEvent e, DriveService service, GetIds ids, GoogleIntegration google,PreSurvAndExp pre)
		{

            #region // Utility vars //

            const int LIMIT = 25;
            const int MASTER_CONTENT_APP = 21310273;
            const int MAX_BATCHES = 10;

            string commentText = "";
            int fieldId = 0;
            int count = 0;
            var workshopAppId = ids.GetFieldId("Workshop Modules");
            var tasklistAppId = ids.GetFieldId("Task List");
            int waitSeconds = 5;

            int day = 0;
            TimeSpan timeFromStart = new TimeSpan(0);
            #endregion

            #region // Admin app values //

            var batchId = ids.GetFieldId("Admin|WS Batch");
            string batch = check.Field<CategoryItemField>(batchId).Options.First().Text;

            var startDateId = ids.GetFieldId("Admin|Program Start Date");
            DateTime startDate = new DateTime(check.Field<DateItemField>(startDateId).Start.Value.Ticks);

            var packageId = ids.GetFieldId("Admin|Curriculum Package");
            string package = check.Field<CategoryItemField>(packageId).Options.First().Text;
            context.Logger.LogLine($"Curriculum Batch '{batch}'");
            #endregion  

            #region // Get Batch //

            var viewServ = new ViewService(podio);
			context.Logger.LogLine("Got View Service");
			var views = await viewServ.GetViews(MASTER_CONTENT_APP);
            var view = from v in views
                       where v.Name == package
                       select v;
			context.Logger.LogLine($"Got View '{package}'");

            var op = new FilterOptions{ Filters = view.First().Filters };
            op.SortBy = "185391072"; // fieldId of Package Sequence (num) from Content Curation
            op.SortDesc = false;
            op.Limit = 25;

            Int32.TryParse(batch, out int batchNum);
            if (0 <= batchNum && batchNum <= MAX_BATCHES)
            {
                op.Offset = op.Limit * (batchNum - 1);
                context.Logger.LogLine($"Grabbing Items {op.Offset.Value + 1}-{op.Offset.Value + LIMIT} ...");
                filter = await podio.FilterItems(MASTER_CONTENT_APP, op);
                context.Logger.LogLine($"Items in filter:{filter.Items.Count()}");
                commentText = $"WS Batch {batch} finished";
            }
            else
            {
                context.Logger.LogLine("WARNING: No items found for batch!");
                commentText = "WS Batch # not recognized";
            }
            #endregion

            // Main Loop //

            foreach (var master in filter.Items)
            {
                // Setup //
                count += 1;
                context.Logger.LogLine($"On item #: {count}");
                Item child = new Item();

                #region // Check for new Day //

                fieldId = ids.GetFieldId("VC Administration|Content Curation |Workshop Day");
                var dayMaster = master.Field<CategoryItemField>(fieldId);
                if (dayMaster.Values != null)
                {
                    int dayMasterVal = 0;
                    Int32.TryParse(dayMaster.Options.First().Text.Split("Day ")[1], out dayMasterVal);

                    if ((dayMasterVal != day) && (dayMasterVal != 0))
                    {
                        day = dayMasterVal;
                        timeFromStart = TimeSpan.FromDays(day - 1);
                    }
                }
                #endregion

                #region // Assign Fields //

                fieldId = ids.GetFieldId("VC Administration|Content Curation |Workshop Detail Title");
                var titleMaster = master.Field<TextItemField>(fieldId);
                if (titleMaster.Value != null)
                {
                    fieldId = ids.GetFieldId("Workshop Modules|Title");
                    var titleChild = child.Field<TextItemField>(fieldId);
                    titleChild.Value = titleMaster.Value;
                }

                fieldId = ids.GetFieldId("VC Administration|Content Curation |Purpose");
                var descMaster = master.Field<TextItemField>(fieldId);
                if (descMaster.Value != null)
                {
                    fieldId = ids.GetFieldId("Workshop Modules|Description");
                    var descChild = child.Field<TextItemField>(fieldId);
                    descChild.Value = StripHTML(descMaster.Value);
                }

                var offsetMaster = master.Field<NumericItemField>(ids.GetFieldId("VC Administration|Content Curation |Minute Offset"));
                if (offsetMaster.Value != null)
                {
                    fieldId = ids.GetFieldId("Workshop Modules|Minute Offset");
                    var offsetChild = child.Field<NumericItemField>(fieldId);
                    offsetChild.Value = offsetMaster.Value;
                }

                fieldId = ids.GetFieldId("VC Administration|Content Curation |Entrepreneur Pre-Work Required");
                var workMaster = master.Field<TextItemField>(fieldId);
                if (workMaster.Value != null)
                {
                    fieldId = ids.GetFieldId("Workshop Modules|Entrepreneur Pre-work Required");
                    var workChild = child.Field<TextItemField>(fieldId);
                    workChild.Value = workMaster.Value;
                }

                fieldId = ids.GetFieldId("VC Administration|Content Curation |Materials Required");
                var matsMaster = master.Field<TextItemField>(fieldId);
                if (matsMaster.Value != null)
                {
                    fieldId = ids.GetFieldId("Workshop Modules|Additional Materials Required");
                    var matsChild = child.Field<TextItemField>(fieldId);
                    matsChild.Value = matsMaster.Value;
                }

                fieldId = ids.GetFieldId("VC Administration|Content Curation |Mentors Required");
                var mentMaster = master.Field<TextItemField>(fieldId);
                if (mentMaster.Value != null)
                {
                    fieldId = ids.GetFieldId("Workshop Modules|Mentors Required");
                    var mentChild = child.Field<TextItemField>(fieldId);
                    mentChild.Value = mentMaster.Value;
                }
                var field = new AppItemField();
                child.Fields.Add(field);

                var childTasks = child.Field<AppItemField>(ids.GetFieldId("Workshop Modules|Mentors Required"));
                var masterTasks = master.Field<AppItemField>(ids.GetFieldId("VC Administration|Content Curation |Dependent Task"));
                var taskOffset = master.Field<DurationItemField>(ids.GetFieldId("VC Administration|Content Curation |Dependent Task Offset"));
                #endregion

                #region // Date Calcs //

                fieldId = ids.GetFieldId("VC Administration|Content Curation |Package Sequence");
                var seqMaster = master.Field<CategoryItemField>(fieldId);

                fieldId = ids.GetFieldId("VC Administration|Content Curation |Duration");
                var durMaster = master.Field<DurationItemField>(fieldId);

                if (durMaster.Value != null)
                {
                    fieldId = ids.GetFieldId("Workshop Modules|Duration");
                    var durChild = child.Field<DurationItemField>(fieldId);
                    durChild.Value = durMaster.Value.Value.Duration(); // durChild.Value.Value.Add(durMaster.Value.Value);? durChild.Value = durMaster.Value;?

                    DateTime childDateTimeStart = startDate.Add(timeFromStart);
                    DateTime childDateTimeEnd = childDateTimeStart.Add(durChild.Value.Value.Duration());
                    context.Logger.LogLine($"Trying to scheduling for {childDateTimeStart.ToString()} - {childDateTimeEnd.ToString()}");
                    timeFromStart = timeFromStart.Add(durChild.Value.Value.Duration());

                    fieldId = ids.GetFieldId("Workshop Modules|Date");
                    var childTime = child.Field<DateItemField>(fieldId);
                    childTime.Start = childDateTimeStart;
                    childTime.End = childDateTimeEnd;
                    context.Logger.LogLine($"Scheduled for {childTime.Start.ToString()} - {childTime.End.ToString()}");
                }
                #endregion

                #region // GDrive Integration //

                fieldId = ids.GetFieldId("VC Administration|Content Curation |GDrive File Name");
                var embedMaster = master.Field<EmbedItemField>(fieldId);
                fieldId = ids.GetFieldId("Workshop Modules|Link to Material");
                var embedChild = child.Field<EmbedItemField>(fieldId);
                List<Embed> embeds = new List<Embed>();

				string parentFolderId = System.Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
				var cloneFolderId = google.GetSubfolderId(service, podio, e, parentFolderId);//TODO:

				foreach (var em in embedMaster.Embeds)
				{
					if (em.OriginalUrl.Contains(".google."))
					{
						await google.UpdateOneEmbed(service, em, embeds, cloneFolderId, podio, e);
					}
					//else          // Hold for 2.0 //
					//{
					//	NonGdriveLinks nonG = new NonGdriveLinks();
					//	await nonG.NonGDriveCopy(em, embeds, podio, e);
					//}
				}

				foreach (var embed in embeds)
				{
					embedChild.AddEmbed(embed.EmbedId);
				}
                context.Logger.LogLine($"Added field:{embedMaster.Label}");
                #endregion

                // Dependent Tasks Generation//
                foreach (var masterTask in masterTasks.Items)
                {
                    Item masterT = new Item();
                    context.Logger.LogLine("Creating empty master item");
                    masterT = await podio.GetItem(masterTask.ItemId);
                    context.Logger.LogLine("Got master item");
                    Item cloneT = new Item();

                    #region // Assign Dep. Task Fields //

                    var nameMasterTValue = masterT.Field<TextItemField>(ids.GetFieldId("VC Administration|Master Schedule|Task Name")).Value;
                    if (nameMasterTValue != null)
                    {
                        var nameCloneT = cloneT.Field<TextItemField>(ids.GetFieldId("Task List|Title"));
                        nameCloneT.Value = nameMasterTValue;
                    }

                    var descrMasterT = masterT.Field<TextItemField>(ids.GetFieldId("VC Administration|Master Schedule|Desciption"));
                    if (descrMasterT.Value != null)
                    {
                        var descrCloneT = cloneT.Field<TextItemField>(ids.GetFieldId("Task List|Description"));
                        descrCloneT.Value = StripHTML(descrMasterT.Value);
                    }

                    var priorityMasterT = masterT.Field<CategoryItemField>(ids.GetFieldId("VC Administration|Master Schedule|Priority"));
                    if (priorityMasterT.Options.Any())
                    {
                        var priorityCloneT = cloneT.Field<CategoryItemField>(ids.GetFieldId("Task List|Priority"));
                        priorityCloneT.OptionText = priorityMasterT.Options.First().Text;
                    }

                    var phaseMasterT = masterT.Field<CategoryItemField>(ids.GetFieldId("VC Administration|Master Schedule|Phase"));
                    if (phaseMasterT.Options.Any())
                    {
                        var phaseCloneT = cloneT.Field<CategoryItemField>(ids.GetFieldId("Task List|Phase"));
                        phaseCloneT.OptionText = phaseMasterT.Options.First().Text;
                    }

                    var esoMasterT = masterT.Field<CategoryItemField>(ids.GetFieldId("VC Administration|Master Schedule|ESO Member Role"));
                    if (esoMasterT.Options.Any())
                    {
                        var esoCloneT = cloneT.Field<CategoryItemField>(ids.GetFieldId("Task List|ESO Member Role"));
                        esoCloneT.OptionText = esoMasterT.Options.First().Text;
                    }

                    var depMasterT = masterT.Field<TextItemField>(ids.GetFieldId("VC Administration|Master Schedule|Dependancy"));
                    if (depMasterT.Value != null)
                    {
                        var depCloneT = cloneT.Field<TextItemField>(ids.GetFieldId("Task List|Additional Dependencies"));
                        depCloneT.Value = depMasterT.Value;
                    }

                    //var comChild = child.Field<CategoryItemField>(ids.GetFieldId("Task List|Completetion"));
                    //comChild.OptionText = "Incomplete";
                    #endregion

                    #region // Dep. Task Date Calcs //

                    var durationMasterT = masterT.Field<NumericItemField>(ids.GetFieldId("VC Administration|Master Schedule|Duration (Days)"));
                    var dateCloneT = cloneT.Field<DateItemField>(ids.GetFieldId("Task List|Date"));

                    if (durationMasterT.Value != null)
                    {
                        var taskStart = new DateTime(child.Field<DateItemField>(ids.GetFieldId("Workshop Modules|Date")).Start.Value.Ticks).Subtract(taskOffset.Value.GetValueOrDefault());
                        dateCloneT.Start = taskStart.Date;
                        var taskEnd = new DateTime(taskStart.AddDays(durationMasterT.Value.GetValueOrDefault()).Ticks);
                        dateCloneT.End = taskEnd.Date;
                    }
                    #endregion

                    #region // Dep. Task Gdrive Integration //

                    fieldId = ids.GetFieldId("VC Administration|Master Schedule|Gdrive Link");
                    var embedMasterT = masterT.Field<EmbedItemField>(fieldId);
                    fieldId = ids.GetFieldId("Task List|Linked Files");
                    var embedChildT = cloneT.Field<EmbedItemField>(fieldId);
                    List<Embed> embedsT = new List<Embed>();
                    string parentFolderIdT = Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
                    var cloneFolderIdT = google.GetSubfolderId(service, podio, e, parentFolderId);//TODO:

                    foreach (var em in embedMasterT.Embeds)
                    {
                        if (em.OriginalUrl.Contains(".google."))
                        {
                            await google.UpdateOneEmbed(service, em, embedsT, cloneFolderIdT, podio, e);
                        }
                        //else          // Hold for 2.0 //
                        //{
                        //	NonGdriveLinks nonG = new NonGdriveLinks();
                        //	await nonG.NonGDriveCopy(em, embeds, podio, e);
                        //}
                    }
                    #endregion

                    #region // Create Dep. Task Item //

                    CallPodioTasks:
                    try
                    {
                        var newTaskId = await podio.CreateItem(cloneT, tasklistAppId, true); //child Task List appId
                        cloneT = await podio.GetItem(newTaskId);
                        context.Logger.LogLine($"newTaskId ({newTaskId}) - cloned itemId ({cloneT.ItemId})");
                        context.Logger.LogLine($"Created Dependent Task");
                        childTasks.Values.Add(cloneT);
                    }
                    catch (PodioUnavailableException ex)
                    {
                        context.Logger.LogLine($"{ex.Message}");
                        context.Logger.LogLine($"Trying again in {waitSeconds} seconds.");
                        for (int i = 0; i < waitSeconds; i++)
                        {
                            System.Threading.Thread.Sleep(1000);
                            context.Logger.LogLine(".");
                        }
                        waitSeconds = waitSeconds * 2;
                        goto CallPodioTasks;
                    }
                    #endregion
                }

                #region // Create WorkshopModule Podio Item //
                context.Logger.LogLine($"Calling Podio");
                CallPodio:
                try
                {
                    context.Logger.LogLine($"Sending CreateItem Request");
                    var CreateItemResult = await podio.CreateItem(child, workshopAppId, true);
                    context.Logger.LogLine($"CreateItemResult={CreateItemResult}");
                    child = await podio.GetItem(CreateItemResult); //child Workshop Modules appId
                    context.Logger.LogLine($"child.itemid={child.ItemId}");
                }
                catch (PodioUnavailableException ex)
                {
                    context.Logger.LogLine($"{ex.Message}");
                    context.Logger.LogLine($"Trying again in {waitSeconds} seconds.");
                    for (int i = 0; i < waitSeconds; i++)
                    {
                        System.Threading.Thread.Sleep(1000);
                        context.Logger.LogLine(".");
                    }
                    waitSeconds = waitSeconds * 2;
                    goto CallPodio;
                }
                context.Logger.LogLine($"Created item #{count}");
                #endregion
            }

            #region // Comment on Client's Admin item && Add aux items //

            CommentService comm = new CommentService(podio);
			if (check.Field<CategoryItemField>(batchId).Options.First().Text == "1")
			{
				await pre.CreateExpendituresAndPreWSSurvs(context,podio,viewServ,check,e,service,ids,google);
			}
			await comm.AddCommentToObject("item", check.ItemId, commentText, hook: true);
            #endregion
        }
    }
}

