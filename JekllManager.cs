using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using LibGit2Sharp;
using System.Collections.Generic;
using LibGit2Sharp.Handlers;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.StaticFiles;
using System.Reflection;

namespace JekyllManager
{


    public static class JekllManager
    {
        private static char seperator = Path.DirectorySeparatorChar;
        public static string path = Path.DirectorySeparatorChar == '\\' ? Environment.ExpandEnvironmentVariables("%HOME%") + "\\repo" : "/home/repo";
        public static string gitUser = Environment.GetEnvironmentVariable("GITUSER");
        public static string gitEmail = Environment.GetEnvironmentVariable("GITEMAIL");
        public static string gitToken = Environment.GetEnvironmentVariable("GITPAT"); 
        public static string gitBranch = Environment.GetEnvironmentVariable("GITBRANCH"); 
        public static string gitUrl = Environment.GetEnvironmentVariable("GITREPO"); 


        [FunctionName("PullOrClone")]
        public static async Task<IActionResult> PullOrClone([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            if (Directory.GetFiles(path).Length == 0) Clone();
            else Pull();
            return new OkObjectResult("OK");
        }

        [FunctionName("Push")]
        public static async Task<IActionResult> PushChanges([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            try
            {
                Push();
                return new OkObjectResult("OK");
            }
            catch (Exception e)
            {
                return new BadRequestObjectResult(e.Message);
            }
        }

        [FunctionName("ListFiles")]
        public static async Task<IActionResult> ListFiles([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            var content = await new StreamReader(req.Body).ReadToEndAsync();
            ListOptions listOptions = JsonConvert.DeserializeObject<ListOptions>(content);

            string filePath = path;

            if (listOptions.folder.StartsWith(seperator))
                filePath += listOptions.folder;
            else
                filePath += seperator + listOptions.folder;

            EnumerationOptions e = new EnumerationOptions();
            e.RecurseSubdirectories = listOptions.recurse;

            //List<string> files = new List<string>();

            FileList fileList = new FileList();
            fileList.folder = listOptions.folder;
            

            string filter = "*";
            if (listOptions.extension != String.Empty) filter = "*." + listOptions.extension;

            if (listOptions.includeFolders) 
            {
                foreach (string dir in Directory.GetDirectories(filePath)) 
                {
                    ListFile lf = new ListFile();
                    lf.name = dir;
                    lf.isFolder = true;
                    fileList.files.Add(lf);
                }
            }


            for (int i = 0; i < fileList.files.Count; i++)
            {
                fileList.files[i].name = fileList.files[i].name + seperator;
            }

            //files.AddRange(Directory.GetFiles(filePath, filter, e));

            if (Directory.Exists(filePath)) 
            {
                foreach (string f in Directory.GetFiles(filePath, filter, e))
                {
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                    ListFile lf = new ListFile();
                    lf.name = f;

                    if (listOptions.includeMetadata)
                    {
                        FileInfo fi = new FileInfo(f);
                        lf.metadata.Add("size", fi.Length);
                        lf.metadata.Add("createddate", fi.CreationTimeUtc);
                        lf.metadata.Add("modified", fi.LastWriteTimeUtc);
                        string contentType = null;
                        new FileExtensionContentTypeProvider().TryGetContentType(f, out contentType);
                        if (contentType == null) contentType = "application/octet-stream";
                        lf.metadata.Add("contentType", contentType);

                        if (fi.Extension.ToLower() == ".md")
                        {
                            bool startHead = false;
                            foreach (string line in System.IO.File.ReadLines(f))
                            {
                                if (line.StartsWith("---") && !startHead) startHead = true;
                                else if (line.StartsWith("---") && startHead) break;
                                List<string> lineParts = line.Split(":").ToList();
                                if (lineParts.Count >= 2)
                                {
                                    string key = lineParts[0];
                                    lineParts.RemoveAt(0);
                                    string value = String.Join(":", lineParts);
                                    lf.metadata.Add(key, value.Trim());
                                }
                            }
                        }
                    }
                    fileList.files.Add(lf);
                }
            }



            for (int i = 0; i < fileList.files.Count; i++)
            {
                fileList.files[i].name = fileList.files[i].name.Replace(path, "");
            }

            if (listOptions.excludedFolders.Count > 0) 
            {
                for (int i = fileList.files.Count - 1; i >= 0; i--)
                {
                    bool remove = false;
                    for (int j = 0; j < listOptions.excludedFolders.Count; j++) 
                    {
                        if (fileList.files[i].name.StartsWith(seperator + listOptions.excludedFolders[j]))
                        {
                            remove = true;   
                        }
                    }
                    if (remove) 
                    {
                        fileList.files.RemoveAt(i);
                    }
                }
            }


            return new OkObjectResult(fileList);
        }




        [FunctionName("GetFile")]
        public static async Task<IActionResult> GetFile([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            string filepath = "";

            if (req.Query.ContainsKey("filepath"))
            {
                filepath = path + req.Query["filepath"];

                if (!File.Exists(filepath)) 
                { 
                    return new BadRequestObjectResult("File Not Found");
                }

            }
            else
            {
                return new BadRequestObjectResult("File Path is required.");
            }

            if (filepath != "")
            {
                byte[] filebytes = File.ReadAllBytes(filepath);
                return new FileContentResult(filebytes, "application/octet-stream")
                {
                    FileDownloadName = Path.GetFileName(filepath)
                };

            }

            return new BadRequestObjectResult("File Not Found");
        }

        [FunctionName("MoveFile")]
        public static async Task<IActionResult> MoveFile([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            string filepath = "";
            string destinationpath = "";

            if (req.Query.ContainsKey("filepath") && req.Query.ContainsKey("destinationpath"))
            {
                filepath = path + req.Query["filepath"];
                destinationpath = path + req.Query["destinationpath"];

                if (!File.Exists(filepath))
                {
                    return new BadRequestObjectResult("File Not Found");
                }

            }
            else
            {
                return new BadRequestObjectResult("File Path and Destination arerequired.");
            }

            if (filepath != "" && destinationpath != "")
            {
                File.Move(filepath, destinationpath);
                Stage("Moved: " + filepath + " to " + destinationpath);
                return new OkObjectResult("OK");
            }

            return new BadRequestObjectResult("File Not Found");
        }

        [FunctionName("static")]
        public static async Task<IActionResult> GetStatic([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req, ILogger log)
        {
            

            string filepath = "";

            if (req.Query.ContainsKey("file"))
            {
                filepath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + seperator + ".." + seperator + "assets" + seperator + req.Query["file"];

                if (!File.Exists(filepath))
                {
                    return new BadRequestObjectResult("File Not Found");
                }

            }
            else
            {
                filepath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + seperator + ".."   + seperator + "assets" + seperator + "index.html";
            }

            if (filepath != "")
            {
                string content = File.ReadAllText(filepath, Encoding.UTF8);

                ContentResult c = new ContentResult();
                c.ContentType = "text/html";
                c.Content = content;
                return c;
            }

            return new BadRequestObjectResult("File Not Found");
        }




        [FunctionName("UploadFile")]
        public static async Task<IActionResult> UploadFile([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            var formData = await req.ReadFormAsync();


            if (!formData.ContainsKey("filepath")) 
            {
                return new BadRequestObjectResult("File Path is required.");
            }

            if (formData.Files.Count != 1) 
            {
                return new BadRequestObjectResult("Only one file at a time is supported.");
            }

            string filepath = formData["filepath"];


            if (filepath.Length > 0 && filepath[0] == seperator)
            {
                filepath = path + filepath;
            }
            else if (filepath.Length > 0)
            {
                filepath = path + seperator + filepath;
            }
            else 
            {
                filepath = path + seperator;
            }

            var file = formData.Files[0];

            if(filepath[filepath.Length -1 ] == seperator || filepath[filepath.Length - 1] == '/') filepath += file.FileName;

            using (var fileStream = File.Create(filepath))
            {
                var fs = file.OpenReadStream();
                fs.Seek(0, SeekOrigin.Begin);
                fs.CopyTo(fileStream);

            }
            Stage("Uploaded: " + filepath);

            return new OkObjectResult("OK");
        }

        [FunctionName("DeleteFile")]
        public static async Task<IActionResult> DeleteFile([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            string filepath = "";

            if (req.Query.ContainsKey("filepath"))
            {
                filepath = path + req.Query["filepath"];

                if (!File.Exists(filepath))
                {
                    return new BadRequestObjectResult("File Not Found");
                }

            }
            else
            {
                return new BadRequestObjectResult("File Path is required.");
            }

            if (filepath != "")
            {
                File.Delete(filepath);
                Stage("Deleted: " + filepath);
                return new OkObjectResult("OK");
            }

            return new BadRequestObjectResult("File Not Found");
        }

        private static void Push() 
        {
            Repository repo = new Repository(path);

            PushOptions options = new LibGit2Sharp.PushOptions();
            options.CredentialsProvider = new CredentialsHandler(
                (url, usernameFromUrl, types) =>
                    new UsernamePasswordCredentials()
                    {
                        Username = gitToken,
                        Password = ""
                    });

            repo.Network.Push(repo.Branches[gitBranch], options);
        }

        private static void Stage(string message) 
        {


            Repository repo = new Repository(path);


            LibGit2Sharp.Commands.Stage(repo, "*");

            Signature sig = new Signature(gitUser, gitEmail, DateTimeOffset.UtcNow);

            repo.Commit(message, sig, sig);


        }

        private static void Pull() 
        {
            Repository repo = new Repository(path);
            var pullOptions = new PullOptions();
            pullOptions.FetchOptions = new FetchOptions();
            pullOptions.FetchOptions.CredentialsProvider = new CredentialsHandler((url, usernameFromUrl, types) =>
                new UsernamePasswordCredentials()
                {
                    Username = gitToken,
                    Password = ""
                });
            Signature sig = new Signature(gitUser, gitEmail, DateTimeOffset.UtcNow);
            LibGit2Sharp.Commands.Pull(repo, sig, pullOptions);
        }

        private static void Clone()
        {



            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            CloneOptions options = new CloneOptions()
            {
                BranchName = gitBranch
            };

            options.CredentialsProvider = new CredentialsHandler((url, usernameFromUrl, types) =>
                new UsernamePasswordCredentials()
                {
                    Username = gitToken,
                    Password = ""
                });

            Repository.Clone(gitUrl, path, options);
        }


    }

    public class Comment
    {
        public string name = "";
        public string email = "";
        public string site = "";
        public string comment = "";
        public bool approved = false;
        public string pageId = "";
    }

    public class ListOptions
    {
        public string folder = "";
        public bool recurse = true;
        public string extension = "";
        public bool includeFolders = false;
        public List<string> excludedFolders = new List<string>();
        public bool includeMetadata = false;
    }

    public class FileList {
        public string folder = "";
        public List<ListFile> files = new List<ListFile>();
    }

    public class ListFile 
    {
        public string name = "";
        public bool isFolder = false;
        public Dictionary<string, object> metadata = new Dictionary<string, object>();
    }


}
