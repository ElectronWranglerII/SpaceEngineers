bool UsePBLCD = false;

static string ProgramSignature = "EWTODO";
static string ProgramName = "ElectronWrangler's ToDo Lists";
static string ProgramVersion = "1.0";
static string ProgramDescription = "To Do lists for LCDs";

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

string LCDQueuedName = "_ToDo_Queued";
string LCDDoneName = "_ToDo_Done";
string LCDOutput;

IMyTextSurface PBDisplay;
string PBOutput;

List<IMyTextSurface> QueueDisplay;
List<IMyTextSurface> DoneDisplay;
List<string> DoneList;
List<string> QueuedList;
string QueuedOutput;
string DoneOutput;
StringBuilder LCDContents;
string[] Lines;
int MarkerIndex;

Program(){
	//Append script name to programmable block's name
	if(!Me.CustomName.Contains(ProgramSignature)){
		Me.CustomName = Me.CustomName + ProgramSignature;
	}
	
	DoneList = new List<string>();
	QueuedList = new List<string>();
	LCDContents = new StringBuilder();
	DoneOutput = "Completed Tasks\n";
	QueuedOutput = "Queued Tasks\n";
	
	DoneDisplay = NamedLCD(LCDDoneName);
	QueueDisplay = NamedLCD(LCDQueuedName);

	foreach(var Display in QueueDisplay){
		Display.ContentType = ContentType.TEXT_AND_IMAGE;
		Display.Font = "Monospace";
		Display.FontColor = Color.Green;
		Display.FontSize = 0.75f;
		Display.WriteText(QueuedOutput);
	}
	foreach(var Display in DoneDisplay){
		Display.ContentType = ContentType.TEXT_AND_IMAGE;
		Display.Font = "Monospace";
		Display.FontColor = Color.Green;
		Display.FontSize = 0.75f;
		Display.WriteText(DoneOutput);
	}
	
	PBDisplay = (IMyTextSurface)(Me.GetSurface(0));
	PBDisplay.ContentType = ContentType.TEXT_AND_IMAGE;

	Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

void Main(string Argument){	
	//Read text from LCDs
	DoneList.Clear();
	QueuedList.Clear();
	
	DoneOutput = "Completed Tasks\n";
	QueuedOutput = "Queued Tasks\n";
	
	DoneDisplay = NamedLCD(LCDDoneName);
	QueueDisplay = NamedLCD(LCDQueuedName);
	
	if(Argument.Length > 0 && Argument.ToUpper() == "CLEAR"){
		Echo("Clearing Displays");
		foreach(var Display in QueueDisplay){
			Display.ContentType = ContentType.TEXT_AND_IMAGE;
			Display.Font = "Monospace";
			Display.FontColor = Color.Green;
			Display.FontSize = 0.75f;
			Display.WriteText(QueuedOutput);
		}
		foreach(var Display in DoneDisplay){
			Display.ContentType = ContentType.TEXT_AND_IMAGE;
			Display.Font = "Monospace";
			Display.FontColor = Color.Green;
			Display.FontSize = 0.75f;
			Display.WriteText(DoneOutput);
		}
	}
	
	foreach(var Display in QueueDisplay){
		Display.ReadText(LCDContents);
		Lines = LCDContents.ToString().Split('\n');
		foreach(var Line in Lines){
			if(!string.IsNullOrEmpty(Line)){
				MarkerIndex = Line.IndexOf(']');
				if(MarkerIndex > 0){
					if(Line.Substring(0, MarkerIndex + 1).ToUpper().Replace(" ", string.Empty) == "[X]"){
						if(!DoneList.Contains(Line)){
							DoneList.Add(Line);
						}
					}else{
						if(!QueuedList.Contains(Line)){
							QueuedList.Add(Line);
						}
					}
				}
			}
		}
	}
	
	foreach(var Display in DoneDisplay){
		Display.ReadText(LCDContents);
		Lines = LCDContents.ToString().Split('\n');
		foreach(var Line in Lines){
			if(!string.IsNullOrEmpty(Line)){
				MarkerIndex = Line.IndexOf(']');
				if(MarkerIndex > 0){
					if(Line.Substring(0, MarkerIndex + 1).ToUpper().Replace(" ", string.Empty) == "[]"){
						if(!QueuedList.Contains(Line)){
							QueuedList.Add(Line);
						}
					}else{
						if(!DoneList.Contains(Line)){
							DoneList.Add(Line);
						}
					}
				}
			}
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
	
	//Send results to LCDs
	for(int i = 0; i < DoneList.Count; i++){
		DoneOutput += DoneList[i] + '\n';
	}
	for(int i = 0; i < QueuedList.Count; i++){
		QueuedOutput += QueuedList[i] + '\n';
	}
	foreach(var Display in QueueDisplay){
		Display.ContentType = ContentType.TEXT_AND_IMAGE;
		Display.Font = "Monospace";
		Display.FontColor = Color.Green;
		Display.FontSize = 0.75f;
		Display.WriteText(QueuedOutput);
	}
	foreach(var Display in DoneDisplay){
		Display.ContentType = ContentType.TEXT_AND_IMAGE;
		Display.Font = "Monospace";
		Display.FontColor = Color.Green;
		Display.FontSize = 0.75f;
		Display.WriteText(DoneOutput);
	}

	//Advance activity index animation
	ActivityIndex = ActivityIndex + 1;
	if(ActivityIndex == ActivityIndicator.Length){
		ActivityIndex = 0;
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
