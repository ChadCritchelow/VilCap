using Amazon.Lambda.Core;
using PodioCore;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using PodioCore.Items;
using System.Text.RegularExpressions;

namespace newVilcapCopyFileToGoogleDrive
{
	class NextBatch
	{
		public async System.Threading.Tasks.Task GoToNextBatch(Item check, ILambdaContext context, Podio podio, GetIds ids, Comment commentToCheck)
		{
		}
	}
}
