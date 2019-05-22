using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Security.Cryptography;

using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

using Amazon.Lambda.Core;
using System.IO;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AWSLambdaFunction
{
    public class Function
    {
        
        /// <summary>
        /// A test lambda function
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public void FunctionHandler(ILambdaContext context)
        {
            //Create a list to store all the users from the database query
            List<User> UserList = new List<User>();

            //Use the entity framework to create a request through the database context
            using(var db = new canvasremindwebappContext())
            {
                //Put the whole database table into a list 
                UserList = db.User.ToList();

                foreach(var user in UserList)
                {
                    //Refresh the access token
                    Refresh(user).Wait();
                }
            

            }

            //Use a new instance of the DbContext to get an updated database list to work from

            using(var db = new canvasremindwebappContext())
            {
                List<User> users = new List<User>(); 
                users = db.User.ToList();

                //Do the rest of the steps
                foreach(var u in users)
                {
                    
                    Message message = new Message();
                    GetCourses(u, message).Wait();
                    SendEmail(u, message);
                    SendText(u,message);
                    
                }
            
                
            
            }

        
        }

        //Function to get parameters from the AWS parameter store
        public string GetAWSParameter(string input)
        {
            var parameterName = input;

            var ssmClient = new AmazonSimpleSystemsManagementClient(Amazon.RegionEndpoint.USEast2);
            var response = ssmClient.GetParameterAsync(new GetParameterRequest
            {
                Name = parameterName,
                WithDecryption = true
            });

           return response.Result.Parameter.Value;
        }

        //Get the courses for the user passed in
        public async Task GetCourses(User user, Message message)
        {
            message.EmailMessage = "Hello " + Decrypt(user.Name) + ",\n";
            message.AssignmentCount = 0;
            //Create an HTTP client to be used to send HTTP requests
            HttpClient Client = new HttpClient();

            //Setup a serializer to read JSON results into a list parsed by the Courses class
            var courseSerializer = new DataContractJsonSerializer(typeof(List<Courses>));

            //Clear the request accept headers and set a new authorization header to get access to the Canvas Web API
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Add("Authorization", String.Format("Bearer {0}", Decrypt(user.AccessToken)));

            //Set up a stream to get the response data
            var coursesStreamTask = Client.GetStreamAsync("https://" + GetAWSParameter($"/CanvasProd/AppSecrets/CanvasURL") +"/api/v1/courses"); //<---- Place proper url here

            //Read the response data into a list of course objects
            var coursesResults = courseSerializer.ReadObject(await coursesStreamTask) as List<Courses>;


            //Get the assignments for each course in the results list
            foreach(var course in coursesResults)
            {
                //Canvas API will return all courses even ones a user isnt part of but returns its name as null
                //to prevent from errors only use ones that arent null
                if(course.CourseName != null)
                {
                    GetAssignments(course, Client, message, user).Wait();
                }
            }

            message.TextMessage = String.Format("You have {0} assignments due in the future. Make sure to do them!", message.AssignmentCount);

        }

        //Function to get all the assignments for the course passed into the function
        public async Task GetAssignments(Courses Course, HttpClient Client, Message message, User user)
        {
            //Serializer to parse the data from the HTTP request
            var assignmentSerializer = new DataContractJsonSerializer(typeof(List<Assignments>));

            //Data stream to get the response from the Canvas Web API
            var assignmentStreamTask = Client.GetStreamAsync(String.Format("https://" + GetAWSParameter($"/CanvasProd/AppSecrets/CanvasURL") + "/api/v1/courses/{0}/assignments", Course.CourseID)); //<---- Place the proper url here

            //Variable to parse the results of the data stream and store it in a list
            var assignmentResults = assignmentSerializer.ReadObject(await assignmentStreamTask) as List<Assignments>;

            //For each assignment do something
            foreach(var assignment in assignmentResults)
            {
                //Check if there are any submissions
               // SubmissionCheck(Course, assignment, Client, user).Wait(); <--- Currently not authorized to use

                //Make sure the assignment is still due
                if(!(DateTime.Compare(assignment.TimeDue, DateTime.Now) <= 0)) //<--- if authorized for submissions add submission check boolean
                {
                    //set messages
                    message.AssignmentCount++;
                    message.EmailMessage = String.Concat(message.EmailMessage, String.Format("Course: {0} has {1} due at {2}\n", Course.CourseName, assignment.AssignmentName, assignment.DueDate));
                }

            }

        }

        //Function to check to see if there are any submissions attached to the assignment and user
        /* 
        public async Task SubmissionCheck(Courses Course, Assignments Assignment, HttpClient Client, User user)
        {
            //Serializer to parse the data from the HTTP request
            var submissionSerializer = new DataContractJsonSerializer(typeof(List<Submission>));

            //Data stream to get the response from the Canvas Web API
            var submissionStreamTask = Client.GetStreamAsync(String.Format("https://" + GetAWSParameter($"/CanvasProd/AppSecrets/CanvasURL") + "/api/v1/courses/{0}/assignments/{1}/submissions", Course.CourseID, Assignment.ID)); //<---- Place the proper url here

            //Variable to parse the results of the data stream and store it in a list
            var submissionResults = submissionSerializer.ReadObject(await submissionStreamTask) as List<Submission>;

            //Compare each submission object to the users id to see if they submitted it
            foreach(var submission in submissionResults)
            {
                if(submission.UserId == user.Id)
                {
                    //set the submitted variable of the assignment object to true
                    Assignment.Submitted = true;
                }

            }
        }
        */

        //Function that checks the users s)ervice provider and sends a text message to the number the user provided during sign up
        public void SendText(User user, Message message)
        {
            MailMessage mailMessage = new MailMessage();

            if(user.ServiceProvider == "T-Mobile")
            {
                MailMessage temp = new MailMessage(GetAWSParameter($"/LambdaTest/Email"), String.Format("{0}@tmomail.net", Decrypt(user.Phone)));
                temp.Subject = "Canvas Homework Reminder Test";
                temp.Body = message.TextMessage;

                mailMessage = temp;
            }
            else if(user.ServiceProvider == "Sprint")
            {
                MailMessage temp = new MailMessage(GetAWSParameter($"/LambdaTest/Email"), String.Format("{0}@messaging.sprintpcs.com", Decrypt(user.Phone)));
                temp.Subject = "Canvas Homework Reminder Test";
                temp.Body = message.TextMessage;

                mailMessage = temp;
            }
            else if(user.ServiceProvider == "ATT")
            {
                MailMessage temp = new MailMessage(GetAWSParameter($"/LambdaTest/Email"), String.Format("{0}@txt.att.net", Decrypt(user.Phone)));
                temp.Subject = "Canvas Homework Reminder Test";
                temp.Body = message.TextMessage;

                mailMessage = temp;
            }
            else if(user.ServiceProvider == "Verizon")
            {
                MailMessage temp = new MailMessage(GetAWSParameter($"/LambdaTest/Email"), String.Format("{0}@vtext.com", Decrypt(user.Phone)));
                temp.Subject = "Canvas Homework Reminder Test";
                temp.Body = message.TextMessage;

                mailMessage = temp;
            }
            else if(user.ServiceProvider == "MetroPCS")
            {
                MailMessage temp = new MailMessage(GetAWSParameter($"/LambdaTest/Email"), String.Format("{0}@mymetropcs.com", Decrypt(user.Phone)));
                temp.Subject = "Canvas Homework Reminder Test";
                temp.Body = message.TextMessage;

                mailMessage = temp;
            }

            //Setup an smtp client to send an email with specific options
            SmtpClient smtp = new SmtpClient("smtp.gmail.com");
            smtp.UseDefaultCredentials = false;
            smtp.Port = 587;
            smtp.EnableSsl = true;
            smtp.Timeout = 10000;
            smtp.Credentials = new System.Net.NetworkCredential(GetAWSParameter($"/LambdaTest/Email"), GetAWSParameter($"/LambdaTest/EmailPassword"));
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;

            //Send the message
            smtp.Send(mailMessage);
        }

        //Function to send an email to the users provided email address during sign up
        public void SendEmail(User user, Message message)
        {
            //Set up the mail message to send to the email address
            string toEmail = Decrypt(user.Email);
            MailMessage mailMessage = new MailMessage(GetAWSParameter($"/LambdaTest/Email"), toEmail);
            mailMessage.Subject = "Canvas Homework Reminder Test";
            mailMessage.Body = message.EmailMessage;

            //Setup the smtp client to send the email
            SmtpClient smtp = new SmtpClient("smtp.gmail.com");
            smtp.UseDefaultCredentials = false;
            smtp.Port = 587;
            smtp.EnableSsl = true;
            smtp.Timeout = 10000;
            smtp.Credentials = new System.Net.NetworkCredential(GetAWSParameter($"/LambdaTest/Email"), GetAWSParameter($"/LambdaTest/EmailPassword"));
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;        

            //Send the message
            smtp.Send(mailMessage);   
        }


        //Function to refresh the users access token
        public async Task Refresh(User user)
        {
            //Setup a JSON data contract serializer to parse through the returned JSON parameters
            var serializer = new DataContractJsonSerializer(typeof(Refresh));
            HttpClient client = new HttpClient();

            //Set the header values for the http request using a dictionary
            var headerValues = new Dictionary<string, string>()
            {
                {"grant_type", "refresh_token"},
                {"client_id", GetAWSParameter($"/CanvasProd/AppSecrets/Client_Id")},
                {"client_secret", GetAWSParameter($"/CanvasProd/AppSecrets/Client_Secret")},
                {"refresh_token", Decrypt(user.RefreshToken)}
                //May need to add the redirect uri header parameter

            };
            
            //Form encode the header values for a POST request
            var content = new FormUrlEncodedContent(headerValues);

            //Send a POST request asynchronously
            var response = await client.PostAsync("https://" +  GetAWSParameter($"/CanvasProd/AppSecrets/CanvasURL") +"/login/oauth2/token", content);

            //Read the response asynchronously
            var stream = response.Content.ReadAsStreamAsync();

            //Parse the results of the stream
            var results = (Refresh)serializer.ReadObject(stream.Result); // <---- Change courses to an OAUTH class

            //Update the database
            using(var db = new canvasremindwebappContext())
            {
                var change = db.User.Where(b => b.Name == user.Name && b.Email == user.Email && b.Phone == user.Phone).FirstOrDefault();
                
                change.AccessToken = Encrypt(results.accessToken);

                db.SaveChanges(); //may have fixed the warning

            }


            

        }

        public string Decrypt(string input)
        {
            //initialize all the variables used in decryption
            string decryptedString = "";
            byte[] keyByteArray = Convert.FromBase64String(GetAWSParameter($"/CanvasProd/AppSecrets/EncryptionKey")); //<---- Put information here
            byte[] IVByteArray = Convert.FromBase64String(GetAWSParameter($"/CanvasProd/AppSecrets/IV")); //<---- Put information here
            byte[] encryptedBytes = Convert.FromBase64String(input);

            using(Aes aes = Aes.Create())
            {
                //Set the aes key and IV to values in the AWS Parameter store
                aes.Key = keyByteArray;
                aes.IV = IVByteArray;

                //Create a decryptor using the aes key and IV set beforehand
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                //Create a memory stream to read the encrypted bytes and write the converted ones to the string
                using(MemoryStream mem = new MemoryStream(encryptedBytes))
                {
                    using(CryptoStream crypto = new CryptoStream(mem, decryptor, CryptoStreamMode.Read))
                    {
                        using(StreamReader reader = new StreamReader(crypto))
                        {
                            //Read the bytes to end into a string
                            decryptedString = reader.ReadToEnd();
                        }
                    }
                }
            }

            //Return the decrypted string
            return decryptedString;
        }

        //Function to encrypt an input using the encryption keys in AWS
        public string Encrypt(string input)
        {
            //Setup all variables needed 
            string encryptedString = "";
            byte[] keyByteArray = Convert.FromBase64String(GetAWSParameter($"/CanvasProd/AppSecrets/EncryptionKey"));
            byte[] IVByteArray = Convert.FromBase64String(GetAWSParameter($"/CanvasProd/AppSecrets/IV"));
            byte[] encryptedBytes;

            //Create a new instance of an AES object
            using(Aes aes = Aes.Create())
            {
                //Set the Key and IV
                aes.Key = keyByteArray;
                aes.IV = IVByteArray;

                //Create a new encryptor
                ICryptoTransform crypto = aes.CreateEncryptor(aes.Key, aes.IV);

                using(MemoryStream mem = new MemoryStream())
                {
                    using(CryptoStream crypt = new CryptoStream(mem, crypto, CryptoStreamMode.Write))
                    {
                        using(StreamWriter sw = new StreamWriter(crypt))
                        {
                            //write to the memory stream the information by encrypting it using the CryptoStream
                            sw.Write(input);
                        }
                        
                        //Write the memory stream bytes to a byte array
                        encryptedBytes = mem.ToArray();
                    }
                }
            }

            //convert the encrypted byte array to a string value
            encryptedString = Convert.ToBase64String(encryptedBytes);   

            //return the encrypted string
            return encryptedString;
        }
    }
}
