﻿using Amazon.Lambda.Core;
using PodioCore;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using PodioCore.Items;

namespace newVilcapCopyFileToGoogleDrive
{
	class CompleteTasks
	{
		public async System.Threading.Tasks.Task SetTasksToComplete(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			var revision = await podio.GetRevisionDifference
				(
				Convert.ToInt32(check.ItemId), 
				check.CurrentRevision.Revision - 1, 
				check.CurrentRevision.Revision
				);
			var firstRevision = revision.First();
			var completionStatus = check.Field<CategoryItemField>(ids.GetFieldId("Task List|Completetion"));
			if (firstRevision.FieldId == completionStatus.FieldId)
			{
				if(completionStatus.Options.Any()&&completionStatus.Options.First().Text=="Complete")
				{
					//mark item tasks as complete
					foreach(var task in check.Tasks)
					{
						task.Status = "Completed";//Maybe????
						//send to podio?
					}
				}
			}

		}
	}
}

