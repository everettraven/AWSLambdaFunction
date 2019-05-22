using System;
using System.Collections.Generic;

namespace AWSLambdaFunction
{
    public partial class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string ServiceProvider { get; set; }
    }
}
