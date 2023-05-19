using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace GoogleDriveUpload
{
    public class DriveServiceHelper
    {
        private readonly DriveService _service;

        public DriveServiceHelper(DriveService service)
        {
            _service = service;
        }

        public string CreateFolder(string folderName)
        {
            var folders = _service.Files.List();
            folders.Q = "mimeType='application/vnd.google-apps.folder' and trashed=false";
            folders.Fields = "nextPageToken, files(id, name)";
            var result = folders.Execute();
            var folder = result.Files.FirstOrDefault(x => x.Name == folderName);
            if (folder != null)
                return folder.Id;

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder"
            };
            var request = _service.Files.Create(fileMetadata);
            request.Fields = "id";
            var file = request.Execute();
            Console.WriteLine("New Folder Created Successfully...");
            Console.WriteLine(" ");
            return file.Id;
        }

        public string UploadFile(string filePath, string contentType, string folderId)
        {
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = Path.GetFileName(filePath),
                Parents = new[] { folderId }
            };
            FilesResource.CreateMediaUpload request;
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                request = _service.Files.Create(fileMetadata, stream, contentType);
                request.Fields = "id";
                request.Upload();
                Console.WriteLine("File Successfully Uploaded...");
            }
            var file = request.ResponseBody;
            return file.Id;
        }

        public void DownloadFile(string folderId)
        {
            var files = _service.Files.List();
            files.Q = $"'{folderId}' in parents and trashed=false";
            files.Fields = "nextPageToken, files(id, name)";
            var result = files.Execute();
            var fileList = result.Files;

            if (fileList.Count == 0)
                Console.WriteLine("No files found in folder.");
            else
            {
                Console.WriteLine(" ");
                Console.WriteLine("Files in folder:");
                Console.WriteLine(" ");
                foreach (var file in fileList)
                    Console.WriteLine("{0}", file.Name);
            }

            Console.WriteLine(" ");
            Console.Write("Enter the name of the file you want to download:");
            string fileName = Console.ReadLine();

            var fileToDownload = fileList.FirstOrDefault(x => x.Name == fileName);

            if (fileToDownload == null)
                Console.WriteLine("File not found: {0}", fileName);
            else
            {
                string downloadPath;
                if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                    downloadPath = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "Downloads", fileName);
                else
                    downloadPath = Path.Combine(Environment.ExpandEnvironmentVariables("%USERPROFILE%"), "Downloads", fileName);

                int count = 1;
                while (File.Exists(downloadPath))
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                    string extension = Path.GetExtension(fileName);
                    downloadPath = Path.Combine(Path.GetDirectoryName(downloadPath), fileNameWithoutExtension + " (" + count + ")" + extension);
                    count++;
                }

                var request = _service.Files.Get(fileToDownload.Id);
                using (var stream = new FileStream(downloadPath, FileMode.Create))
                {
                    request.Download(stream);
                }
            }
        }



        public static DriveService Authenticate()
        {
            UserCredential credential;

            using (var stream =
                new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    new[] { DriveService.Scope.Drive },
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Drive API Sample",
            });

            return service;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Enter the name of the folder,where you want to store Your File: ");
            string folderName = Console.ReadLine();
            Console.WriteLine(" ");

            var service = DriveServiceHelper.Authenticate();
            var helper = new DriveServiceHelper(service);

            string folderId = helper.CreateFolder(folderName);

            Console.Write("Do you want to upload, download or do both? (Enter 'upload', 'download' or 'both') : ");
            string operation = Console.ReadLine();
            Console.WriteLine(" ");

            if (operation.ToLower() == "upload" || operation.ToLower() == "both")
            {
                Console.Write("Enter the number of files to upload: ");
                int numFiles = int.Parse(Console.ReadLine());
                Console.WriteLine(" "); 

                string[] userINPUT = new string[numFiles];

                for (int i = 0; i < numFiles; i++)
                {
                    Console.Write("Enter the path of file {0}: ", i + 1);
                    userINPUT[i] = Console.ReadLine();
                }

                foreach (string filePath in userINPUT)
                {
                    string contentType;

                    switch (Path.GetExtension(filePath).ToLower())
                    {
                        case ".pdf":
                            contentType = "application/pdf";
                            break;
                        case ".jpg":
                        case ".jpeg":
                            contentType = "image/jpeg";
                            break;
                        case ".png":
                            contentType = "image/png";
                            break;
                        default:
                            Console.WriteLine("File type not supported. Only (PDF/JPG/PNG) supported.");
                            continue;
                    }

                    helper.UploadFile(filePath, contentType, folderId);
                }
            }

            if (operation.ToLower() == "download" || operation.ToLower() == "both")
            {
                helper.DownloadFile(folderId);
            }

            if (operation.ToLower() != "upload" && operation.ToLower() != "download" && operation.ToLower() != "both")
            {
                Console.WriteLine("Invalid operation. Please enter 'upload', 'download' or 'both'.");
            }
        }
    }
}
