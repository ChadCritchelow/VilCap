using Amazon.Lambda.Core;
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
	class TaskListDateParity
	{
		public async System.Threading.Tasks.Task DealWithTaskListDateParity(Item check, ILambdaContext context, Podio podio, GetIds ids)
		{
			//When Admin is updated... make sure to implement by checking is Program Start Date or Task List Status has just hanged
			var checkField = check.Field<CategoryItemField>(ids.GetFieldId("Admin|Task List Status"));
			if(checkField.Options.Any()&&checkField.Options.First().Text=="Created")
			{
				//continue
				//just creates a variable... think this is legacy. Will leave in case
			}
		}
	}
}

