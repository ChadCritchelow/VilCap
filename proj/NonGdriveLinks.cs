using PodioCore;
using PodioCore.Models;
using System;
using System.Collections.Generic;

namespace newVilcapCopyFileToGoogleDrive
{
    class NonGdriveLinks
	{
		public async System.Threading.Tasks.Task NonGDriveCopy(Embed embed, List<Embed> embeds, Podio podio, RoutedPodioEvent e)  // Hold for 2.0 //
		{
			try
			{
				Console.WriteLine($"{e.podioEvent.item_id} - Direct URL Embed Link (resolved): {embed.ResolvedUrl}");
				Console.WriteLine($"{e.podioEvent.item_id} - Direct URL Embed Link (original): {embed.OriginalUrl}");
				await System.Threading.Tasks.Task.Run(() =>
				{
					embeds.Add(embed);
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"{e.podioEvent.item_id} - {ex.Message} - {ex.StackTrace} - {ex.InnerException}");
				throw ex;
			}
		}
	}
}
