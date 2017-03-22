# libOMVClients
Experimental client tools built around lib OpenMetaverse.

Three client tools for use with OpenSimulator and Secondlife.


# GridImageUpload

A Windows .NET/Linux Mono GUI client for uploading Images to your inventory without using a full viewer.
Only one image can be uploaded at a time.

# GridImageUploadCmd

A Windows .NET/Linux Mono commandline client for uploading Images to your inventory without using a full viewer.
Only one image can be uploaded at a time, but as a commandline tool it can be use in a script or batch file to upload multiple images.

# GridMeshUploadCmd

A Windows .NET/Linux Mono commandline client for uploading mesh assets to your inventory without using a full viewer.
It supports sending the mesh complete with all necessary textures, and can be configured to set the physics to be either convex hull or prim (i.e. same as the mesh itself)


	Usage: GridMeshUploadCmd.exe --login=LoginURL --first=FirstName --last=LastName --password=Password --file=PathToMesh
				     [--cc-first=CopyToFirstName] [--cc-last=CopyToLastName] [--upload-textures] [--allow-oversize]
				     [--texture-scale=ScaleFactor] [--physics-from-mesh] [--login-region=RegionName]                                          

	This commandline client can connect to your grid and upload mesh assets. The mesh asset is described by a Collada (.dae) file and
	any associated texture image files in the format .bmp, .tga, .png or .jpg. All the files must reside in the same source directory.
	Being a commandline client (as opposed to a GUI client), it is easy to use in automation scripts to speed up the workflow of getting
	your mesh assets into OpenSimulator. The client can also optionally copy the uploaded assets to another avatar and perform other
	operations to speed up the workflow.

	--login               This is the login URI for your grid. e.g. OSGrid is http://login.osgrid.org:80
	--first               The first name of the avatar the client uses to login
	--last                The last name of the avatar the client uses to login
	--password            The password of the avatar logging in
	--file                Either a full path to the .dae and texture files, or relative to the directory this program runs from
	--cc-first            Copy the uploaded asset to the avatar with this first name
	--cc-last             Copy the uploaded asset to the avatar with this last name
	--upload-textures     Upload the textures this mesh asset references.
	--allow-oversize      Allow textures greater than 1024x1024px without resizing them to 1024x1024px.
	--texture-scale       Set the texture horizontal and vertical scaling. Normally 1.0, but for terrain tiles 0.995 is best.
	--physics-from-mesh   Also send the mesh data as physics. Equivalent to 'from file' in the Physics tab of viewer's mesh upload page.
	--login-region        This is the optional name of the Region on the grid that you want to login to, to perform the upload


Binary are prebuilt in the bin directory, or you can compile the project yourself:

# With Visual Studio:

Run one of the following batch files to create the VS project:
runprebuild2010.bat for VS2010
runprebuild2012.bat for VS2012
runprebuild2013.bat for VS2013
Then open the libOMVClient.sln solution file and compile in the IDE.

# Mono/Linux:

Run the following commands:

	./runprebuild.sh autoclean
	./runprebuild.sh
	xbuild

Binaries will be generated in the bin/ directory.

Any questions or comments, please email jak at ateb dot co dot uk

