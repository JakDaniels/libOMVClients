using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Drawing;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using OpenMetaverse;
using OpenMetaverse.Http;
using OpenMetaverse.Imaging;
using OpenMetaverse.ImportExport;
using OpenMetaverse.StructuredData;

namespace cmdGridMeshUpload
{
    public class cmdGridMeshUpload
    {

        private GridClient Client;
        private string FileName = String.Empty;
        private string LoginURL = String.Empty;
        private string FirstName = String.Empty;
        private string LastName = String.Empty;
        private string Password = String.Empty;
        private bool UploadImages = true;
        private bool NoResize = false;
        private bool ShowUsage = false;
        private int Debug = 0;

        private bool Connected = false;
        private bool ConnectFailed = false;
        private bool EventQueueRunning = false;
        private bool UploadComplete = false;
        private bool UploadFailed = false;

        private UUID UploadedMeshUUID;

        public static int Main(string[] args)
        {
            //args should be --login=LoginURL --first=FirstName --last=LastName ==password=Password --file=PathToMesh [--cc-first=CopyToFirstName] [--cc-last=CopyToLastName] [--upload-images] [--no-resize]

            cmdGridMeshUpload m = new cmdGridMeshUpload();

            Dictionary<string, string> arg = m.ParseArgs(args);

            if (arg.ContainsKey("login"))
            {
                m.LoginURL = arg["login"];
            }
            else
            {
                m.ShowUsage = true;
            }

            if (arg.ContainsKey("first"))
            {
                m.FirstName = arg["first"];
            }
            else
            {
                m.ShowUsage = true;
            }

            if (arg.ContainsKey("last"))
            {
                m.LastName = arg["last"];
            }
            else
            {
                m.ShowUsage = true;
            }

            if (arg.ContainsKey("password"))
            {
                m.Password = arg["password"];
            }
            else
            {
                m.ShowUsage = true;
            }

            if (arg.ContainsKey("file"))
            {
                m.FileName = arg["file"];
            }
            else
            {
                m.ShowUsage = true;
            }

            if (arg.ContainsKey("upload-images"))
            {
                m.UploadImages = true;
            }
            if (arg.ContainsKey("no-resize"))
            {
                m.NoResize = true;
            }
            
            if (arg.ContainsKey("d")) Int32.TryParse(arg["d"], out m.Debug);
            else if(arg.ContainsKey("debug")) Int32.TryParse(arg["debug"], out m.Debug);
            
            if (m.ShowUsage)
            {
                Console.WriteLine("Usage: GridMeshUploadCmd.exe --login=LoginURL --first=FirstName --last=LastName --password=Password --file=PathToMesh [--upload-images] [--no-resize]");
                return 1;
            }

            m.InitClient();
            //initiate login
            LoginParams lp = m.Client.Network.DefaultLoginParams(m.FirstName, m.LastName, m.Password, "GridMeshUploadCmd", "0.1");
            lp.URI = m.LoginURL;
            m.Client.Network.BeginLogin(lp);

            while (!m.Connected)
            {
                if (m.ConnectFailed) return 1;
                System.Threading.Thread.Sleep(500);
            }


            while (!m.EventQueueRunning) System.Threading.Thread.Sleep(500);

            if (m.UploadMesh())
            {
                while (!m.UploadComplete) System.Threading.Thread.Sleep(500);

            }
            else
            {
                Console.WriteLine("Error Uploading Mesh");
            }

            //initiate logout
            if (m.Connected)
            {
                Console.WriteLine("Logging Out");
                m.Client.Network.Logout();
            }
            m.Client = null;
            if (m.UploadFailed) return 1; else return 0;
        }

        private Dictionary<string, string> ParseArgs(string[] args)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            int i = 0;
            string a;
            string v = "";
            int ov = 0;

            while (i < args.Length)
            {
                if (args[i].Substring(0, 2) == "--") a = args[i].Substring(2, args[i].Length - 2);
                else
                {
                    if (args[i].Substring(0, 1) == "-") a = args[i].Substring(1, args[i].Length - 1);
                    else
                    {
                        a = String.Format("{0}", ov);
                        ov++;
                    }
                }
                if (a.Contains("="))
                {
                    string[] tmp = a.Split("=".ToCharArray());
                    //Console.WriteLine("{0}:{1}", tmp[0], tmp[1]);
                    dictionary.Add(tmp[0], tmp[1]);
                }
                else
                {
                    if (args[i].Substring(0, 1) == "-" || i == args.Length) v = "1";
                    else v = args[i];
                    //Console.WriteLine("{0}:{1}", a, v);
                    dictionary.Add(a, v);
                }
                i++;
            }
            return dictionary;
        }

        private void InitClient()
        {
            Client = new GridClient();
            Client.Network.EventQueueRunning += Network_OnEventQueueRunning;
            Client.Network.LoginProgress += Network_OnLogin;

            // Turn almost everything off since we are only interested in uploading textures
            Settings.LOG_LEVEL = Helpers.LogLevel.None;
            Client.Settings.ALWAYS_DECODE_OBJECTS = false;
            Client.Settings.ALWAYS_REQUEST_OBJECTS = false;
            Client.Settings.SEND_AGENT_UPDATES = true;
            Client.Settings.OBJECT_TRACKING = false;
            Client.Settings.STORE_LAND_PATCHES = false;
            Client.Settings.MULTIPLE_SIMS = false;
            Client.Self.Movement.Camera.Far = 32.0f;
            Client.Throttle.Cloud = 0.0f;
            Client.Throttle.Land = 0.0f;
            Client.Throttle.Wind = 0.0f;

            Client.Throttle.Texture = 446000.0f;
        }

        private void Network_OnLogin(object sender, LoginProgressEventArgs e)
        {

            if (e.Status == LoginStatus.Success)
            {
                Connected = true;
            }
            else if (e.Status == LoginStatus.Failed)
            {
                Console.WriteLine("Error logging in ({0}): {1}", Client.Network.LoginErrorKey, Client.Network.LoginMessage);
                ConnectFailed = true;
            }

        }

        private void Network_OnEventQueueRunning(object sender, EventQueueRunningEventArgs e)
        {
            //Console.WriteLine("Event queue is running for {0}, enabling uploads");
            EventQueueRunning = true;
        }

        private bool UploadMesh()
        {
            Console.WriteLine("Processing: {0}", FileName);

            var parser = new cmdColladaLoader();
            parser.Debug = Debug;
            parser.Resize = !NoResize;
            var prims = parser.Load(FileName, UploadImages);
            if (prims == null || prims.Count == 0)
            {
                Console.WriteLine("Error: Failed to parse collada file");
                return false;
            }

            Console.WriteLine("Parse collada file success, found {0} objects", prims.Count);

            Console.WriteLine("Uploading...");

            string ObjectName=Path.GetFileNameWithoutExtension(FileName);

            UUID ObjectFolder = Client.Inventory.FindFolderForType(AssetType.Object);
            UUID MyObjectFolder = Client.Inventory.CreateFolder(ObjectFolder, ObjectName);
            UUID MyTexturesFolder = Client.Inventory.CreateFolder(MyObjectFolder, "Textures");

            Console.WriteLine("Created New Folder under 'Objects' called '{0}' with UUID {1}", ObjectName, MyObjectFolder);
            Console.WriteLine("Created New Folder under '{0}' called 'Textures' with UUID {1}", ObjectName, MyTexturesFolder);
            
            var uploader = new cmdModelUploader(Client, prims);
            var uploadDone = new AutoResetEvent(false);

            uploader.IncludePhysicsStub = true;
            uploader.UseModelAsPhysics = false;
            uploader.InvName = ObjectName;
            uploader.InvDescription = "Uploaded by GridUploadMeshCmd on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            uploader.InvAssetFolderUUID = MyObjectFolder;
            uploader.InvTextureFolderUUID = MyTexturesFolder;
            uploader.UploadTextures = UploadImages;
            uploader.Debug = Debug;

            uploader.Upload((res=>

            {
                if (res == null)
                {
                    Console.WriteLine("Upload failed.");
                    UploadComplete = true;
                    UploadFailed = true;
                }
                else
                {
                    UploadedMeshUUID = uploader.ReturnedMeshUUID;
                    Console.WriteLine("Upload success. Uploaded Mesh has Asset UUID: {0}", UploadedMeshUUID);
                    UploadComplete = true;
                }

                uploadDone.Set();
            }));

            if (!uploadDone.WaitOne(4 * 60 * 1000))
            {
                Console.WriteLine("Message upload timeout");
                UploadComplete = true;
                UploadFailed = true;
            }
            return true;
        }

    }
}
