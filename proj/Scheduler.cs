using System;
using Amazon.Lambda.Core;
using PodioCore;
using PodioCore.Models;
using PodioCore.Utils.ItemFields;
using PodioCore.Utils;


namespace newVilcapCopyFileToGoogleDrive
{

    class Scheduler
    {

        // Public Vars (for clarity)

        DateTime programDeStart, recruitmeStart, selectionStart, workshopOStart;
        DateTime programDeEnd, recruitmeEnd, selectionEnd, workshopOEnd;
        TimeSpan programDeTSpan, recruitmeTSpan, selectionTSpan, workshopOTSpan;

        // Store derived values from the {client WS}|Admin app 

        public Scheduler(ILambdaContext context, Podio podio, Item check, RoutedPodioEvent e, GetIds ids, int PARTITIONS)
        {
            var programDeId = ids.GetFieldId("Admin|Program Design");
            programDeStart = new DateTime(check.Field<DateItemField>(programDeId).Start.Value.Ticks);
            programDeEnd = new DateTime(check.Field<DateItemField>(programDeId).End.Value.Ticks);
            programDeTSpan = (check.Field<DateItemField>(programDeId).End.Value - programDeStart) / PARTITIONS;

            var recruitmeId = ids.GetFieldId("Admin|Recruitment Phase");
            recruitmeStart = new DateTime(check.Field<DateItemField>(recruitmeId).Start.Value.Ticks);
            recruitmeEnd = new DateTime(check.Field<DateItemField>(recruitmeId).End.Value.Ticks);
            recruitmeTSpan = (check.Field<DateItemField>(recruitmeId).End.Value - recruitmeStart) / PARTITIONS;

            var selectionId = ids.GetFieldId("Admin|Selection");
            selectionStart = new DateTime(check.Field<DateItemField>(selectionId).Start.Value.Ticks);
            selectionEnd = new DateTime(check.Field<DateItemField>(selectionId).End.Value.Ticks);
            selectionTSpan = (check.Field<DateItemField>(selectionId).End.Value - selectionStart) / PARTITIONS;

            var workshopOId = ids.GetFieldId("Admin|Workshop Operations");
            workshopOStart = new DateTime(check.Field<DateItemField>(workshopOId).Start.Value.Ticks);
            workshopOEnd = new DateTime(check.Field<DateItemField>(workshopOId).End.Value.Ticks);
            workshopOTSpan = (check.Field<DateItemField>(workshopOId).End.Value - workshopOStart) / PARTITIONS;
        }

        // Set real dates based on the item's abstract phase & duration

        public Item SetDate(Item child, GetIds ids, string phase, int assignmentVal, double durMaster)
        {
            var date = child.Field<DateItemField>(ids.GetFieldId("Task List|Date"));
            switch (phase)
                {
                    case "Program Design":
                        date.Start = programDeStart.Add(programDeTSpan * (assignmentVal - 1));
                        date.End = date.Start.Value.AddDays(durMaster).Date;
                        if (date.End.Value.CompareTo(programDeEnd) > 0) date.End = programDeEnd;
                        break;
                    case "Recruitment Phase":
                        date.Start = recruitmeStart.Add(recruitmeTSpan * (assignmentVal - 1));
                        date.End = date.Start.Value.AddDays(durMaster).Date;
                        if (date.End.Value.CompareTo(recruitmeEnd) > 0) date.End = recruitmeEnd;
                        break;
                    case "Selection":
                        date.Start = selectionStart.Add(selectionTSpan * (assignmentVal - 1));
                        date.End = date.Start.Value.AddDays(durMaster).Date;
                        if (date.End.Value.CompareTo(selectionEnd) > 0) date.End = selectionEnd;
                        break;
                    case "Workshop Operations":
                        date.Start = workshopOStart.Add(workshopOTSpan * (assignmentVal - 1));
                        date.End = date.Start.Value.AddDays(durMaster).Date;
                        if (date.End.Value.CompareTo(workshopOEnd) > 0) date.End = workshopOEnd;
                        break;
                    default:
                        break;
                }
            return child;
        }
    }
}
