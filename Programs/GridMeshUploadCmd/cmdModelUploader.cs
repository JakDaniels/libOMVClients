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


namespace OpenMetaverse.ImportExport
{
    
    /// <summary>
    /// Implements mesh upload communications with the simulator
    /// </summary>
    public class cmdModelUploader
    {
        public GridClient Client;
        public List<ModelPrim> Prims;
        public List<byte[]> Images;
        public Dictionary<string, int> ImgIndex;
        public string InvName = "NewMesh";
        public string InvDescription = "";
        public UUID InvAssetFolderUUID, InvTextureFolderUUID;
        public bool UploadTextures;
        public int Debug;
        public UUID ReturnedMeshUUID;

        /// <summary>
        /// Inlcude stub convex hull physics, required for uploading to Second Life
        /// </summary>
        public bool IncludePhysicsStub;

        /// <summary>
        /// Use the same mesh used for geometry as the physical mesh upload
        /// </summary>
        public bool UseModelAsPhysics;

        /// <summary>
        /// Callback for mesh upload operations
        /// </summary>
        /// <param name="result">null on failure, result from server on success</param>
        public delegate void ModelUploadCallback(OSD result);

        /// <summary>
        /// Creates instance of the mesh uploader
        /// </summary>
        /// <param name="client">GridClient instance to communicate with the simulator</param>
        /// <param name="prims">List of ModelPrimitive objects to upload as a linkset</param>
        public cmdModelUploader(GridClient client, List<ModelPrim> prims)
        {
            this.Client = client;
            this.Prims = prims;
        }
        /// <summary>
        /// Performs model upload in one go, without first checking for the price
        /// </summary>
        /// <param name="callback">Callback that will be invoke upon completion of the upload. Null is sent on request failure</param>
        public void Upload(ModelUploadCallback callback)
        {
            PrepareUpload((result =>
            {
                if (result == null && callback != null)
                {
                    callback(null);
                    return;
                }

                if (result is OSDMap)
                {
                    var res = (OSDMap)result;
                    Uri uploader = new Uri(res["uploader"]);
                    PerformUpload(uploader, (contents =>
                    {
                        if (contents != null)
                        {
                            var reply = (OSDMap)contents;
                            if (reply.ContainsKey("new_inventory_item") && reply.ContainsKey("new_asset"))
                            {
                                // Request full update on the item in order to update the local store
                                Client.Inventory.RequestFetchInventory(reply["new_inventory_item"].AsUUID(), Client.Self.AgentID);
                                ReturnedMeshUUID = reply["new_asset"].AsUUID();
                            }
                        }
                        if (callback != null) callback(contents);
                    }));
                }
            }));

        }

        /// <summary>
        /// Ask server for details of cost and impact of the mesh upload
        /// </summary>
        /// <param name="callback">Callback that will be invoke upon completion of the upload. Null is sent on request failure</param>
        public void PrepareUpload(ModelUploadCallback callback)
        {
            Uri url = null;
            if (Client.Network.CurrentSim == null ||
                Client.Network.CurrentSim.Caps == null ||
                null == (url = Client.Network.CurrentSim.Caps.CapabilityURI("NewFileAgentInventory")))
            {
                Console.WriteLine("Cannot upload mesh, no connection or NewFileAgentInventory not available");
                if (callback != null) callback(null);
                return;
            }

            Images = new List<byte[]>();
            ImgIndex = new Dictionary<string, int>();

            OSDMap req = new OSDMap();
            req["name"] = InvName;
            req["description"] = InvDescription;

            req["asset_resources"] = AssetResources(UploadTextures);
            req["asset_type"] = "mesh";
            req["inventory_type"] = "object";

            req["folder_id"] = InvAssetFolderUUID;
            req["texture_folder_id"] = InvTextureFolderUUID;

            req["everyone_mask"] = (int)PermissionMask.All;
            req["group_mask"] = (int)PermissionMask.All; ;
            req["next_owner_mask"] = (int)PermissionMask.All;

            CapsClient request = new CapsClient(url);
            request.OnComplete += (client, result, error) =>
            {
                if (error != null || result == null || result.Type != OSDType.Map)
                {
                    Console.WriteLine("Mesh upload request failure: {0}", error.Message);
                    if (callback != null) callback(null);
                    return;
                }
                OSDMap res = (OSDMap)result;

                if (res["state"] != "upload")
                {
                    Console.WriteLine("Mesh upload failure: {0}", res["message"]);
                    if (callback != null) callback(null);
                    return;
                }

                if (Debug > 2) Console.WriteLine("Response from mesh upload prepare:\n{0}", OSDParser.SerializeLLSDNotationFormatted(result));
                if (callback != null) callback(result);
            };

            if (Debug > 2) Console.WriteLine("Sending Request Resources: {0}", OSDParser.SerializeLLSDNotationFormatted(req));
            request.BeginGetResponse(req, OSDFormat.Xml, 3 * 60 * 1000);

        }

        OSD AssetResources(bool upload)
        {
            OSDArray instanceList = new OSDArray();
            List<byte[]> meshes = new List<byte[]>();
            //List<byte[]> textures = new List<byte[]>();

            foreach (var prim in Prims)
            {
                OSDMap primMap = new OSDMap();

                OSDArray faceList = new OSDArray();

                foreach (var face in prim.Faces)
                {
                    OSDMap faceMap = new OSDMap();

                    faceMap["diffuse_color"] = face.Material.DiffuseColor;
                    faceMap["fullbright"] = false;

                    if (face.Material.TextureData != null)
                    {
                        int index;
                        if (ImgIndex.ContainsKey(face.Material.Texture))
                        {
                            index = ImgIndex[face.Material.Texture];
                        }
                        else
                        {
                            index = Images.Count;
                            ImgIndex[face.Material.Texture] = index;
                            Images.Add(face.Material.TextureData);
                        }
                        faceMap["image"] = index;
                        faceMap["scales"] = 1.0f;
                        faceMap["scalet"] = 1.0f;
                        faceMap["offsets"] = 0.0f;
                        faceMap["offsett"] = 0.0f;
                        faceMap["imagerot"] = 0.0f;
                    }

                    faceList.Add(faceMap);
                }

                primMap["face_list"] = faceList;

                primMap["position"] = prim.Position;
                primMap["rotation"] = prim.Rotation;
                primMap["scale"] = prim.Scale;

                primMap["material"] = (int)Material.Wood; // always sent as "wood" material
                primMap["physics_shape_type"] = (int)PhysicsShapeType.ConvexHull; // always sent as "convex hull";
                primMap["mesh"] = meshes.Count;
                meshes.Add(prim.Asset);

                instanceList.Add(primMap);
            }

            OSDMap resources = new OSDMap();
            resources["instance_list"] = instanceList;

            OSDArray meshList = new OSDArray();
            foreach (var mesh in meshes)
            {
                meshList.Add(OSD.FromBinary(mesh));
            }
            resources["mesh_list"] = meshList;

            OSDArray textureList = new OSDArray();
            for (int i = 0; i < Images.Count; i++)
            {
                if (upload)
                {
                    textureList.Add(new OSDBinary(Images[i]));
                }
                else
                {
                    textureList.Add(new OSDBinary(Utils.EmptyBytes));
                }
            }

            resources["texture_list"] = textureList;

            resources["metric"] = "MUT_Unspecified";
            return resources;
        }

        /// <summary>
        /// Performas actual mesh and image upload
        /// </summary>
        /// <param name="uploader">Uri recieved in the upload prepare stage</param>
        /// <param name="callback">Callback that will be invoke upon completion of the upload. Null is sent on request failure</param>
        public void PerformUpload(Uri uploader, ModelUploadCallback callback)
        {
            CapsClient request = new CapsClient(uploader);
            request.OnComplete += (client, result, error) =>
            {
                if (error != null || result == null || result.Type != OSDType.Map)
                {
                    Console.WriteLine("Mesh upload request failure {0}", error.Message);
                    if (callback != null) callback(null);
                    return;
                }
                OSDMap res = (OSDMap)result;
                if (Debug > 2) Console.WriteLine("Response from mesh upload perform:\n{0}", OSDParser.SerializeLLSDNotationFormatted(result));
                if (callback != null) callback(res);
            };

            OSD resources = AssetResources(UploadTextures);
            if (Debug > 2) Console.WriteLine("Sending Request Resources: {0}", OSDParser.SerializeLLSDNotationFormatted(resources));
            request.BeginGetResponse(resources, OSDFormat.Xml, 60 * 1000);
        }
    }
}