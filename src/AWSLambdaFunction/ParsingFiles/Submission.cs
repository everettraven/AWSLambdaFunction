using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Globalization;

namespace AWSLambdaFunction
{
    [DataContract(Name = "submission")]
    public class Submission
    {
        [DataMember(Name = "user_id")]
        public Int64 UserId {get; set;}

    }
}