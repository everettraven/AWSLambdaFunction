
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;

namespace AWSLambdaFunction
{
    [DataContract(Name = "courses")]
    public class Courses
    {
        [DataMember(Name = "id")]
        public Int64 CourseID { get; set; }

        [DataMember(Name ="name")]
        public string CourseName { get; set; }

    }
}
