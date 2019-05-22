using System;
using System.Collections.Generic;
using System.Text;

namespace AWSLambdaFunction
{
    public class Message
    {
        public string TextMessage { get; set; }

        public string EmailMessage {get; set;}

        public int AssignmentCount {get; set;}

    }
}
