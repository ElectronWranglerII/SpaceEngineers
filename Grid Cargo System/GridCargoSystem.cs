/*
* This work is made available under the Creative Commons
* Attribution-NonCommercial-ShareAlike 4.0 International License.
* To view a copy of this license, visit
* http://creativecommons.org/licenses/by-nc-sa/4.0/.
*/

string UILCD = "GCSLCD";

string LCDControllerName = "EWLCDCTL";

/*
Valid terminal types:
Aerospace - flying vehicles
Container - 
Wheeled - wheeled vehicles
*/
string TerminalType = "Cargo";

//*****************************************************************************
// System Variables
//*****************************************************************************
string ProgramSignature = "_GCS";
string ProgramName = "ElectronWrangler's Grid Cargo System";
string ProgramVersion = "1.0";
string ProgramDescription = "Grid Cargo System";

IMyTextSurface PBDisplay;
List<IMyTextSurfaceProvider> UIDisplay;

List<IMyProgrammableBlock> LCDController;

int ErrorCount = 0;
int WarningCount = 0;

List<string> Menus = new List<string>{
	"Thruster Calculator",
	""
};

string LCDOutput;

int ActivityIndex = 0;
string[] ActivityIndicator = {
	"|",
	"/",
	"-",
	"\\"
};

List<IMyShipController> ShipControllers = new List<IMyShipController>();
IMyShipController ShipController;

double ShipOEM;
double ShipAUM;

List<IMyShipMergeBlock> MergeBlocks = new List<IMyShipMergeBlock>();
int UsedSlots;


Program(){
	//Append script name to programmable block's name
	if(!Me.CustomName.Contains(ProgramSignature)){
		Me.CustomName = Me.CustomName + ProgramSignature;
	}
	
	PBDisplay = (IMyTextSurface)(Me.GetSurface(0));
	PBDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
	PBDisplay.Font = "Debug";
	PBDisplay.FontColor = Color.Green;
	PBDisplay.FontSize = 0.75f;
	
	//Try to get the programmable block running EW's LCD controller script
	LCDController = ComputerNameContaining(LCDControllerName);
	if(LCDController.Count == 1){
		Echo(LCDController[0].CustomData);
		LCDController[0].CustomData = LCDController[0].CustomData + Me.CustomName + " register " + UILCD + "\n";
	}else{
		Echo("Could not find LCD controller");
	}

	Runtime.UpdateFrequency = UpdateFrequency.Update100;
}


public void Main(string argument){
	//Clear LCD output
	LCDOutput = "";
	
	//Print program info to programmable block's display
	if(ErrorCount > 0){
		PBDisplay.FontColor = Color.Red;
	}else if(WarningCount > 0){
		PBDisplay.FontColor = Color.Yellow;
	}else{
		PBDisplay.FontColor = Color.Green;
	}
	LCDOutput = LCDOutput + ProgramName + "\n" + "Version: " + ProgramVersion + "\n" + ProgramDescription + "\n";
	
	//Find ship controller
	ShipControllers = EnumerateGridControllers();
	if(ShipControllers.Count > 0){
		ShipController = ShipControllers[0];
	}
	if(ShipController == null){
		Echo("Could not find ship controller!");
	}else{
		//Calculate ship's mass
		ShipOEM = ShipController.CalculateShipMass().BaseMass;
		ShipAUM = ShipController.CalculateShipMass().PhysicalMass;
	}
	
	MergeBlocks = EnumerateBaseMergeBlocks();
	UsedSlots = 0;
	if(MergeBlocks.Count > 0){
		foreach(var MergeBlock in MergeBlocks){
			if(MergeBlock.IsConnected){
				UsedSlots = UsedSlots + 1;
			}
		}
		LCDOutput = LCDOutput + UsedSlots.ToString() + " of " + MergeBlocks.Count.ToString() + " rack slots in use\n";
	}else{
		LCDOutput = LCDOutput + "No functional rack slots detected\n";
	}
	
	LCDOutput = LCDOutput + ShipAUM.ToString() + "kg\n";
	LCDOutput = LCDOutput + ShipOEM.ToString() + "kg\n";
	
	LCDOutput = LCDOutput + ActivityIndicator[ActivityIndex];
	
	//
	
	//
	PBDisplay.WriteText(LCDOutput);
	
	ActivityIndex = ActivityIndex + 1;
	if(ActivityIndex == ActivityIndicator.Length){
		ActivityIndex = 0;
	}
}


//Returns a list of controllers whose grid name is the same as the grid name of the
//programmable block the script is running on
List<IMyShipController> EnumerateGridControllers(){
	List<IMyShipController> Controllers = new List<IMyShipController>();
	GridTerminalSystem.GetBlocksOfType<IMyShipController>(Controllers, Filter => ((Filter.CubeGrid.CustomName == Me.CubeGrid.CustomName) && Filter.IsWorking));
	return Controllers;
}


//Returns a list of merge blocks whose base name is the same as the base name of the
//programmable blocks the script is running on
List<IMyShipMergeBlock> EnumerateBaseMergeBlocks(){
	List<IMyShipMergeBlock> MergeBlocks = new List<IMyShipMergeBlock>();
	int Stop = Me.CustomName.IndexOf('.');
	if(Stop >= 0){
		string BaseName = Me.CustomName.Substring(0, Stop);
		GridTerminalSystem.GetBlocksOfType<IMyShipMergeBlock>(MergeBlocks, Filter => (Filter.CubeGrid.CustomName == Me.CubeGrid.CustomName));
		for(int i = MergeBlocks.Count - 1; i > -1; i--){
			Stop = MergeBlocks[i].CustomName.IndexOf('.');
			if(Stop > 0){
				if(MergeBlocks[i].CustomName.Substring(0, Stop) != BaseName){
					MergeBlocks.RemoveAt(i);
				}
			}else{
				MergeBlocks.RemoveAt(i);
			}
		}
	}
	foreach(var MergeBlock in MergeBlocks){
		Echo(MergeBlock.CustomName);
	}
	return MergeBlocks;
}


//Returns a list of programmable blocks whose custom names contain the specified string
List<IMyProgrammableBlock> ComputerNameContaining(string Text){
	List<IMyProgrammableBlock> Blocks = new List<IMyProgrammableBlock>();
	GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(Blocks, Filter => Filter.CustomName.Contains(Text));
	return Blocks;
}


//Returns a list of LCDs whose custom names contain the specified string
List<IMyTextSurfaceProvider> NamedLCD(string Name){
	List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
	List<IMyTextSurfaceProvider> Provider = new List<IMyTextSurfaceProvider>();
	GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(Blocks, Filter => Filter.CustomName.Contains(Name));
	if(Blocks.Count > 0){
		foreach(var Block in Blocks){
			Provider.Add((IMyTextSurfaceProvider)Block);
		}
	}
	return Provider;
}
