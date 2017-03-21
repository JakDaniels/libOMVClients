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
        private string LoginRegion = String.Empty;
        private string FirstName = String.Empty;
        private string LastName = String.Empty;
        private string Password = String.Empty;
        private string ccFirstName = String.Empty;
        private string ccLastName = String.Empty;
        private bool UploadTextures = true;
        private bool AllowOversize = false;
        private bool PhysicsFromMesh = false;
        private float TextureScale = 1.0f;
        private int Debug = 0;

        private bool ShowUsage = false;

        private bool Connected = false;
        private bool ConnectFailed = false;
        private bool EventQueueRunning = false;
        private bool UploadComplete = false;
        private bool UploadFailed = false;

        private string ObjectName;
        private string ObjectTmpName;
        private List<string> ImageNames;
        private UUID UploadedMeshUUID;
        private UUID UploadedMeshInvUUID;
        private UUID MyObjectFolder;
        private UUID MyTexturesFolder;
        private UUID ccUUID = UUID.Zero;
        public string ClientVersion = "0.2";

        public static int Main(string[] args)
        {
            //args should be --login=LoginURL --first=FirstName --last=LastName ==password=Password --file=PathToMesh 
            //               [--cc-first=CopyToFirstName] [--cc-last=CopyToLastName] [--upload-textures] [--allow-oversize] [--texture-scale=ScaleFactor] [--physics-from-mesh]

            cmdGridMeshUpload m = new cmdGridMeshUpload();

            Dictionary<string, string> arg = m.ParseArgs(args);

            if (arg.ContainsKey("login")) m.LoginURL = arg["login"]; else m.ShowUsage = true;

            if (arg.ContainsKey("login-region")) m.LoginRegion = arg["login-region"];

            if (arg.ContainsKey("first")) m.FirstName = arg["first"]; else m.ShowUsage = true;

            if (arg.ContainsKey("last")) m.LastName = arg["last"]; else m.ShowUsage = true;

            if (arg.ContainsKey("password")) m.Password = arg["password"]; else m.ShowUsage = true;

            if (arg.ContainsKey("file")) m.FileName = arg["file"]; else m.ShowUsage = true;
            
            if (arg.ContainsKey("cc-first")) m.ccFirstName = arg["cc-first"];
            
            if (arg.ContainsKey("cc-last")) m.ccLastName = arg["cc-last"];
            
            if (arg.ContainsKey("upload-textures")) m.UploadTextures = true;
            
            if (arg.ContainsKey("allow-oversize")) m.AllowOversize = true;

            if (arg.ContainsKey("physics-from-mesh")) m.PhysicsFromMesh = true;
            
            if (arg.ContainsKey("texture-scale"))  float.TryParse(arg["texture-scale"], out m.TextureScale);
            
            if (arg.ContainsKey("h") || arg.ContainsKey("help")) m.ShowUsage = true;
            
            if (arg.ContainsKey("d")) Int32.TryParse(arg["d"], out m.Debug);
            else if(arg.ContainsKey("debug")) Int32.TryParse(arg["debug"], out m.Debug);
            
            if (m.ShowUsage)
            {
                Console.WriteLine("GridMeshUploadCmd v" + m.ClientVersion);
                Console.WriteLine();
                Console.WriteLine("Usage: GridMeshUploadCmd.exe --login=LoginURL --first=FirstName --last=LastName --password=Password --file=PathToMesh               ");
                Console.WriteLine("                             [--cc-first=CopyToFirstName] [--cc-last=CopyToLastName] [--upload-textures] [--allow-oversize]         ");
                Console.WriteLine("                             [--texture-scale=ScaleFactor] [--physics-from-mesh] [--login-region=RegionName]                                                                        ");
                Console.WriteLine("                                                                                                                                    ");
                Console.WriteLine("This commandline client can connect to your grid and upload mesh assets. The mesh asset is described by a Collada (.dae) file and   ");
                Console.WriteLine("any associated texture image files in the format .bmp, .tga, .png or .jpg. All the files must reside in the same source directory.  ");
                Console.WriteLine("Being a commandline client (as opposed to a GUI client), it is easy to use in automation scripts to speed up the workflow of getting");
                Console.WriteLine("your mesh assets into OpenSimulator. The client can also optionally copy the uploaded assets to another avatar and perform other    ");
                Console.WriteLine("operations to speed up the workflow.                                                                                                ");
                Console.WriteLine();
                Console.WriteLine("--login               This is the login URI for your grid. e.g. OSGrid is http://login.osgrid.org:80                                ");
                Console.WriteLine("--first               The first name of the avatar the client uses to login                                                         ");
                Console.WriteLine("--last                The last name of the avatar the client uses to login                                                          ");
                Console.WriteLine("--password            The password of the avatar logging in                                                                         ");
                Console.WriteLine("--file                Either a full path to the .dae and texture files, or relative to the directory this program runs from         ");
                Console.WriteLine("--cc-first            Copy the uploaded asset to the avatar with this first name                                                    ");
                Console.WriteLine("--cc-last             Copy the uploaded asset to the avatar with this last name                                                     ");
                Console.WriteLine("--upload-textures     Upload the textures this mesh asset references.                                                               ");
                Console.WriteLine("--allow-oversize      Allow textures greater than 1024x1024px without resizing them to 1024x1024px.                                 ");
                Console.WriteLine("--texture-scale       Set the texture horizontal and vertical scaling. Normally 1.0, but for terrain tiles 0.995 is best.           ");
                Console.WriteLine("--physics-from-mesh   Also send the mesh data as physics. Equivalent to 'from file' in the Physics tab of viewer's mesh upload page.");
                Console.WriteLine("--login-region        This is the optional name of the Region on the grid that you want to login to, to perform the upload          ");
                Console.WriteLine();
                return 1;
            }

            m.InitClient();
            //initiate login
            LoginParams lp = m.Client.Network.DefaultLoginParams(m.FirstName, m.LastName, m.Password, "GridMeshUploadCmd", m.ClientVersion);
            lp.URI = m.LoginURL;
            if(m.LoginRegion != "") lp.Start = "uri:" + m.LoginRegion + "&128&128&25";
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

            if (m.UploadComplete && !m.UploadFailed)
            {
                m.FixUploadFolders();
                m.ccUUID = m.FindCCUser(m.ccFirstName, m.ccLastName);
                if (m.ccUUID != UUID.Zero) m.SendCopyToUser(m.ccUUID);
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
            parser.AllowOversizeTextures = AllowOversize;
            parser.UsePhysicsFromMesh = PhysicsFromMesh;
            var prims = parser.Load(FileName, UploadTextures);
            if (prims == null || prims.Count == 0)
            {
                Console.WriteLine("Error: Failed to parse collada file");
                return false;
            }

            Console.WriteLine("Parse collada file success, found {0} objects", prims.Count);

            ObjectName = Path.GetFileNameWithoutExtension(FileName);
            ObjectTmpName = "GMUC-" + UUID.Random().ToString();

            //use a folder name we can later find by searching, so we can move the textures into the same folder.
            //OpenSim ignores the textures folder UUID we give it in the mesh upload and places textures in its own folder under /Textures/MeshName
            //but we'll fix this after the upload is complete. That's why we give the folder a temp name we can find later.
            UUID ObjectFolder = Client.Inventory.FindFolderForType(AssetType.Object);
            MyObjectFolder = Client.Inventory.CreateFolder(ObjectFolder, ObjectTmpName);

            // This would be nicer: a subfolder containing the textures, but that stops us giving the folder to someone else
            // MyTexturesFolder = Client.Inventory.CreateFolder(MyObjectFolder, "Textures");
            MyTexturesFolder = MyObjectFolder; 

            if (Debug > 1)
            {
                Console.WriteLine("Created New Folder under 'Objects' called '{0}' with UUID {1}", ObjectTmpName, MyObjectFolder);
                //Console.WriteLine("Created New Folder under '{0}' called 'Textures' with UUID {1}", ObjectTmpName, MyTexturesFolder);
            }
            
            var uploader = new cmdModelUploader(Client, prims);
            var uploadDone = new AutoResetEvent(false);

            uploader.IncludePhysicsStub = true;
            uploader.UseModelAsPhysics = PhysicsFromMesh;
            uploader.InvName = ObjectTmpName;
            uploader.InvDescription = "Uploaded by GridUploadMeshCmd on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            uploader.InvAssetFolderUUID = MyObjectFolder;
            uploader.InvTextureFolderUUID = MyTexturesFolder;
            uploader.UploadTextures = UploadTextures;
            uploader.TextureScale = TextureScale;
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
                    UploadedMeshInvUUID = uploader.ReturnedMeshInvUUID;
                    ImageNames = uploader.ImageNames;
                    Console.WriteLine("Upload success. Uploaded Mesh has Asset UUID: {0} and Inventory UUID: {1}", UploadedMeshUUID, UploadedMeshInvUUID);
                    UploadComplete = true;
                }

                uploadDone.Set();
            }));

            if (!uploadDone.WaitOne(20 * 60 * 1000))
            {
                Console.WriteLine("Message upload timeout");
                UploadComplete = true;
                UploadFailed = true;
            }
            return true;
        }

        private void FixUploadFolders()
        {
            UUID MyUUID = Client.Self.AgentID;

            if(UploadTextures) 
            {
                List<InventoryBase> fl;
                UUID OurTexturesFolder = UUID.Zero;
                UUID TexturesFolder = Client.Inventory.FindFolderForType(AssetType.Texture);
                
                if(Debug > 0) Console.WriteLine("Scanning for uploaded texture folder '{0}' in system folder 'Textures' with UUID = {1}", ObjectTmpName, TexturesFolder);
                fl = Client.Inventory.FolderContents(TexturesFolder, MyUUID, true, false, InventorySortOrder.ByName, 20000);
                if (fl != null)
                {
                    foreach (InventoryBase f in fl)
                    {
                        if (f is InventoryFolder && f.Name == ObjectTmpName)
                        {
                            OurTexturesFolder = f.UUID;
                            break;
                        }
                    }
                    fl.Clear();
                }

                if (OurTexturesFolder != UUID.Zero)
                {
                    //now get a list of the texture names in the "Textures" folder
                    if (Debug > 0) Console.WriteLine("Scanning for uploaded textures in folder with UUID={0}...", OurTexturesFolder);
                    fl = Client.Inventory.FolderContents(OurTexturesFolder, MyUUID, false, true, InventorySortOrder.ByName, 20000);
                    if (fl != null)
                    {
                        foreach (InventoryBase f in fl)
                        {
                            if (f is InventoryTexture)
                            {
                                if (f.Name.Contains(ObjectTmpName + " - Texture"))
                                {
                                    if (Debug > 0) Console.WriteLine("Found texture '{0}' with UUID={1}, ParentUUID={2}", f.Name, f.UUID, f.ParentUUID);
                                    string[] tx = f.Name.Split(" ".ToCharArray());
                                    int i;
                                    if (Int32.TryParse(tx[tx.Length - 1], out i))
                                    {
                                        string ImgName = ImageNames[i - 1];
                                        string ext = System.IO.Path.GetExtension(ImgName);
                                        ImgName = ImgName.Substring(0, ImgName.Length - ext.Length);

                                        if (Debug > 0) Console.WriteLine("Renaming texture from '{0}' to '{1}' and moving from folder {2} to {3}", f.Name, ImgName, f.ParentUUID, MyTexturesFolder);
                                        var Texture = Client.Inventory.FetchItem(f.UUID, MyUUID, 20000);
                                        Texture.Name = ImgName;
                                        Texture.Description = "Uploaded with mesh '" + ObjectName + "' from file '" + ImageNames[i - 1] + "'";
                                        if (Debug > 0) Console.WriteLine("Inventory Item Name: '{0}' AssetUUID:{1} InvUUID:{2} ParentUUID:{3}", Texture.Name, Texture.AssetUUID, Texture.UUID, Texture.ParentUUID);
                                        Client.Inventory.RequestUpdateItem(Texture);
                                        Client.Inventory.MoveItem(f.UUID, MyTexturesFolder);
                                        if (Debug > 0) Console.WriteLine();
                                    }
                                }
                            }
                        }
                        fl.Clear();
                    }

                    if (Debug > 0) Console.WriteLine("Removing folder {0}", OurTexturesFolder);
                    Client.Inventory.RemoveFolder(OurTexturesFolder); //remove the created "Textures" folder that Opensim made
                }
            }
            if (Debug > 0) Console.WriteLine("Renaming mesh inventory object {0} from {1} to {2}", UploadedMeshInvUUID, ObjectTmpName, ObjectName);
            //rename the mesh from ObjectTmpName to ObjectName
            var MyObject = Client.Inventory.FetchItem(UploadedMeshInvUUID, MyUUID, 20000);
            MyObject.Name = ObjectName;
            if (Debug > 0) Console.WriteLine("Inventory Item '{0}' {1} {2} {3}", MyObject.Name, MyObject.AssetUUID, MyObject.UUID, MyObject.ParentUUID);
            Client.Inventory.RequestUpdateItem(MyObject);

            if (Debug > 0) Console.WriteLine("Renaming object folder from {0} to {1}", ObjectTmpName, ObjectName);
            Client.Inventory.MoveFolder(MyObjectFolder, Client.Inventory.FindFolderForType(AssetType.Object), ObjectName);
             
        }

        public UUID FindCCUser(string FirstName, string LastName)
        {
            UUID SendToID = UUID.Zero;
            string sendTo = String.Empty;
            
            if (FirstName != String.Empty && LastName != String.Empty) sendTo = FirstName + " " + LastName;

            if (sendTo.Length > 0)
            {
                AutoResetEvent lookupEvent = new AutoResetEvent(false);
                UUID thisQueryID = UUID.Zero;
                bool lookupSuccess = false;

                EventHandler<DirPeopleReplyEventArgs> callback =
                    delegate(object s, DirPeopleReplyEventArgs ep)
                    {
                        if (ep.QueryID == thisQueryID)
                        {
                            if (ep.MatchedPeople.Count > 0)
                            {
                                //lookups return people with any of the words above in so we must check the results again for exact match
                                for (int i = 0; i < ep.MatchedPeople.Count; i++)
                                {
                                    if(ep.MatchedPeople[i].FirstName == FirstName && ep.MatchedPeople[i].LastName == LastName)
                                    {
                                        SendToID = ep.MatchedPeople[i].AgentID;
                                        lookupSuccess = true;
                                        break;
                                    }
                                }
                            }

                            lookupEvent.Set();
                        }
                    };

                Client.Directory.DirPeopleReply += callback;
                thisQueryID = Client.Directory.StartPeopleSearch(sendTo, 0);

                bool eventSuccess = lookupEvent.WaitOne(20 * 1000, false);
                Client.Directory.DirPeopleReply -= callback;

                if (eventSuccess && lookupSuccess)
                {
                    Console.WriteLine("A copy of the uploaded mesh folder will be sent to avatar {0} with UUID {1}", sendTo, SendToID.ToString());
                }
                else
                {
                    Console.WriteLine("Could not find avatar {0}. No copy will be generated.", sendTo);
                }
            }

            return SendToID;
        }

        public void SendCopyToUser(UUID GiveTo)
        {
            Console.WriteLine("Giving object {0}, named {1} to user {2}", MyObjectFolder, ObjectName, GiveTo);
            Client.Inventory.GiveFolder(MyObjectFolder, ObjectName, AssetType.Folder, GiveTo, true);
        }
    }
}
