using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;

namespace AWSLambdaFunction
{
    [DataContract(Name = "token")]
    public class Refresh
    {
        [DataMember(Name = "access_token")]
        public string accessToken { get; set; }

    }
}
