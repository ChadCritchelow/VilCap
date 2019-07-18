using System;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;


namespace newVilcapCopyFileToGoogleDrive
{

    class Scheduler
    {

        // Public Vars (for clarity)

        DateTime pStart, rStart, sStart, wStart;
        DateTime pEnd, rEnd, sEnd, wEnd;
        TimeSpan pTSpan, rTSpan, sTSpan, wTSpan;

        // Store derived values from the {client WS}|Admin app 

        public Scheduler(Item check, RoutedPodioEvent e, GetIds ids, int PARTITIONS)
        {
            var pId = ids.Get("Admin|Program Design");
            pStart = new DateTime(check.Field<DateItemField>(pId).Start.Value.Ticks);
            pEnd = new DateTime(check.Field<DateItemField>(pId).End.Value.Ticks);
            pTSpan = (check.Field<DateItemField>(pId).End.Value - pStart) / PARTITIONS;

            var rId = ids.Get("Admin|Recruitment");
            rStart = new DateTime(check.Field<DateItemField>(rId).Start.Value.Ticks);
            rEnd = new DateTime(check.Field<DateItemField>(rId).End.Value.Ticks);
            rTSpan = (check.Field<DateItemField>(rId).End.Value - rStart) / PARTITIONS;

            var sId = ids.Get("Admin|Selection");
            sStart = new DateTime(check.Field<DateItemField>(sId).Start.Value.Ticks);
            sEnd = new DateTime(check.Field<DateItemField>(sId).End.Value.Ticks);
            sTSpan = (check.Field<DateItemField>(sId).End.Value - sStart) / PARTITIONS;

            var wId = ids.Get("Admin|Workshop Operations");
            wStart = new DateTime(check.Field<DateItemField>(wId).Start.Value.Ticks);
            wEnd = new DateTime(check.Field<DateItemField>(wId).End.Value.Ticks);
            wTSpan = (check.Field<DateItemField>(wId).End.Value - wStart) / PARTITIONS;
        }

        // Set real dates based on the item's abstract phase & duration

        public Item SetDate(Item child, GetIds ids, string phase, int assignmentVal, double durMaster)
        {
            var date = child.Field<DateItemField>(ids.Get("Task List|Date"));
            switch (phase)
                {
                    case "Program Design":
                        date.Start = pStart.Add(pTSpan * (assignmentVal - 1));
                        date.End = date.Start.Value.AddDays(durMaster).Date;
                        if (date.End.Value.CompareTo(pEnd) > 0) date.End = pEnd;
                        break;

                    case "Recruitment":
                        date.Start = rStart.Add(rTSpan * (assignmentVal - 1));
                        date.End = date.Start.Value.AddDays(durMaster).Date;
                        if (date.End.Value.CompareTo(rEnd) > 0) date.End = rEnd;
                        break;

                    case "Selection":
                        date.Start = sStart.Add(sTSpan * (assignmentVal - 1));
                        date.End = date.Start.Value.AddDays(durMaster).Date;
                        if (date.End.Value.CompareTo(sEnd) > 0) date.End = sEnd;
                        break;

                    case "Workshop Operations":
                        date.Start = wStart.Add(wTSpan * (assignmentVal - 1));
                        date.End = date.Start.Value.AddDays(durMaster).Date;
                        if (date.End.Value.CompareTo(wEnd) > 0) date.End = wEnd;
                        break;

                    default:
                        break;
                }
            return child;
        }

    }
}
