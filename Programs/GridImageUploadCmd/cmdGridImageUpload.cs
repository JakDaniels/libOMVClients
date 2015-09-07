using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Drawing;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Http;
using OpenMetaverse.Imaging;


namespace cmdGridImageUpload
{
    public class cmdGridImageUpload
    {
        private GridClient Client;
        private byte[] UploadData = null;
        private string FileName = String.Empty;
        private UUID SendToID;
        private UUID AssetID;

        private string LoginURL = String.Empty;
        private string FirstName = String.Empty;
        private string LastName = String.Empty;
        private string Password = String.Empty;
        private string ccFirstName = String.Empty;
        private string ccLastName = String.Empty;
        private bool noResize = false;
        private bool ShowUsage = false;
        private bool Connected = false;
        private bool ConnectFailed = false;
        private bool EventQueueRunning = false;
        private bool UploadComplete = false;
        private bool UploadFailed = false;

        public static int Main(string[] args)
        {
            //args should be --login=LoginURL --first=FirstName --last=LastName ==password=Password --file=PathToImage [--cc-first=CopyToFirstName] [--cc-last=CopyToLastName] [--no-resize]
            //int i = 0;
            //while (i<args.Length) Console.WriteLine(args[i++]);
            //return 0;

            cmdGridImageUpload m = new cmdGridImageUpload();
            
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

            if (arg.ContainsKey("cc-first"))
            {
                m.ccFirstName = arg["cc-first"];
            }
            if (arg.ContainsKey("cc-last"))
            {
                m.ccLastName = arg["cc-last"];
            }
            if (arg.ContainsKey("no-resize"))
            {
                m.noResize = true;
            }
            if (m.ShowUsage)
            {
                Console.WriteLine("Usage: GridImageUploadCmd.exe --login=LoginURL --first=FirstName --last=LastName --password=Password --file=PathToImage [--cc-first=CopyToFirstName] [--cc-last=CopyToLastName] [--no-resize]");
                return 1;
            }

            m.InitClient();
            //initiate login
            LoginParams lp = m.Client.Network.DefaultLoginParams(m.FirstName, m.LastName, m.Password, "GridImageUploadCmd", "0.1");
            lp.URI = m.LoginURL;
            m.Client.Network.BeginLogin(lp);

            while (!m.Connected)
            {
                if (m.ConnectFailed) return 1;
                System.Threading.Thread.Sleep(500);
            }


            while (!m.EventQueueRunning) System.Threading.Thread.Sleep(500);
            
            if (m.LoadImage())
            {
                if (m.UploadImage())
                {
                    while (!m.UploadComplete) System.Threading.Thread.Sleep(500);

                }
                else
                {
                    Console.WriteLine("Error Uploading Image");
                }
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
                if (args[i].Substring(0, 2) == "--") a = args[i].Substring(2,args[i].Length-2);
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

        private bool LoadImage()
        {
            if (String.IsNullOrEmpty(FileName))
                return false;

            string extension = System.IO.Path.GetExtension(FileName).ToLower();
            Bitmap bitmap = null;

            try
            {
                if (extension == ".jp2" || extension == ".j2c")
                {
                    Image image;
                    ManagedImage managedImage;

                    // Upload JPEG2000 images untouched
                    UploadData = System.IO.File.ReadAllBytes(FileName);

                    OpenJPEG.DecodeToImage(UploadData, out managedImage, out image);
                    bitmap = (Bitmap)image;

                    Console.WriteLine("Loaded raw JPEG2000 data {0}", FileName);
                }
                else
                {
                    if (extension == ".tga")
                        bitmap = LoadTGAClass.LoadTGA(FileName);
                    else
                        bitmap = (Bitmap)System.Drawing.Image.FromFile(FileName);

                    Console.WriteLine("Loaded image {0}", FileName);

                    int oldwidth = bitmap.Width;
                    int oldheight = bitmap.Height;
                    int newwidth;
                    int newheight;

                    if (!IsPowerOfTwo((uint)oldwidth) || !IsPowerOfTwo((uint)oldheight))
                    {
                        newwidth = Pow2Roundup(oldwidth);
                        newheight = Pow2Roundup(oldheight);
                        Console.WriteLine("Image has irregular dimensions {0}x{1}, resizing to {2}x{3}", oldwidth, oldheight, newwidth, newheight);

                        Bitmap resized = new Bitmap(newwidth, newheight, bitmap.PixelFormat);
                        Graphics graphics = Graphics.FromImage(resized);

                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        graphics.InterpolationMode =
                            System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(bitmap, 0, 0, newwidth, newheight);

                        bitmap.Dispose();
                        bitmap = resized;

                        oldwidth = newwidth;
                        oldheight = newheight;
                    }

                    if (!noResize)
                    {
                        // Handle resizing to prevent excessively large images
                        if (oldwidth > 1024 || oldheight > 1024)
                        {
                            newwidth = (oldwidth > 1024) ? 1024 : oldwidth;
                            newheight = (oldheight > 1024) ? 1024 : oldheight;

                            Console.WriteLine("Image has oversized dimensions {0}x{1}, resizing to {2}x{3}", oldwidth, oldheight, newwidth, newheight);

                            Bitmap resized = new Bitmap(newwidth, newheight, bitmap.PixelFormat);
                            Graphics graphics = Graphics.FromImage(resized);

                            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            graphics.InterpolationMode =
                               System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            graphics.DrawImage(bitmap, 0, 0, newwidth, newheight);

                            bitmap.Dispose();
                            bitmap = resized;
                        }
                    }
                    else Console.WriteLine("Oversize resizing is disabled...");

                    Console.WriteLine("Encoding image...");

                    UploadData = OpenJPEG.EncodeFromImage(bitmap, true);

                    Console.WriteLine("Finished encoding");

                    //System.IO.File.WriteAllBytes("out.jp2", UploadData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Image Upload Error {0}", ex.ToString());
                return false;
            }

            Console.WriteLine("Image Size: {0} KB", Math.Round((double)UploadData.Length / 1024.0d, 2));
            return true;
        }

        private bool SaveImage()
        {
            if (String.IsNullOrEmpty(FileName))
                return false;

            if (UploadData != null)
            {
                try
                {
                    System.IO.File.WriteAllBytes(FileName, UploadData);
                    Console.WriteLine("Saved {0} bytes to {1}", UploadData.Length, FileName);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to save {0}, the error was {1}", FileName, ex.Message);
                }
            }
            return false;
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


        private bool UploadImage()
        {
            SendToID = UUID.Zero;
            string sendTo = String.Empty;
            if(ccFirstName!=String.Empty) sendTo = ccFirstName + " " + ccLastName;

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
                                SendToID = ep.MatchedPeople[0].AgentID;
                                lookupSuccess = true;
                            }

                            lookupEvent.Set();
                        }
                    };

                Client.Directory.DirPeopleReply += callback;
                thisQueryID = Client.Directory.StartPeopleSearch(sendTo, 0);

                bool eventSuccess = lookupEvent.WaitOne(10 * 1000, false);
                Client.Directory.DirPeopleReply -= callback;

                if (eventSuccess && lookupSuccess)
                {
                    Console.WriteLine("Will send uploaded image to avatar {0} with UUID {1}", sendTo, SendToID.ToString());
                }
                else
                {
                    Console.WriteLine("Could not find avatar {0}. Upload Cancelled.", sendTo);
                    return false;
                }
            }

            if (UploadData != null)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(FileName);

                Permissions perms = new Permissions();
                perms.EveryoneMask = PermissionMask.All;
                perms.NextOwnerMask = PermissionMask.All;
                Console.WriteLine("Starting uploading of image to Asset Server with name '{0}'", name);
                Client.Inventory.RequestCreateItemFromAsset(UploadData, name, "Uploaded with GridImageUploadCmd", AssetType.Texture,
                    InventoryType.Texture, Client.Inventory.FindFolderForType(AssetType.Texture), perms, ItemCreatedFromAssetCallback);

                return true;
            }
            return false;
        }

        private void ItemCreatedFromAssetCallback(bool success, string status, UUID itemID, UUID assetID)
        {
            //Console.WriteLine("Called ItemCreatedFromAssetCallback. success: {0}, status: {1}, itemID: {2}, assetId: {3}", success.ToString(), status, itemID.ToString(), assetID.ToString());
            if (success)
            {
                AssetID = assetID;
                Console.WriteLine("Asset created with asset UUID: {0}", AssetID.ToString());

            }
            else
            {
                UploadFailed = true;
                Console.WriteLine("Failed to create Asset! The error was: {0}", status);
            }
            UploadComplete = true;
        }

        private void Network_OnEventQueueRunning(object sender, EventQueueRunningEventArgs e)
        {
            //Console.WriteLine("Event queue is running for {0}, enabling uploads");
            EventQueueRunning = true;
        }


        private bool IsPowerOfTwo(uint n)
        {
            return (n & (n - 1)) == 0 && n != 0;
        }

        private int Pow2Roundup (int n)
        {
            if (n < 0)
            return 0;
            --n;
            n |= n >> 1;
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;
            n |= n >> 32;
            n |= n >> 64;
            n |= n >> 128;
            n |= n >> 256;
            n |= n >> 512;
            n |= n >> 1024;
            return n + 1;
        }
     }
}
