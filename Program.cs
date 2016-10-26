using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Test.Tomn
{
    class Program
    {
        static void Main(string[] args)
        {
            var objC = new Contact { Name = "13234" }; objC.Adress.Name = "777";

            UpdateObjectValue(objC, "Adress.Name", "5555");


            FileStream fs = new FileStream("c:\\temp\\people.doc", FileMode.Open, FileAccess.Read);
            byte[] data = new byte[fs.Length];
            fs.Read(data, 0, data.Length);
            fs.Close();

            // Generate post objects
            Dictionary<string, object> postParameters = new Dictionary<string, object>();
            postParameters.Add("filename", "People.doc");
            postParameters.Add("fileformat", "doc");
            postParameters.Add("file", new FormUpload.FileParameter(data, "People.doc", "application/msword"));

            // Create request and receive response
            string postURL = "http://localhost/TOMN.Surefire.API/media/library/collection/a440e18a-ee35-4568-b638-5f25b519aa65/asset/phoneCall";
            string userAgent = "Someone";
            HttpWebResponse webResponse = FormUpload.MultipartFormDataPost(postURL, userAgent, postParameters);

            // Process response
            StreamReader responseReader = new StreamReader(webResponse.GetResponseStream());
            string fullResponse = responseReader.ReadToEnd();
            webResponse.Close();
            //Response.Write(fullResponse);

            //var data = new PowerCallForEdit
            //{
            //    Id = Guid.NewGuid(),
            //    Contact = new ReadonlyContact
            //    {
            //        Id = Guid.Parse("94312676-F419-4796-A0AF-F7A64017B30D")
            //    },
            //    Reason = "Call me!",
            //    User_Id = Guid.Parse("EBC914BD-1C53-4F75-9DB7-899C1A18212C"),
            //    ScheduledDatetime = DateTimeOffset.Now.AddDays(3),
            //    DueDate = DateTimeOffset.Now.AddDays(6),
            //    IsDismissed = false,
            //    CallLength = new TimeSpan(0,5,0)
            //};

            //var json = new JavaScriptSerializer().Serialize(data);
        }

        public static void UpdateObjectValue(object target, string fieldName, object value)
        {
            if (fieldName.Contains("."))
            {
                string[] bits = fieldName.Split('.');
                for (int i = 0; i < bits.Length - 1; i++)
                {
                    PropertyInfo propertyToGet = target.GetType().GetProperty(bits[i]);
                    target = propertyToGet.GetValue(target, null);
                }
                PropertyInfo propertyToSet = target.GetType().GetProperty(bits.Last());
                propertyToSet.SetValue(target, value, null);
            }
            else
            {
                Type type = target.GetType();

                var prop = type.GetProperty(fieldName);
                prop.SetValue(target, value);
            }
        }
    }

    public class Contact
    {
        public Contact()
        {
            Adress = new Adress();

            //234234
        }

        public string Name { get; set; }

        public Adress Adress { get; set; }
    }

    public class Adress
    {
        public string Name { get; set; }
    }

    public static class FormUpload
    {
        private static readonly Encoding encoding = Encoding.UTF8;
        public static HttpWebResponse MultipartFormDataPost(string postUrl, string userAgent, Dictionary<string, object> postParameters)
        {
            string formDataBoundary = String.Format("----------{0:N}", Guid.NewGuid());
            string contentType = "multipart/form-data; boundary=" + formDataBoundary;

            byte[] formData = GetMultipartFormData(postParameters, formDataBoundary);

            return PostForm(postUrl, userAgent, contentType, formData);
        }
        private static HttpWebResponse PostForm(string postUrl, string userAgent, string contentType, byte[] formData)
        {
            HttpWebRequest request = WebRequest.Create(postUrl) as HttpWebRequest;

            if (request == null)
            {
                throw new NullReferenceException("request is not a http request");
            }

            // Set up the request properties.
            request.Method = "POST";
            request.ContentType = contentType;
            request.UserAgent = userAgent;
            request.CookieContainer = new CookieContainer();
            request.ContentLength = formData.Length;

            // You could add authentication here as well if needed:
            // request.PreAuthenticate = true;
            // request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequested;
            // request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.Default.GetBytes("username" + ":" + "password")));

            // Send the form data to the request.
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(formData, 0, formData.Length);
                requestStream.Close();
            }

            return request.GetResponse() as HttpWebResponse;
        }

        private static byte[] GetMultipartFormData(Dictionary<string, object> postParameters, string boundary)
        {
            Stream formDataStream = new System.IO.MemoryStream();
            bool needsCLRF = false;

            foreach (var param in postParameters)
            {
                // Thanks to feedback from commenters, add a CRLF to allow multiple parameters to be added.
                // Skip it on the first parameter, add it to subsequent parameters.
                if (needsCLRF)
                    formDataStream.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                needsCLRF = true;

                if (param.Value is FileParameter)
                {
                    FileParameter fileToUpload = (FileParameter)param.Value;

                    // Add just the first part of this param, since we will write the file data directly to the Stream
                    string header = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\"\r\nContent-Type: {3}\r\n\r\n",
                        boundary,
                        param.Key,
                        fileToUpload.FileName ?? param.Key,
                        fileToUpload.ContentType ?? "application/octet-stream");

                    formDataStream.Write(encoding.GetBytes(header), 0, encoding.GetByteCount(header));

                    // Write the file data directly to the Stream, rather than serializing it to a string.
                    formDataStream.Write(fileToUpload.File, 0, fileToUpload.File.Length);
                }
                else
                {
                    string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
                        boundary,
                        param.Key,
                        param.Value);
                    formDataStream.Write(encoding.GetBytes(postData), 0, encoding.GetByteCount(postData));
                }
            }

            // Add the end of the request.  Start with a newline
            string footer = "\r\n--" + boundary + "--\r\n";
            formDataStream.Write(encoding.GetBytes(footer), 0, encoding.GetByteCount(footer));

            // Dump the Stream into a byte[]
            formDataStream.Position = 0;
            byte[] formData = new byte[formDataStream.Length];
            formDataStream.Read(formData, 0, formData.Length);
            formDataStream.Close();

            return formData;
        }

        public class FileParameter
        {
            public byte[] File { get; set; }
            public string FileName { get; set; }
            public string ContentType { get; set; }
            public FileParameter(byte[] file) : this(file, null) { }
            public FileParameter(byte[] file, string filename) : this(file, filename, null) { }
            public FileParameter(byte[] file, string filename, string contenttype)
            {
                File = file;
                FileName = filename;
                ContentType = contenttype;
            }
        }
    }

    public class PowerCallForEdit
    {
        public Guid Id { get; set; }

        public ReadonlyContact Contact { get; set; }

        public Guid? CallNumber_Id { get; set; }

        public string Reason { get; set; }

        public Guid User_Id { get; set; }

        public Guid? ContentCollection_Id { get; set; }

        public DateTimeOffset ScheduledDatetime { get; set; }

        public DateTimeOffset DueDate { get; set; }

        public DateTimeOffset? CompletionDate { get; set; }

        public bool IsDismissed { get; set; }

        //public CompletionStatus? CompletionStatus { get; set; }

        public string CompletionNotes { get; set; }

        public TimeSpan? CallLength { get; set; }
    }

    public class ReadonlyContact
    {
        public Guid Id { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string FullName => $"{this.FirstName} {this.LastName}";

        public string Email { get; set; }

        public string Mobile { get; set; }

        public string Home { get; set; }

        public string Work { get; set; }

        public ReadonlyContactType ContactType { get; set; }

        public ContactPreference ContactPreference { get; set; }

        public string OrganizationName { get; set; }

        public List<ReadonlyContactTag> Tags { get; set; }
    }

    public class ReadonlyContactType
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public bool IsLead { get; set; }
    }

    public enum ContactPreference
    {
        Email = 0,
        Mobile = 1,
        Home = 2,
        Mail = 3
    }

    public class ReadonlyContactTag
    {
        public Guid Id { get; set; }

        public string Text { get; set; }
    }

}
