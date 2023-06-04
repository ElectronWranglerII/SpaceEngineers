/*
* This work is made available under the Creative Commons
* Attribution-NonCommercial-ShareAlike 4.0 International License.
* To view a copy of this license, visit
* http://creativecommons.org/licenses/by-nc-sa/4.0/.
*/

//*****************************************************************************
// User Definable Variables
//*****************************************************************************
bool DisplayActiveConnectors = true;
bool DisplayInactiveConnectors = true;
bool DisplayInopConnectors = true;
bool DisplayReadyConnectors = true;

//Set to true to force system to empty connectors into container inventories
bool EmptyConnectors = true;

//List of protected connectors and grids permitted to connect to them
//Each entry may use a whitelist or blacklist
//Entry format:
//"ConnectorName:ListType:GridName, GridName"
//Entry example:
//"LandingPad_0.Connector:Whitelist:Heavy_Lift,SmallMiner"
List<string> ProtectedList = new List<string>{
	"LandingPad_0.Connector:Whitelist:Heavy_Lift"
};

//List of connectors to use safe disconnect with
//Safe disconnect will ensure grids perform the following before disconnecting:
//Turn batteries to auto
//Disable hydrogen tank stockpiling
//Turn thrusters on
List<string> SafeDisconnectList = new List<string>{
	
};


//*****************************************************************************
// System Variables
//*****************************************************************************
string ProgramName = "EW's Connector Status & Control";
string ProgramVersion = "1.0";
string ProgramDescription = "Monitors & displays status of connectors on this program block's grid";

int ErrorCount = 0;
int WarningCount = 0;

List<IMyShipConnector> ActiveConnector;
List<IMyShipConnector> InactiveConnector;
List<IMyShipConnector> InopConnector;
List<IMyShipConnector> ReadyConnector;

bool CanDisplayActiveConnectors = false;
bool CanDisplayInactiveConnectors = false;
bool CanDisplayInopConnectors = false;
bool CanDisplayReadyConnectors = false;

List<IMyTextSurfaceProvider> LCDActiveConnectors;
List<IMyTextSurfaceProvider> LCDInactiveConnectors;
List<IMyTextSurfaceProvider> LCDReadyConnectors;
List<IMyTextSurfaceProvider> LCDInopConnectors;
string LCDConnectorStatusText;

IMyTextSurface PBDisplay;

struct ProtectedEntry{
	string ConnectorName;
	int ListType;
	List<string> GridList;
}
List<ProtectedEntry> ProtectedConnector;


Program(){
	PBDisplay = (IMyTextSurface)(Me.GetSurface(0));
	PBDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
	
	ActiveConnector = new List<IMyShipConnector>();
	InactiveConnector = new List<IMyShipConnector>();
	InopConnector = new List<IMyShipConnector>();
	ReadyConnector = new List<IMyShipConnector>();
	
/* 	foreach(var Entry in ProtectedList){
		string[] Parameters = Entry.Split(":");
		
	} */
	
	Runtime.UpdateFrequency = UpdateFrequency.Update100;
}


public void Main(string argument){
	//Print program info to programmable block's display
	if(ErrorCount > 0){
		PBDisplay.FontColor = Color.Red;
	}else if(WarningCount > 0){
		PBDisplay.FontColor = Color.Yellow;
	}else{
		PBDisplay.FontColor = Color.Green;
	}
	PBDisplay.WriteText("Running: " + ProgramName + "\n" + "Version: " + ProgramVersion + "\n" + ProgramDescription);
	
	//Process arguments

	
	//Get LCD for displaying active connectors
	LCDActiveConnectors = NamedLCD("Active Connectors");
	if(LCDActiveConnectors.Count > 0){
		CanDisplayActiveConnectors = true;
		foreach(IMyTextSurface Display in LCDActiveConnectors){
			Display.ContentType = ContentType.TEXT_AND_IMAGE;
			Display.FontColor = Color.Green;
		}
	}
	
	//Get LCD for displaying inactive connectors
	LCDInactiveConnectors = NamedLCD("Inactive Connectors");
	if(LCDInactiveConnectors.Count > 0){
		CanDisplayInactiveConnectors = true;
		foreach(IMyTextSurface Display in LCDInactiveConnectors){
			Display.ContentType = ContentType.TEXT_AND_IMAGE;
			Display.FontColor = Color.White;
		}
	}
	
	//Get LCD for displaying inoperative connectors
	LCDInopConnectors = NamedLCD("Inop Connectors");
	if(LCDInopConnectors.Count > 0){
		CanDisplayInopConnectors = true;
		foreach(IMyTextSurface Display in LCDInopConnectors){
			Display.ContentType = ContentType.TEXT_AND_IMAGE;
			Display.FontColor = Color.Red;
		}
	}
	
	//Get LCD for displaying ready connectors
	LCDReadyConnectors = NamedLCD("Ready Connectors");
	if(LCDReadyConnectors.Count > 0){
		CanDisplayReadyConnectors = true;
		foreach(IMyTextSurface Display in LCDReadyConnectors){
			Display.ContentType = ContentType.TEXT_AND_IMAGE;
			Display.FontColor = Color.Yellow;
		}
	}
	
	//Get a list of unconnected connectors
	InactiveConnector = InactiveConnectors();
	if(DisplayInactiveConnectors && CanDisplayInactiveConnectors){
		LCDConnectorStatusText = "Inactive Connectors:" + InactiveConnector.Count.ToString() + "\n";
		foreach(var Entry in InactiveConnector){
			if(Entry.IsSameConstructAs(Me)){
				LCDConnectorStatusText += Entry.CustomName + "\n";
			}
		}
		foreach(IMyTextSurface Display in LCDInactiveConnectors){
			Display.WriteText(LCDConnectorStatusText);
		}
	}
	
	//Get a list of inoperative connectors
	InopConnector = InopConnectors();
	if(DisplayInopConnectors && CanDisplayInopConnectors){
		LCDConnectorStatusText = "Inoperative Connectors:" + InopConnector.Count.ToString() + "\n";
		foreach(var Entry in InopConnector){
			if(Entry.IsSameConstructAs(Me)){
				LCDConnectorStatusText += Entry.CustomName + "\n";
			}
		}
		foreach(IMyTextSurface Display in LCDInopConnectors){
			Display.WriteText(LCDConnectorStatusText);
		}
	}
	
	//Get a list of connected connectors
	ActiveConnector = ActiveConnectors();
	if(DisplayActiveConnectors && CanDisplayActiveConnectors){
		LCDConnectorStatusText = "Active Connectors:" + InactiveConnector.Count.ToString() + "\n";
		foreach(var Entry in ActiveConnector){
			if(Entry.IsSameConstructAs(Me)){
				LCDConnectorStatusText += Entry.CustomName + "-->" + Entry.OtherConnector.CubeGrid.CustomName + "\n";
			}
		}
		foreach(IMyTextSurface Display in LCDActiveConnectors){
			Display.WriteText(LCDConnectorStatusText);
		}
	}
	
	//Get a list of ready connectors
	ReadyConnector = ReadyConnectors();
	if(DisplayReadyConnectors && CanDisplayReadyConnectors){
		LCDConnectorStatusText = "Ready Connectors:" + ReadyConnector.Count.ToString() + "\n";
		foreach(var Entry in ReadyConnector){
			if(Entry.IsSameConstructAs(Me)){
				LCDConnectorStatusText += Entry.CustomName + "\n";
			}
		}
		foreach(IMyTextSurface Display in LCDReadyConnectors){
			Display.WriteText(LCDConnectorStatusText);
		}
	}
	
	//Process protected connectors
	
/* 	if(IsGridConnected("Ore Car")){
		Echo("Ore Car is connected");
	}else{
		Echo("Ore Car is not connected");
	} */
	
/* 	List<IMyCubeGrid> BunchOfGrids = new List<IMyCubeGrid>(EnumerateConnectedGrids());
	foreach(var Grid in BunchOfGrids){
		Echo(Grid.CustomName);
	} */
	
/* 	List<string> GridNameList = new List<string>(EnumerateGridNames());
	foreach(var Entry in GridNameList){
		Echo(Entry);
	} */
	
/* 	
	List<string> GridList = new List<string>(EnumerateGrids());
	foreach(var Entry in GridList){
		Echo(Entry);
	} */
}


//Returns a list of grid names composing the construct
//Grid names are set using the info tab of the UI
List<string> EnumerateGridNames(){
	List<string> GridNames = new List<string>();
	List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
	GridTerminalSystem.GetBlocks(Blocks);
	foreach(var Block in Blocks){
		if(!GridNames.Contains(Block.CubeGrid.CustomName)){
			GridNames.Add(Block.CubeGrid.CustomName);
		}
	}
	return GridNames;
}


//Returns a list of grids composing the construct
//Unique grids are indicated by the format of the block names within the grid
//Format:
//Grid.Subgrid.Component.Subcomponent
//Example (a connector within a landing pad grid):
//LandingPad_0.Connector
List<string> EnumerateGrids(){
	List<string> Grids = new List<string>();
	List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
	GridTerminalSystem.GetBlocks(Blocks);
	foreach(var Block in Blocks){
		int Stop = Block.CustomName.IndexOf('.');
		if(Stop >= 0){
			string GridName = Block.CustomName.Substring(0, Stop);
			if(!Grids.Contains(GridName)){
				Grids.Add(GridName);
			}
		}
	}
	return Grids;
}


//Returns a list of LCDs with the specified custom name
List<IMyTextSurfaceProvider> NamedLCD(string Name){
	List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
	List<IMyTextSurfaceProvider> Provider = new List<IMyTextSurfaceProvider>();
	GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(Blocks, Filter => Filter.CustomName.Contains(Name));
	foreach(var Block in Blocks){
		Provider.Add((IMyTextSurfaceProvider)Block);
	}
	return Provider;
}

//Returns true if the grid with the specified custom name is connected
bool IsGridConnected(string Name){
	bool RetVal = false;
	foreach(var Entry in ActiveConnector){
		if(Entry.OtherConnector.CubeGrid.CustomName == Name){
			RetVal = true;
		}
	}
	return RetVal;
}

//Returns a list containing connectors having connections
List<IMyShipConnector> ActiveConnectors(){
	List<IMyShipConnector> Blocks = new List<IMyShipConnector>();
	GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(Blocks, Filter => Filter.Status.Equals(MyShipConnectorStatus.Connected));
	return Blocks;
}

//Returns a list containing connectors not having connections
List<IMyShipConnector> InactiveConnectors(){
	List<IMyShipConnector> Blocks = new List<IMyShipConnector>();
	GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(Blocks, Filter => Filter.Status.Equals(MyShipConnectorStatus.Unconnected));
	return Blocks;
}

//Returns a list containing inoperative connectors
List<IMyShipConnector> InopConnectors(){
	List<IMyShipConnector> Blocks = new List<IMyShipConnector>();
	GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(Blocks, Filter => !Filter.IsWorking);
	return Blocks;
}

//Returns a list containing connectors ready to connect
List<IMyShipConnector> ReadyConnectors(){
	List<IMyShipConnector> Blocks = new List<IMyShipConnector>();
	GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(Blocks, Filter => Filter.Status.Equals(MyShipConnectorStatus.Connectable));
	return Blocks;
}


//Returns a list containing grids connected via connectors
List<IMyCubeGrid> EnumerateConnectedGrids(){
	List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
	List<IMyShipConnector> Blocks = new List<IMyShipConnector>();
	//GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(Blocks, Filter => Filter.IsSameConstructAs(Me));
	GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(Blocks);
	if(Blocks.Count > 0){
		foreach(var Block in Blocks){
			if(Block.Status != MyShipConnectorStatus.Connected){
				continue;
			}else{
				Grids.Add(Block.OtherConnector.CubeGrid);
			}
		}
	}
	return Grids;
}
