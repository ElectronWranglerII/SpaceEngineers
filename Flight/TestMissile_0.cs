//*****************************************************************************
// User Configurable Variables
//*****************************************************************************
//General settings
bool UsePBLCD = false;							//Set to true if script should use programmable block's LCD for data display
bool MonH2 = true;								//Set to true to monitor hydrogen tanks
bool MonO2 = false;								//Set to true to monitor oxygen tanks
//General display controls
Color InfoColor = Color.Blue;
Color GoodColor = Color.Green;
Color CautionColor = Color.Yellow;
Color WarningColor = Color.Red;
//Block identification data
string UILCD = "EWH2O2MON";						//Any LCD whose name contains this string will be used to display data
string H2LCD = "H2MON";							//Any UILCD whose Custom Data contains this string will display hydrogen tank data
string O2MON = "O2MON";							//Any UILCD whose Custom Data contains this string will display oxygen tank data
												//If H2LCD == O2LCD the LCD will display data for both tank types
string H2TankTag = "EWH2O2MON";					//Any hydrogen tank whose custom data contains this string will be monitored
string O2TankTag = "EWH2O2MON";					//Any oxygen tank whose custom data contains this string will be monitored
//Limits for how to display tank names
bool HTankNameShorten = true;					//Set to true to enable shortening of displayed hydrogen tank names
int HTankNameLimit = 2;							//Length to limit hydrogen tank names to
int HTankNamePre = 0;							//The number of characters to include from the start of the name
int HTankNamePost = 2;							//The number of characters to include from the end of the name
bool OTankNameShorten = true;					//Set to true to enable shortening of displayed oxygen tank names
int OTankNameLimit = 2;							//Length to limit oxygen tank names to
int OTankNamePre = 0;							//The number of characters to include from the start of the name
int OTankNamePost = 2;							//The number of characters to include from the end of the name
//Tank display controls
LevelFormat HLevelFormat = LevelFormat.Percent | LevelFormat.Ratio;	//Defines how hydrogen tank levels will be formatted
TankSortControls HSortControls = new TankSortControls(
	true,
	false,
	true,
	false,
	true,
	false
);
TankSortOrder[] HSortPriority = {
	TankSortOrder.Level,
	TankSortOrder.Capacity,
	TankSortOrder.Name
};												//Defines hydrogen tank sorting priority
LevelFormat OLevelFormat = LevelFormat.Percent;	//Defines how oxygen tank levels will be formatted
TankSortControls OSortControls = new TankSortControls(
	true,
	false,
	true,
	false,
	true,
	false
);
TankSortOrder[] OSortPriority = {
	TankSortOrder.Level,
	TankSortOrder.Capacity,
	TankSortOrder.Name
};												//Defines oxygen tank sorting priority

//*****************************************************************************
// System Variables
//*****************************************************************************
//Screen/Menu control
enum ScreenType{
	Config,
	HMon,
	HMonSelect,
	HTankInfo,
	OMon,
	OMonSelect,
	OTankInfo
}
ScreenType[] ScreenOrder = new ScreenType[]{
	ScreenType.HMonSelect,
	ScreenType.HMon,
	ScreenType.OMon,
	ScreenType.OMonSelect,
	ScreenType.Config
};
int ItemIndex = 0;					//The index of the selected item
int CurrentScreen = (int)ScreenType.HMon;
ScreenType Screen;
bool DisplayHTankInfo = false;
bool DisplayOTankInfo = false;

enum LevelFormat{
	Percent = 1,
	Ratio = 2,
	Raw = 4
}

enum SystemMode{
	Boot = 1
}

struct TankSortControls{
	public bool ByName;				//Set to true to display tanks alphabetically by tank names
	public bool NameDescending;		//Set to true to sort tank names in ascending order
	public bool ByLevel;			//Set to true to display tanks by level
	public bool LevelDescending;	//Set to true to sort tanks by level
	public bool ByCapacity;			//Set to true to display tanks by capacity
	public bool CapacityDescending;	//Set to true to sort tank capacities in ascending order
	
	public TankSortControls(bool Name, bool NameOrder, bool Level, bool LevelOrder, bool Capacity, bool CapacityOrder){
		this.ByName = Name;
		this.NameDescending = NameOrder;
		this.ByCapacity = Capacity;
		this.CapacityDescending = CapacityOrder;
		this.ByLevel = Level;
		this.LevelDescending = LevelOrder;
	}
}

enum TankSortOrder{
	Name,
	Capacity,
	Level
}

static string ProgramSignature = "_EWH2O2MON";
static string ProgramName = "ElectronWrangler's Fuel Monitor";
static string ProgramVersion = "1.0";
static string ProgramDescription = "Monitors & displays H2 & O2 quantities";

IMyTextSurface PBDisplay;
List<IMyTextSurface> UIDisplay;
string PBOutput;

struct TankEntry{
	public string Name;
	public float Capacity;
	public double Level;
	public double Temp;
	public double Delta;
	public bool Monitor;
	public string Data;
	
	public TankEntry(string InName, float InCapacity, double InLevel, bool InMonitor, string InData){
		this.Name = InName;
		this.Capacity = InCapacity;
		this.Level = InLevel;
		this.Temp = 0.0;
		this.Delta = 0.0;
		this.Monitor = InMonitor;
		this.Data = InData;
	}
}

TankEntry[] FuelTankList;
double FuelCapacity;
double FuelDelta;
double FuelLevel;
double FuelTemp;

TankEntry[] OxygenTankList;
double OxygenCapacity;
double OxygenDelta;
double OxygenLevel;
double OxygenTemp;

int AveragePointCount;
List<double> AverageWeights;


enum Phase{
	Idle,
	Boost,
	Spinup,
	Release
}
List<IMyGyro> GyroList;
List<IMyShipMergeBlock> ClusterList;
List<IMyThrust> ThrusterList;
List<IMyWarhead> WarheadList;
IMyShipMergeBlock MissileMerge;
IMyRemoteControl RemoteControl;
Phase FlightPhase;
Vector3D RotationRate;
bool Abort = false;

DateTime TimeNow;
double TimeDelta;
double DepleteTime;

//Script performance metrics
int ErrorCount = 0;
int WarningCount = 0;

int CommandsReceived = 0;
int CommandsProcessed = 0;
int CommandsWaiting = 0;
int CommandsFailed = 0;
int CommandsUnrecognized = 0;

int ActivityIndex = 0;
static string[] ActivityIndicator = {
	"|",
	"/",
	"-",
	"\\"
};

string LCDOutput;

int TempInt;

Program(){
	//Append script name to programmable block's name
	if(!Me.CustomName.Contains(ProgramSignature)){
		Me.CustomName = Me.CustomName + ProgramSignature;
	}
	
	PBDisplay = (IMyTextSurface)(Me.GetSurface(0));
	PBDisplay.Font = "Monospace";
	PBDisplay.FontColor = Color.Green;
	PBDisplay.FontSize = 0.75f;
	PBDisplay.WriteText("Booting");
	
	TimeNow = DateTime.Now;
	
	Screen = ScreenOrder[CurrentScreen];
	
	if(Storage.Length > 0){
		string[] Entries = Storage.Trim().Split(';');
		foreach(string Entry in Entries){
			string[] Temp = Entry.Trim().Split('=');
			if(Temp.Length == 2){
				switch(Temp[0].ToUpper().Trim()){
					case "COMMANDSFAILED":
						if(int.TryParse(Temp[1].Trim(), out TempInt)){
							CommandsFailed = TempInt;
						}else{
							WarningCount++;
						}
						break;
					case "COMMANDSPROCESSED":
						if(int.TryParse(Temp[1].Trim(), out TempInt)){
							CommandsProcessed = TempInt;
						}else{
							WarningCount++;
						}
						break;
					case "COMMANDSRECEIVED":
						if(int.TryParse(Temp[1].Trim(), out TempInt)){
							CommandsReceived = TempInt;
						}else{
							WarningCount++;
						}
						break;
					case "COMMANDSUNRECOGNIZED":
						if(int.TryParse(Temp[1].Trim(), out TempInt)){
							CommandsUnrecognized = TempInt;
						}else{
							WarningCount++;
						}
						break;
					case "COMMANDSWAITING":
						if(int.TryParse(Temp[1].Trim(), out TempInt)){
							CommandsWaiting = TempInt;
						}else{
							WarningCount++;
						}
						break;
					case "ERRORCOUNT":
						if(int.TryParse(Temp[1].Trim(), out TempInt)){
							ErrorCount = TempInt;
						}else{
							WarningCount++;
						}
						break;
					case "WARNINGCOUNT":
						if(int.TryParse(Temp[1].Trim(), out TempInt)){
							WarningCount += TempInt;
						}else{
							WarningCount++;
						}
						break;
					default:
						break;
				}
			}
		}
	}
	
	FuelTemp = 0;
	FuelDelta = 0;
	OxygenTemp = 0;
	OxygenDelta = 0;
	
	AverageWeights = new List<double>(new double[AveragePointCount]);
	
	GyroList = new List<IMyGyro>();
	ClusterList = new List<IMyShipMergeBlock>();
	ThrusterList = new List<IMyThrust>();
	WarheadList = new List<IMyWarhead>();
	FlightPhase = Phase.Idle;
	RotationRate = new Vector3D();

	Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

void Main(string Argument){
	TimeDelta = Runtime.TimeSinceLastRun.TotalSeconds;
	
	LCDOutput = "";
	
	RemoteControl = GridTerminalSystem.GetBlockWithName("Missile.Remote") as IMyRemoteControl;
	
	MissileMerge = GetMerge();
	if(MissileMerge == null){
		Abort = true;
	}
	
	if(!ScanGyros(ref GyroList)){
		Abort = true;
	}
	
	if(!ScanMergeBlocks(ref ClusterList)){
		Abort = true;
	}
	
	if(!ScanThrusters(ref ThrusterList)){
		Abort = true;
	}
	
	if(!ScanWarheads(ref WarheadList)){
		Abort = true;
	}
	
	if(ScanTanks(ref FuelTankList, "Hydrogen", ref FuelLevel, ref FuelCapacity) == true){
		for(int i = 0; i < FuelTankList.Length; i++){
			if(FuelTankList[i].Data.Contains(H2TankTag)){
				FuelTankList[i].Monitor = true;
			}else{
				FuelTankList[i].Monitor = false;
			}
		}
		FuelDelta = (FuelLevel - FuelTemp) * FuelCapacity;
		FuelTemp = FuelLevel;
		if(FuelLevel < 0.1){
			switch(FlightPhase){
				case Phase.Idle:
					break;
				case Phase.Boost:
					foreach(var Gyro in GyroList){
						Gyro.GyroOverride = true;
						Gyro.GyroPower = 1;
						Gyro.ApplyAction("OnOff_On");
					}
					FlightPhase = Phase.Spinup;
					break;
				case Phase.Spinup:
					RotationRate = Vector3D.Transform(RemoteControl.GetShipVelocities().AngularVelocity, Matrix.Transpose(RemoteControl.WorldMatrix.GetOrientation()));
					Echo(RotationRate.Z.ToString());
					if(RotationRate.Z > 6){
						FlightPhase = Phase.Release;
					}
					break;
				case Phase.Release:
					foreach(var Warhead in WarheadList){
						//Warhead.ApplyAction("OnOff_On");
						Warhead.IsArmed = true;
					}
					foreach(var Cluster in ClusterList){
						Cluster.ApplyAction("OnOff_Off");
					}
					Runtime.UpdateFrequency = UpdateFrequency.None;
					break;
			}
		}
	}
	
	//Command processor
	if(!String.IsNullOrEmpty(Argument)){
		string[] CommandLine = Argument.Trim().Split(' ');
		switch(CommandLine[0].Trim().ToUpper()){
			case "LAUNCH":
				if(!Abort){
					foreach(var Gyro in GyroList){
						Gyro.GyroOverride = false;
						Gyro.GyroPower = 1;
						Gyro.ApplyAction("OnOff_On");
					}
					foreach(var Thruster in ThrusterList){
						Thruster.ThrustOverridePercentage = 1;
						Thruster.ApplyAction("OnOff_On");
					}
					MissileMerge.ApplyAction("OnOff_Off");
					FlightPhase = Phase.Boost;
				}else{
					Echo("Aborting");
				}
				break;
		}
	}

	//Print program info to programmable block's display
	if(ErrorCount > 0){
		PBDisplay.FontColor = Color.Red;
	}else if(WarningCount > 0){
		PBDisplay.FontColor = Color.Yellow;
	}else{
		PBDisplay.FontColor = Color.Green;
	}
	PBOutput =
		"Running: " +
		ProgramName + "\n" +
		"Version: " + ProgramVersion + "\n" +
		ProgramDescription + "\n" +
		"Commands received: " + CommandsReceived.ToString() + "\n";
	if(UsePBLCD){
		PBOutput += LCDOutput;
	}
	PBOutput += ActivityIndicator[ActivityIndex];
	PBDisplay.WriteText(PBOutput);
	
	//Append activity indicator to output string
	LCDOutput = LCDOutput + ActivityIndicator[ActivityIndex];
	
	//Send results to LCDs
	UIDisplay = NamedLCD(UILCD);
	if(UIDisplay.Count > 0){
		foreach(var Display in UIDisplay){
			Display.ContentType = ContentType.TEXT_AND_IMAGE;
			Display.Font = "Monospace";
			Display.FontColor = Color.Green;
			Display.FontSize = 0.75f;
			Display.WriteText(LCDOutput);
		}
	}
	
	//Advance activity index animation
	ActivityIndex = ActivityIndex + 1;
	if(ActivityIndex == ActivityIndicator.Length){
		ActivityIndex = 0;
	}
}

void Save(){
	
}

static double SimpleDivision(double Numerator, double Denominator) {
	if (Denominator == 0){
		return 0;
	}
	return Numerator / Denominator;
}

bool ScanTanks(ref TankEntry[] TankList, string Gas, ref double Level, ref double Capacity){
	Level = 0;
	Capacity = 0;
	List<IMyGasTank> Blocks = new List<IMyGasTank>();
	GridTerminalSystem.GetBlocksOfType<IMyGasTank>(Blocks, Filter => Me.IsSameConstructAs(Filter) && Filter.DetailedInfo.Contains(Gas));
	if(Blocks.Count > 0){
		TankList = new TankEntry[Blocks.Count];
		int i = 0;
		foreach(var Block in Blocks){
			Level += Block.FilledRatio;
			Capacity += Block.Capacity;
			TankList[i].Name = Block.CustomName.Substring(Block.CustomName.LastIndexOf('.') + 1);
			TankList[i].Capacity = Block.Capacity;
			TankList[i].Level = Block.FilledRatio;
			TankList[i].Temp = 0.0;
			TankList[i].Delta = 0.0;
			TankList[i].Monitor = true;
			TankList[i].Data = Block.CustomData;
			i++;
		}
		Level = Level / Blocks.Count;
		return true;
	}else{
		return false;
	}
}

string ShortenString(string String, int Limit, int Pre, int Post){
	string Output = "";
	if(String.Length > Limit){
		if(Pre > 0){
			Output += String.Substring(0, Pre) + "...";
		}
		if(Post > 0){
			Output += String.Substring(String.Length - Post, Post);
		}
	}else{
		Output += String;
	}
	return Output;
}

string ToUnits(MyFixedPoint Value){
	if((decimal) Value < 1000){
		return Math.Round((decimal)Value, 2).ToString().PadLeft(3);
	}else if((decimal) Value < 1000000){
		return Math.Round((decimal)Value * (decimal)0.001, 2).ToString().PadLeft(3) + "k";
	}else if((decimal) Value < 1000000000){
		return Math.Round((decimal)Value * (decimal)0.000001, 2).ToString().PadLeft(3) + "M";
	}else if((decimal) Value < 1000000000000){
		return Math.Round((decimal)Value * (decimal)0.0000000001, 2).ToString().PadLeft(3) + "G";
	}else{
		return Math.Round((decimal)Value * (decimal)0.0000000000001, 2).ToString().PadLeft(3) + "T";
	}
}

string ToTime(double Value){
	int Days = 0;
	int Hours = 0;
	int Minutes = 0;
	int Seconds = 0;
	string Time;
	if(Value > 86400){
		Days = (int)(Value / 86400);
		Value = Value % 86400f;
	}
	if(Value > 3600){
		Hours = (int)(Value / 3600);
		Value = Value % 3600f;
	}
	if(Value > 60){
		Minutes = (int)(Value / 60);
		Seconds = (int)(Value % 60);
	}
	if(Days > 999){
		Time = "> 999d";
	}else{
		Time =  " < " + Days.ToString().PadLeft(3) +"d ";
		Time += Hours.ToString().PadLeft(2) + "h ";
		Time += Minutes.ToString().PadLeft(2) + "m ";
		Time += Seconds.ToString().PadLeft(2) + "s";
	}
	return Time;
}

string UnitPrefix(MyFixedPoint Value){
	if((decimal) Value < 1000){
		return "";
	}else if((decimal) Value < 1000000){
		return "k";
	}else if((decimal) Value < 1000000000){
		return "M";
	}else if((decimal) Value < 1000000000000){
		return "G";
	}else{
		return "T";
	}
}

//Returns a list of LCDs whose custom names contain the specified string
//and Cockpit/Programmable Block LCDs whose Custom Data contains the specified string
//Cockpit/Programmable Blocks need to have "Name=Number" in their Custom Data fields
//where Name is the name and Number is the display number within the block
List<IMyTextSurface> NamedLCD(string Name){
	List<IMyTextPanel> LCDs = new List<IMyTextPanel>();
	List<IMyCockpit> Cockpits = new List<IMyCockpit>();
	List<IMyTextSurface> Provider = new List<IMyTextSurface>();
	GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(LCDs, Filter => Me.IsSameConstructAs(Filter) && Filter.CustomName.Contains(Name));
	foreach(var LCD in LCDs){
		Provider.Add(LCD);
	}
	GridTerminalSystem.GetBlocksOfType<IMyCockpit>(Cockpits, Filter => Me.IsSameConstructAs(Filter));
	foreach(var Cockpit in Cockpits){
		if(Cockpit is IMyTextSurfaceProvider){
			string[] Data = Cockpit.CustomData.Trim().Split('\n');
			foreach(var Entry in Data){
				string[] Temp = Entry.Trim().Split('=');
				if(Temp.Length == 2 && Temp[0].Trim() == Name){
					int SurfaceNumber;
					if(int.TryParse(Temp[1].Trim(), out SurfaceNumber)){
						Provider.Add(((IMyTextSurfaceProvider)Cockpit).GetSurface(SurfaceNumber));
						break;
					}else{
						
					}
				}
			}
		}
	}
	return Provider;
}

bool ScanGyros(ref List<IMyGyro> BlockList){
	BlockList.Clear();
	GridTerminalSystem.GetBlocksOfType<IMyGyro>(BlockList, Filter => Me.IsSameConstructAs(Filter));
	if(BlockList.Count > 0){
		return true;
	}else{
		return false;
	}
}

bool ScanMergeBlocks(ref List<IMyShipMergeBlock> BlockList){
	BlockList.Clear();
	GridTerminalSystem.GetBlocksOfType<IMyShipMergeBlock>(BlockList, Filter => Me.IsSameConstructAs(Filter) && Filter.CustomName.Contains("Cluster"));
	if(BlockList.Count > 0){
		return true;
	}else{
		return false;
	}
}

bool ScanThrusters(ref List<IMyThrust> BlockList){
	BlockList.Clear();
	GridTerminalSystem.GetBlocksOfType<IMyThrust>(BlockList, Filter => Me.IsSameConstructAs(Filter));
	if(BlockList.Count > 0){
		return true;
	}else{
		return false;
	}
}

bool ScanWarheads(ref List<IMyWarhead> BlockList){
	BlockList.Clear();
	GridTerminalSystem.GetBlocksOfType<IMyWarhead>(BlockList, Filter => Me.IsSameConstructAs(Filter));
	if(BlockList.Count > 0){
		return true;
	}else{
		return false;
	}
}

IMyShipMergeBlock GetMerge(){
	IMyShipMergeBlock RetVal = null;
	List<IMyShipMergeBlock> BlockList = new List<IMyShipMergeBlock>();
	GridTerminalSystem.GetBlocksOfType<IMyShipMergeBlock>(BlockList, Filter => Me.IsSameConstructAs(Filter));
	foreach(var Block in BlockList){
		if(Block.CustomName == "Missile.Merge"){
			RetVal = Block;
			break;
		}
	}
	return RetVal;
}
