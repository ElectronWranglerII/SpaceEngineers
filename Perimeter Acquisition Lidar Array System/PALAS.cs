/*
* This work is made available under the Creative Commons
* Attribution-NonCommercial-ShareAlike 4.0 International License.
* To view a copy of this license, visit
* http://creativecommons.org/licenses/by-nc-sa/4.0/.
*/

/*
PERIMETER ACQUISITION LIDAR ARRAY SYSTEM (PALAS)
*/

	//Structure holding operational parameters for each camera
	struct ArrayElement{
		public bool IsUnassigned;
		public double HorizontalMinimum;
		public double HorizontalSize;
		public double VerticalMinimum;
		public double VerticalSize;
		public double ScanRange;
		public IMyCameraBlock Emitter;
	}

private bool firstrun = true;
private bool InList = false;
private MyDetectedEntityInfo info;

	//The block group containing LIDAR cameras
	string CameraArrayGroupName = "LiDARCameras";
	
	//The maximum scan rate the LIDAR is allowed
	//to operate at, from 0.0 to 1.0 (0% to 100%)
	double ScanRate = 1.0;

	//The array's minimum and maximum cone angles
	//Left/down are negative, right/up are positive
	double HorizontalMinimum = -45.0;
	double HorizontalMaximum = -25.0;
	double VerticalMinimum = -45.0;
	double VerticalMaximum = 35.0;

	//Where the array is electronically pointed
	//Computed using the cone angle values
	double HorizontalCenter;
	double VerticalCenter;

	//The array's total scan angles
	//Computed using the cone angle values
	double HorizontalAngle;
	double VerticalAngle;

	//The array's resolution, expressed as the number of elements
	//in the horizontal and vertical dimensions
	int HorizontalResolution = 8;
	int VerticalResolution = 8;

	//The array's maximum scan distance in meters
	//and a plane describing the array's "wavefront" orientation
	//Expressed using an equilateral triangle with one side lying on
	//the same horizontal plane as the array's base
	//From the view of the array, Point A is on the lower left,
	//Point B is on the lower right, and Point C is on the top
	//meaning the points are in counterclockwise order
	double RangePointA = 2000;
	double RangePointB = 2000;
	double RangePointC = 2000;


struct Contact{
	public long ID;
	public string Name;
	public MyDetectedEntityType Type;
	public double Distance;
}
private Contact CurrentContact;
private List<Contact> Target = new List<Contact>();
private Random Random;
private int CurrentCamera = 0;

List<IMyTerminalBlock> Cockpits = new List<IMyTerminalBlock>();
IMyCockpit Cockpit;

List<ArrayElement> ElementArray = new List<ArrayElement>();

public void Main(string argument){
	if (firstrun){
		firstrun = false;
		List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
		GridTerminalSystem.GetBlocks(blocks);
		GridTerminalSystem.GetBlocksOfType<IMyCockpit>(Cockpits);
		Cockpit = (IMyCockpit)Cockpits[0];

		foreach(var block in blocks){
			if (block is IMyCameraBlock){
				Camera.Add((IMyCameraBlock)block);
				Camera[Camera.Count - 1].EnableRaycast = true;
			}
		}
		Random = new Random();
	}
	
	HorizontalAngle = Math.Abs(HorizontalMaximum - HorizontalMinimum);
	VerticalAngle = Math.Abs(VerticalMaximum - VerticalMinimum);
	HorizontalCenter = HorizontalMinimum + HorizontalAngle / 2.0;
	VerticalCenter = VerticalMinimum + VerticalAngle / 2.0;
	Echo("Scan Angle: " + HorizontalAngle.ToString("#.##") + ", " + VerticalAngle.ToString("#.##"));
	Echo("Scan Center: " + HorizontalCenter.ToString() + ", " + VerticalCenter.ToString());
	
	if(CurrentCamera < Camera.Count){
		InList = false;
		if(Camera[CurrentCamera].CanScan(SCAN_DISTANCE)){
			info = Camera[CurrentCamera].Raycast(SCAN_DISTANCE, (float)(Random.NextDouble() * 2.0 - 1.0) * 45, (float)(Random.NextDouble() * 2.0 - 1.0) * 45);
		}
		foreach(var StoredTarget in Target){
			if(info.EntityId == StoredTarget.ID){
				InList = true;
				break;
			}
		}
		if(InList == false){
			CurrentContact.ID = info.EntityId;
			CurrentContact.Type = info.Type;
			CurrentContact.Name = info.Name;
			if(info.HitPosition.HasValue){
				CurrentContact.Distance = Vector3D.Distance(Camera[CurrentCamera].GetPosition(), info.HitPosition.Value);
			}else{
				CurrentContact.Distance = -1.0f;
			}
			Target.Add(CurrentContact);
		}
		CurrentCamera = CurrentCamera + 1;
	}else{
		
		Echo("Target Count: " + Target.Count);
		foreach(var Output in Target){
			Echo(Output.Name + "(" + Output.ID.ToString() + ")");
			//Cockpit.GetSurface(0).WriteText("      Target: " + Output.Name);
		}
		CurrentCamera = 0;
		Target.Clear();
	}
	
	Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

public void ConfigureArray(){
	List<IMyCameraBlock> Camera = new List<IMyCameraBlock>();
	List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
	double HMin = HorizontalMinimum;
	double VMin = VerticalMinimum;
	ArrayElement Cam = new ArrayElement();


	HorizontalAngle = Math.Abs(HorizontalMaximum - HorizontalMinimum);
	VerticalAngle = Math.Abs(VerticalMaximum - VerticalMinimum);
	HorizontalCenter = HorizontalMinimum + HorizontalAngle / 2.0;
	VerticalCenter = VerticalMinimum + VerticalAngle / 2.0;

	double HorizontalStep = HorizontalAngle / HorizontalResolution;
	double VerticalStep = VerticalAngle / VerticalResolution;

	double HorizontalRangeStep = (RangePointA - RangePointB) / HorizontalResolution;
	double VerticalRangeStep = (RangePointA - RangePointC) / VerticalResolution;
	
	//Get list of LIDAR array cameras
	List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();   
	try{
		blocks = GetBlockGroupsWithName(CameraArrayGroupName);
	}catch(System.Exception){
		Echo("LiDAR array group not found");
		return;
	}    
	foreach(var block in blocks){
		if (block is IMyCameraBlock){
			Camera.Add((IMyCameraBlock)block);
			Camera[Camera.Count - 1].EnableRaycast = true;
		}
	}

	//Configure each camera's operational parameters
	for(int Y = 0; Y < VerticalResolution; Y++){
		for(int X = 0; X < HorizontalResolution; X++){
			Cam.HorizontalMinimum = HMin + X * HorizontalStep;
			Cam.HorizontalSize = HorizontalStep;
			Cam.VerticalMinimum = VMin + Y * VerticalStep;
			Cam.VerticalSize = VerticalStep;
			Cam.ScanRange = RangePointA - X * HorizontalRangeStep - Y * VerticalRangeStep;
			ElementArray.Add(Cam);
		}
	}

	for(int i = 0; i < ElementArray.Count; i++){
		Console.WriteLine("Camera" + i.ToString());
		Console.WriteLine("HMin/HMax: " + ElementArray[i].HorizontalMinimum.ToString() + " / " + HorizontalMaximum.ToString());
		Console.WriteLine("VMin/VMax: " + ElementArray[i].VerticalMinimum.ToString() + " / " + VerticalMaximum.ToString());
		Console.WriteLine("Range: " + ElementArray[i].ScanRange.ToString());
		Console.WriteLine();
	}
}
