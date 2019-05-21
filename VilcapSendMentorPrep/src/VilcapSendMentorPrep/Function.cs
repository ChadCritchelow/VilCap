using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Google.Apis.Gmail.v1;


[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VilcapSendMentorPrep
{
    public class Function
    {
        public void FunctionHandler(string input, ILambdaContext context)
        {
            var gmail = new GmailService();


        }
    }
}
