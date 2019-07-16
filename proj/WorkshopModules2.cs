using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PodioCore.Exceptions;
using PodioCore.Items;
using PodioCore.Models;
using PodioCore.Models.Request;
using PodioCore.Services;
using PodioCore.Utils;
using PodioCore.Utils.ItemFields;

namespace newVilcapCopyFileToGoogleDrive
{
    public class WorkshopModules2
    {
        private PodioCollection<Item> filter;
        public static int LIMIT = 30;
        public static int MASTER_CONTENT_APP = newVilcapCopyFileToGoogleDrive.MASTER_CONTENT_APP;
        public static string SORT_ID_FIELD = "188139930"; // Local_Sorting; "185391072" = Package
        public static int MAX_BATCHES = 8;

        /// <summary>
        /// Pulls content from Administration|Content Curation into {client}|Workshop Modules, as well as accessory tasks and Google files. <para />
        /// Success= next batch #; Failure -1 
        /// </summary>
        public async Task<int> CreateWorkshopModules2( newVilcapCopyFileToGoogleDrive vilcap )
        {

            #region // Utility vars //

            var check = vilcap.item;
            var pre = vilcap.pre;
            var batchNum = -1;
            var commentText = "";
            var fieldId = 0;
            var count = 0;
            var workshopAppId = vilcap.ids.GetFieldId("Workshop Modules");
            var tasklistAppId = vilcap.ids.GetFieldId("Task List");
            var materialsAppId = vilcap.ids.GetFieldId("Materials");
            var waitSeconds = 5;
            var day = 0;
            var timeFromStart = new TimeSpan(0);
            #endregion

            #region // Admin app values //

            var batchId = vilcap.ids.GetFieldId("Admin|WS Batch");
            var batch = check.Field<CategoryItemField>(batchId).Options.First().Text;
            int.TryParse(batch, out batchNum);

            var startDateId = vilcap.ids.GetFieldId("Admin|Program Start Date");
            var startDate = new DateTime(check.Field<DateItemField>(startDateId).Start.Value.Ticks);

            var packageId = vilcap.ids.GetFieldId("Admin|Curriculum Package");
            var package = check.Field<CategoryItemField>(packageId).Options.First().Text;
            vilcap.context.Logger.LogLine($"Curriculum Batch '{batch}'");
            #endregion

            #region // Get Batch //

            var viewServ = new ViewService(vilcap.podio);
            //context.Logger.LogLine("Got View Service");
            var views = await viewServ.GetViews(MASTER_CONTENT_APP);
            var view = from v in views
                       where v.Name == $"{package} Batch {batchNum}"
                       select v;
            vilcap.context.Logger.LogLine($"Got View '{package}'");

            var op = new FilterOptions { Filters = view.First().Filters };
            vilcap.context.Logger.LogLine($"Filter: ({op.Filters.ToStringOrNull()}) ");
            op.SortBy = SORT_ID_FIELD; // fieldId of Package Sequence (num) from Content_Curation_
            op.SortDesc = false;
            op.Limit = LIMIT;

            if( 0 <= batchNum && batchNum <= MAX_BATCHES )
            {
                //op.Offset = op.Limit * (batchNum - 1); // 1. USING OFFSET & LIMIT 
                //context.Logger.LogLine($"Grabbing Items 1-{filter.Items.Count()} ..."); // 1. USING OFFSET & LIMIT
                filter = await vilcap.podio.FilterItems(MASTER_CONTENT_APP, op);
                vilcap.context.Logger.LogLine($"Items in filter:{filter.Items.Count()}");
                commentText = $"WS Batch {batch} finished ( {filter.Items.Count()} items)";
                if( !filter.Items.Any() )
                {
                    commentText = "No Items found for the given filter";
                    vilcap.context.Logger.LogLine(commentText);
                    return -1; // Let nVCFTGD.Program know somethings borked
                }
            }
            else
            {
                vilcap.context.Logger.LogLine("WS Batch # not recognized!");
                commentText = "WS Batch # not recognized";
                return -1; // Let nVCFTGD.Program know somethings borked
            }
            #endregion

            // Main Loop //

            foreach( var master in filter.Items )
            {
                // Setup //
                count += 1;
                vilcap.context.Logger.LogLine($"On item #: {count}");
                var child = new Item();

                #region // Check for new Day //

                fieldId = (vilcap.ids).GetFieldId("VC Administration|Content Curation |Workshop Day");
                var dayMaster = master.Field<CategoryItemField>(fieldId);
                if( dayMaster.Values != null )
                {
                    int.TryParse(dayMaster.Options.First().Text.Split("Day ")[1], out var dayMasterVal);
                    var color = child.Field<CategoryItemField>(vilcap.ids.GetFieldId("Workshop Modules|Calendar Color"));
                    var dayChild = child.Field<CategoryItemField>(vilcap.ids.GetFieldId("Workshop Modules|Day Number"));
                    dayChild.OptionText = dayMaster.Options.First().Text.Split(" ")[dayMaster.Options.First().Text.Split(" ").Length - 1];

                    if( (dayMasterVal != day) && (dayMasterVal != 0) ) // ie. Not a new Day
                    {
                        day = dayMasterVal;
                        timeFromStart = TimeSpan.FromDays(day - 1);
                        color.OptionText = "Date Manager";
                    }
                    else
                    {
                        color.OptionText = "Module";
                    }
                }
                #endregion

                #region // Assign Fields //

                fieldId = vilcap.ids.GetFieldId("VC Administration|Content Curation |Module Name");
                var titleMaster = master.Field<TextItemField>(fieldId);
                if( titleMaster.Value != null )
                {
                    fieldId = (vilcap.ids).GetFieldId("Workshop Modules|Title");
                    var titleChild = child.Field<TextItemField>(fieldId);
                    titleChild.Value = titleMaster.Value;
                }

                fieldId = vilcap.ids.GetFieldId("VC Administration|Content Curation |Purpose");
                var descMaster = master.Field<TextItemField>(fieldId);
                if( descMaster.Value != null )
                {
                    fieldId = vilcap.ids.GetFieldId("Workshop Modules|Description");
                    var descChild = child.Field<TextItemField>(fieldId);
                    //descChild.Value = StripHTML(descMaster.Value);
                    descChild.Value = descMaster.Value;
                }

                fieldId = vilcap.ids.GetFieldId("VC Administration|Content Curation |Entrepreneur Pre-Work Required");
                var workMaster = master.Field<TextItemField>(fieldId);
                if( workMaster.Value != null )
                {
                    fieldId = vilcap.ids.GetFieldId("Workshop Modules|Entrepreneur Pre-work Required");
                    var workChild = child.Field<TextItemField>(fieldId);
                    workChild.Value = workMaster.Value;
                }

                fieldId = vilcap.ids.GetFieldId("VC Administration|Content Curation |Materials Required");
                var matsMaster = master.Field<TextItemField>(fieldId);
                if( matsMaster.Value != null )
                {
                    fieldId = vilcap.ids.GetFieldId("Workshop Modules|Additional Materials Required");
                    var matsChild = child.Field<TextItemField>(fieldId);
                    matsChild.Value = matsMaster.Value;
                }

                fieldId = vilcap.ids.GetFieldId("VC Administration|Content Curation |Mentors Required");
                var mentMaster = master.Field<TextItemField>(fieldId);
                if( mentMaster.Value != null )
                {
                    fieldId = vilcap.ids.GetFieldId("Workshop Modules|Mentors Required");
                    var mentChild = child.Field<TextItemField>(fieldId);
                    mentChild.Value = mentMaster.Value;
                }

                var childTasks = child.Field<AppItemField>(vilcap.ids.GetFieldId("Workshop Modules|Dependent Task"));
                var masterTasks = master.Field<AppItemField>(vilcap.ids.GetFieldId("VC Administration|Content Curation |Dependent Task"));
                var taskOffset = master.Field<DurationItemField>(vilcap.ids.GetFieldId("VC Administration|Content Curation |Dependent Task Offset"));
                var mMaterials = master.Field<AppItemField>(vilcap.ids.GetFieldId("VC Administration|Content Curation |All Attached Materials")); // PLACEHOLDER
                //var childMats = child.Field<AppItemField>(ids.GetFieldId("Workshop Modules|Auxillary Materials")); // PLACEHOLDER
                var cEntrepreneurMaterials = child.Field<AppItemField>(vilcap.ids.GetFieldId("Workshop Modules|Entrepreneur Prep"));
                //var cEntrepreneurMaterials = child.Field<AppItemField>(ids.GetFieldId("Workshop Modules|Entrepreneur Prep"));
                #endregion

                #region// Auxillary Material Generation //

                foreach( var mMaterial in mMaterials.Items )
                {
                    var newMaterial = Materials.Copy(mMaterial, null, vilcap.ids, vilcap.podio, materialsAppId);
                    cEntrepreneurMaterials.Values.Add(newMaterial);
                    vilcap.context.Logger.LogLine($"Created Material #{newMaterial.Id}");
                    /////////////////   TODO
                }
                #endregion

                #region // Date Calcs //

                fieldId = vilcap.ids.GetFieldId("VC Administration|Content Curation |Package Sequence");
                var seqMaster = master.Field<CategoryItemField>(fieldId);

                fieldId = vilcap.ids.GetFieldId("VC Administration|Content Curation |Duration");
                var durMaster = master.Field<DurationItemField>(fieldId);

                if( durMaster.Value != null )
                {
                    fieldId = vilcap.ids.GetFieldId("Workshop Modules|Duration");
                    var durChild = child.Field<DurationItemField>(fieldId);
                    durChild.Value = durMaster.Value.Value.Duration(); // durChild.Value.Value.Add(durMaster.Value.Value);? durChild.Value = durMaster.Value;?

                    var childDateTimeStart = startDate.Add(timeFromStart);
                    var childDateTimeEnd = childDateTimeStart.Add(durChild.Value.Value.Duration());
                    //context.Logger.LogLine($"Scheduling for {childDateTimeStart.ToString()} - {childDateTimeEnd.ToString()}");
                    timeFromStart = timeFromStart.Add(durChild.Value.Value.Duration());

                    fieldId = vilcap.ids.GetFieldId("Workshop Modules|Date");
                    var childTime = child.Field<DateItemField>(fieldId);
                    childTime.Start = childDateTimeStart;
                    childTime.End = childDateTimeEnd;
                    vilcap.context.Logger.LogLine($"Scheduled for {childTime.Start.ToString()} - {childTime.End.ToString()}");
                }
                #endregion

                #region // GDrive Integration //

                fieldId = vilcap.ids.GetFieldId("VC Administration|Content Curation |GDrive Link");
                var embedMaster = master.Field<EmbedItemField>(fieldId);
                fieldId = vilcap.ids.GetFieldId("Workshop Modules|Link to Material");
                var embedChild = child.Field<EmbedItemField>(fieldId);
                var embeds = new List<Embed>();

                var parentFolderId = Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
                var cloneFolderId = vilcap.google.GetSubfolderId(vilcap.service, vilcap.podio, vilcap.e, parentFolderId);//TODO:

                foreach( var em in embedMaster.Embeds )
                {
                    if( em.OriginalUrl.Contains(".google.") )
                    {
                        await vilcap.google.UpdateOneEmbed(vilcap.service, em, embeds, cloneFolderId, vilcap.podio, vilcap.e);
                    }
                    //else          // Hold for 2.0 //
                    //{
                    //	NonGdriveLinks nonG = new NonGdriveLinks();
                    //	await nonG.NonGDriveCopy(em, embeds, podio, e);
                    //}
                }
                foreach( var embed in embeds ) { embedChild.AddEmbed(embed.EmbedId); }
                #endregion

                #region // Dependent Tasks Generation//
                foreach( var masterTask in masterTasks.Items )
                {
                    var masterT = new Item();
                    vilcap.context.Logger.LogLine("Creating empty master item");
                    masterT = await vilcap.podio.GetItem(masterTask.ItemId);
                    vilcap.context.Logger.LogLine("Got master item");
                    var cloneT = new Item();

                    #region // Assign Dep. Task Fields //

                    var nameMasterTValue = masterT.Field<TextItemField>(vilcap.ids.GetFieldId("VC Administration|Master Schedule|Task Name")).Value;
                    if( nameMasterTValue != null )
                    {
                        var nameCloneT = cloneT.Field<TextItemField>(vilcap.ids.GetFieldId("Task List|Title"));
                        nameCloneT.Value = $"{nameMasterTValue} ({titleMaster.Value})";
                    }

                    var descrMasterT = masterT.Field<TextItemField>(vilcap.ids.GetFieldId("VC Administration|Master Schedule|Desciption"));
                    if( descrMasterT.Value != null )
                    {
                        var descrCloneT = cloneT.Field<TextItemField>(vilcap.ids.GetFieldId("Task List|Description"));
                        //descrCloneT.Value = StripHTML(descrMasterT.Value);
                        descrCloneT.Value = descrMasterT.Value;
                    }

                    var priorityMasterT = masterT.Field<CategoryItemField>(vilcap.ids.GetFieldId("VC Administration|Master Schedule|Priority"));
                    if( priorityMasterT.Options.Any() )
                    {
                        var priorityCloneT = cloneT.Field<CategoryItemField>(vilcap.ids.GetFieldId("Task List|Priority"));
                        priorityCloneT.OptionText = priorityMasterT.Options.First().Text;
                    }

                    var phaseMasterT = masterT.Field<CategoryItemField>(vilcap.ids.GetFieldId("VC Administration|Master Schedule|Phase"));
                    if( phaseMasterT.Options.Any() )
                    {
                        var phaseCloneT = cloneT.Field<CategoryItemField>(vilcap.ids.GetFieldId("Task List|Phase"));
                        phaseCloneT.OptionText = phaseMasterT.Options.First().Text;
                    }

                    var esoMasterT = masterT.Field<CategoryItemField>(vilcap.ids.GetFieldId("VC Administration|Master Schedule|ESO Member Role"));
                    if( esoMasterT.Options.Any() )
                    {
                        var esoCloneT = cloneT.Field<CategoryItemField>(vilcap.ids.GetFieldId("Task List|ESO Member Role"));
                        esoCloneT.OptionText = esoMasterT.Options.First().Text;
                    }

                    var depMasterT = masterT.Field<TextItemField>(vilcap.ids.GetFieldId("VC Administration|Master Schedule|Dependancy"));
                    if( depMasterT.Value != null )
                    {
                        var depCloneT = cloneT.Field<TextItemField>(vilcap.ids.GetFieldId("Task List|Additional Dependencies"));
                        depCloneT.Value = depMasterT.Value;
                    }

                    //var comChild = child.Field<CategoryItemField>(ids.GetFieldId("Task List|Completetion"));
                    //comChild.OptionText = "Incomplete";
                    #endregion

                    #region // Dep. Task Date Calcs //

                    var durationMasterT = masterT.Field<NumericItemField>(vilcap.ids.GetFieldId("VC Administration|Master Schedule|Duration (Days)"));
                    var dateCloneT = cloneT.Field<DateItemField>(vilcap.ids.GetFieldId("Task List|Date"));
                    var durationCloneT = cloneT.Field<DurationItemField>(vilcap.ids.GetFieldId("Task List|Duration"));

                    if( durationMasterT.Value != null )
                    {
                        durationCloneT.Value = new TimeSpan((int)durationMasterT.Value.GetValueOrDefault(), 0, 0);
                        var taskStart = new DateTime(child.Field<DateItemField>(vilcap.ids.GetFieldId("Workshop Modules|Date")).Start.Value.Ticks).Subtract(taskOffset.Value.GetValueOrDefault());
                        dateCloneT.Start = taskStart.Date;
                        var taskEnd = new DateTime(taskStart.AddDays(durationMasterT.Value.GetValueOrDefault()).Ticks);
                        dateCloneT.End = taskEnd.Date;
                    }
                    #endregion

                    #region // Dep. Task Gdrive Integration //

                    fieldId = vilcap.ids.GetFieldId("VC Administration|Master Schedule|Gdrive Link");
                    var embedMasterT = masterT.Field<EmbedItemField>(fieldId);
                    fieldId = vilcap.ids.GetFieldId("Task List|Linked Files");
                    var embedChildT = cloneT.Field<EmbedItemField>(fieldId);
                    var embedsT = new List<Embed>();
                    var parentFolderIdT = Environment.GetEnvironmentVariable("GOOGLE_PARENT_FOLDER_ID");
                    var cloneFolderIdT = vilcap.google.GetSubfolderId(vilcap.service, vilcap.podio, vilcap.e, parentFolderId);//TODO:

                    foreach( var em in embedMasterT.Embeds )
                    {
                        if( em.OriginalUrl.Contains(".google.") )
                        {
                            await vilcap.google.UpdateOneEmbed(vilcap.service, em, embedsT, cloneFolderIdT, vilcap.podio, vilcap.e);
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
                        var newTaskId = await vilcap.podio.CreateItem(cloneT, tasklistAppId, true); //child Task List appId
                        cloneT = await vilcap.podio.GetFullItem(newTaskId);
                        vilcap.context.Logger.LogLine($"newTaskId ({newTaskId}) - cloned itemId ({cloneT.ItemId}) - cloned exId ({cloneT.ExternalId})");
                        vilcap.context.Logger.LogLine($"Created Dependent Task");
                        childTasks.ItemId = cloneT.ItemId;
                        vilcap.context.Logger.LogLine($"childTasks values: {childTasks.Values.FirstOrDefault().ToString()}");
                    }
                    catch( PodioUnavailableException ex )
                    {
                        vilcap.context.Logger.LogLine($"{ex.Message}");
                        vilcap.context.Logger.LogLine($"Trying again in {waitSeconds} seconds.");
                        for( var i = 0 ; i < waitSeconds ; i++ )
                        {
                            System.Threading.Thread.Sleep(1000);
                            vilcap.context.Logger.LogLine(".");
                        }
                        waitSeconds *= 2;
                        goto CallPodioTasks;
                    }
                    #endregion
                }
                vilcap.context.Logger.LogLine($"Calling Podio");
            CallPodio:
                try
                {
                    vilcap.context.Logger.LogLine($"child.ItemId={child.ItemId} & child.exId={child.ExternalId}");
                    await vilcap.podio.CreateItem(child, workshopAppId, true);
                }
                catch( PodioUnavailableException ex )
                {
                    vilcap.context.Logger.LogLine($"{ex.Message}");
                    vilcap.context.Logger.LogLine($"Trying again in {waitSeconds} seconds.");
                    for( var i = 0 ; i < waitSeconds ; i++ )
                    {
                        System.Threading.Thread.Sleep(1000);
                        vilcap.context.Logger.LogLine(".");
                    }
                    waitSeconds *= 2;
                    goto CallPodio;
                }
                vilcap.context.Logger.LogLine($"Created item #{count}");
                #endregion
            }

            // Comment on Client's Admin item && Add aux items //
            if( check.Field<CategoryItemField>(batchId).Options.First().Text == "1" ) { await PreSurvAndExp.CreateExpendituresAndPreWSSurvs(vilcap); }

            // Return the next Batch #, or -1 if all Items have been completed
            if( count == 0 ) return -1;
            return ++batchNum;
            //return count != 0 ? ++batchNum : -1;
        }
    }
}

