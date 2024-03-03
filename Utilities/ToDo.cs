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

bool ShowActivity = true;

string LCDQueuedName = "_ToDo_Queued";
string LCDDoneName = "_ToDo_Done";
string LCDOutput;

IMyTextSurface PBDisplay;
string PBOutput;

List<IMyTextSurface> QueueDisplay;
List<IMyTextSurface> DoneDisplay;
string QueuedOutput;
string DoneOutput;
string LCDContents;
string[] Lines;

Program(){
	//Append script name to programmable block's name
	if(!Me.CustomName.Contains(ProgramSignature)){
		Me.CustomName = Me.CustomName + ProgramSignature;
	}
	
	PBDisplay = (IMyTextSurface)(Me.GetSurface(0));
	PBDisplay.ContentType = ContentType.TEXT_AND_IMAGE;

	Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

void Main(string Argument){
	//Read text from LCDs
	DoneOutput = string.Empty;
	QueuedOutput = string.Empty;
	
	DoneDisplay = NamedLCD(LCDDoneName);
	QueueDisplay = NamedLCD(LCDQueuedName);
	
	foreach(var Display in QueueDisplay){
		Display.ReadText(LCDContents);
		Lines = LCDContents.Split('\n');
		foreach(var Line in Lines){
			if(!string.IsNullOrEmpty(Line) && Line.Substring(0, Line.IndexOf(']')).ToUpper().Replace(' ', string.Empty) == "[X]"){
				DoneOutput = DoneOutput + Line + '\n';
			}else{
				QueuedOutput = QueuedOutput + Line + '\n';
			}
		}
	}
	
	foreach(var Display in DoneDisplay){
		Display.ReadText(LCDContents);
		Lines = LCDContents.Split('\n');
		foreach(var Line in Lines){
			if(!string.IsNullOrEmpty(Line) && Line.Substring(0, Line.IndexOf(']')).ToUpper().Replace(' ', string.Empty) == "[]"){
				QueuedOutput = QueuedOutput + Line + '\n';
			}else{
				DoneOutput = DoneOutput + Line + '\n';
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
	if(ShowActivity){
		DoneOutput += ActivityIndicator[ActivityIndex];
		QueuedOutput += ActivityIndicator[ActivityIndex];
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
