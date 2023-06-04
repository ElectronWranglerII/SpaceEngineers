/*
* This work is made available under the Creative Commons
* Attribution-NonCommercial-ShareAlike 4.0 International License.
* To view a copy of this license, visit
* http://creativecommons.org/licenses/by-nc-sa/4.0/.
*/

string UILCD = "FMCLCD";

string LCDControllerName = "EWLCDCTL";

//*****************************************************************************
// System Variables
//*****************************************************************************
string ProgramSignature = "_EWFMC";
string ProgramName = "ElectronWrangler's Flight Computer";
string ProgramVersion = "1.0";
string ProgramDescription = "Flight Computer";

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

bool InsideNaturalGravity = false;
Vector3D NaturalGravity;
double GravitationalAcceleration;
double GravitationalAccelerationG;

double ShipOEM;
double ShipAUM;
Vector3D ShipVelocity;
Vector3D Forward;
Vector3D Left;
Vector3D Up;

Matrix InverseWorldRotation;

double AltimeterAGL;
double AltimeterMSL;

List<IMyThrust> Thrusters = new List<IMyThrust>();
List<IMyThrust> ThrustersForward = new List<IMyThrust>();
List<IMyThrust> ThrustersBackward = new List<IMyThrust>();
List<IMyThrust> ThrustersLeft = new List<IMyThrust>();
List<IMyThrust> ThrustersRight = new List<IMyThrust>();
List<IMyThrust> ThrustersUp = new List<IMyThrust>();
List<IMyThrust> ThrustersDown = new List<IMyThrust>();

double TotalMassForward;
double TotalMassBackward;
double TotalMassLeft;
double TotalMassRight;
double TotalMassUp;
double TotalMassDown;

double TotalThrustForward;
double TotalThrustBackward;
double TotalThrustLeft;
double TotalThrustRight;
double TotalThrustUp;
double TotalThrustDown;

double LiftCapacityForward;
double LiftCapacityBackward;
double LiftCapacityLeft;
double LiftCapacityRight;
double LiftCapacityUp;
double LiftCapacityDown;

double StopDistanceForward;
double StopDistanceBackward;
double StopDistanceLeft;
double StopDistanceRight;
double StopDistanceUp;
double StopDistanceDown;

double StopTimeForward;
double StopTimeBackward;
double StopTimeLeft;
double StopTimeRight;
double StopTimeUp;
double StopTimeDown;


Program(){
	//Append script name to programmable block's name
	if(!Me.CustomName.Contains(ProgramSignature)){
		Me.CustomName = Me.CustomName + ProgramSignature;
	}
	
	PBDisplay = (IMyTextSurface)(Me.GetSurface(0));
	PBDisplay.ContentType = ContentType.TEXT_AND_IMAGE;
	
	//Try to get the programmable block running EW's LCD controller script
	LCDController = ComputerNameContaining(LCDControllerName);
	if(LCDController.Count == 1){
		Echo(LCDController[0].CustomData);
		LCDController[0].CustomData = LCDController[0].CustomData + Me.CustomName + " register " + UILCD + "\n";
	}else{
		Echo("Could not find LCD controller");
	}

	Runtime.UpdateFrequency = UpdateFrequency.Update10;
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
	PBDisplay.WriteText("Running: " + ProgramName + "\n" + "Version: " + ProgramVersion + "\n" + ProgramDescription + "\n" + ActivityIndicator[ActivityIndex]);
	
	//Find main ship controller
	ShipControllers = EnumerateConstructControllers();
	if(ShipControllers.Count > 0){
		foreach(var Controller in ShipControllers){
			if(Controller.IsMainCockpit){
				ShipController = Controller;
				continue;
			}
		}
	}
	if(ShipController == null){
		Echo("Could not find ship controller!");
		Echo("Is cockpit or remote set to main controller?");
	}else{
		//Calculate gravitational pull at ship's location
		InsideNaturalGravity = ShipController.TryGetPlanetElevation(MyPlanetElevation.Surface, out AltimeterAGL);
		InsideNaturalGravity = ShipController.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out AltimeterMSL);
		NaturalGravity = ShipController.GetNaturalGravity();
		GravitationalAcceleration = NaturalGravity.Length();
		GravitationalAccelerationG = GravitationalAcceleration / 9.81;
		Up = -NaturalGravity;
		Left = Vector3D.Cross(Up, ShipController.WorldMatrix.Forward);
		Forward = Vector3D.Cross(Left, Up);
		
		//Calculate ship's mass and velocity
		ShipOEM = ShipController.CalculateShipMass().BaseMass;
		ShipAUM = ShipController.CalculateShipMass().PhysicalMass;
		InverseWorldRotation = Matrix.Invert(Matrix.Normalize(ShipController.CubeGrid.WorldMatrix)).GetOrientation();
		ShipVelocity = Vector3.Transform(ShipController.GetShipVelocities().LinearVelocity, InverseWorldRotation);
	}
	
	//Clear thruster data
	Thrusters.Clear();
	ThrustersForward.Clear();
	ThrustersBackward.Clear();
	ThrustersLeft.Clear();
	ThrustersRight.Clear();
	ThrustersUp.Clear();
	ThrustersDown.Clear();
	
	//Clear thrust totals
	TotalThrustForward = 0;
	TotalThrustBackward = 0;
	TotalThrustLeft = 0;
	TotalThrustRight = 0;
	TotalThrustUp = 0;
	TotalThrustDown = 0;
	
	//Clear stop distance data
	StopDistanceForward = 0;
	StopDistanceBackward = 0;
	StopDistanceLeft = 0;
	StopDistanceRight = 0;
	StopDistanceUp = 0;
	StopDistanceDown = 0;
	StopTimeForward = 0;
	StopTimeBackward = 0;
	StopTimeLeft = 0;
	StopTimeRight = 0;
	StopTimeUp = 0;
	StopTimeDown = 0;
	
	//Find functioning thrusters and their facing
	Thrusters = EnumerateConstructThrusters();
	if(Thrusters.Count > 0){
		foreach(var Thruster in Thrusters){
            if (Thruster.GridThrustDirection.Z == 1){
				ThrustersForward.Add(Thruster);
				TotalThrustForward = TotalThrustForward + Thruster.MaxThrust;
			}
			if (Thruster.GridThrustDirection.Z == -1){
				ThrustersBackward.Add(Thruster);
				TotalThrustBackward = TotalThrustBackward + Thruster.MaxThrust;
			}
            if (Thruster.GridThrustDirection.Y == 1){
				ThrustersUp.Add(Thruster);
				TotalThrustUp = TotalThrustUp + Thruster.MaxThrust;
			}
			if (Thruster.GridThrustDirection.Y == -1){
				ThrustersDown.Add(Thruster);
				TotalThrustDown = TotalThrustDown + Thruster.MaxThrust;
			}
            if (Thruster.GridThrustDirection.X == 1){
				ThrustersLeft.Add(Thruster);
				TotalThrustLeft = TotalThrustLeft + Thruster.MaxThrust;
			}
			if (Thruster.GridThrustDirection.X == -1){
				ThrustersRight.Add(Thruster);
				TotalThrustRight = TotalThrustRight + Thruster.MaxThrust;
			}
		}
	}
	
	//Calculate total mass thrusters can support
	if(InsideNaturalGravity){
		TotalMassForward = TotalThrustForward / GravitationalAcceleration;
		TotalMassBackward = TotalThrustBackward / GravitationalAcceleration;
		TotalMassLeft = TotalThrustLeft / GravitationalAcceleration;
		TotalMassRight = TotalThrustRight / GravitationalAcceleration;
		TotalMassUp = TotalThrustUp / GravitationalAcceleration;
		TotalMassDown = TotalThrustDown / GravitationalAcceleration;
	}else{
		TotalMassForward = Double.PositiveInfinity;
		TotalMassBackward = Double.PositiveInfinity;
		TotalMassLeft = Double.PositiveInfinity;
		TotalMassRight = Double.PositiveInfinity;
		TotalMassUp = Double.PositiveInfinity;
		TotalMassDown = Double.PositiveInfinity;
	}
	
	StopDistanceForward = RetrofireDistance(NaturalGravity.Z, ShipAUM, TotalThrustForward, ShipVelocity.Z);
	StopTimeForward = RetrofireTime(NaturalGravity.Z, ShipAUM, TotalThrustForward, ShipVelocity.Z);
	StopDistanceBackward = RetrofireDistance(NaturalGravity.Z, ShipAUM, TotalThrustBackward, ShipVelocity.Z);
	StopTimeBackward = RetrofireTime(NaturalGravity.Z, ShipAUM, TotalThrustBackward, ShipVelocity.Z);
	StopDistanceLeft = RetrofireDistance(NaturalGravity.X, ShipAUM, TotalThrustLeft, ShipVelocity.X);
	StopTimeLeft = RetrofireTime(NaturalGravity.X, ShipAUM, TotalThrustLeft, ShipVelocity.X);
	StopDistanceRight = RetrofireDistance(NaturalGravity.X, ShipAUM, TotalThrustRight, ShipVelocity.X);
	StopTimeRight = RetrofireTime(NaturalGravity.X, ShipAUM, TotalThrustRight, ShipVelocity.X);
	StopDistanceUp = RetrofireDistance(NaturalGravity.Y, ShipAUM, TotalThrustUp, ShipVelocity.Y);
	StopTimeUp = RetrofireTime(NaturalGravity.Y, ShipAUM, TotalThrustUp, ShipVelocity.Y);
	StopDistanceDown = RetrofireDistance(NaturalGravity.Y, ShipAUM, TotalThrustDown, ShipVelocity.Y);
	StopTimeDown = RetrofireTime(NaturalGravity.Y, ShipAUM, TotalThrustDown, ShipVelocity.Y);

	Vector3 gravityVec = Vector3.Multiply(Vector3.Normalize(NaturalGravity), -1);
	LCDOutput = LCDOutput + GravitationalAccelerationG.ToString("0.00") + " G (" + GravitationalAcceleration.ToString("0.00") + " m/s/s)\n";
	//LCDOutput = LCDOutput + "G(X):" + NaturalGravity.X.ToString("0.00") + ", " + "G(Y):" + NaturalGravity.Y.ToString("0.00") + ", " + "G(Z):" + NaturalGravity.Z.ToString("0.00") + "\n";
	LCDOutput = LCDOutput + "G(X):" + gravityVec.X.ToString("0.00") + ", " + "G(Y):" + gravityVec.Y.ToString("0.00") + ", " + "G(Z):" + gravityVec.Z.ToString("0.00") + "\n";
	
	LCDOutput = LCDOutput + "V(X):" + ShipVelocity.X.ToString("0.00") + ", " + "V(Y):" + ShipVelocity.Y.ToString("0.00") + ", " + "V(Z):" + ShipVelocity.Z.ToString("0.00") + "\n";
	
 	LCDOutput = LCDOutput + "Forward: " + ThrustersForward.Count.ToString() + "\n";
	LCDOutput = LCDOutput + TotalThrustForward.ToString("0") + " N (" + TotalMassForward.ToString("0") + " kg)" + "\n";
	LCDOutput = LCDOutput + "Backward: " + ThrustersBackward.Count.ToString() + "\n";
	LCDOutput = LCDOutput + TotalThrustBackward.ToString("0") + " N (" + TotalMassBackward.ToString("0") + " kg)" + "\n";
	LCDOutput = LCDOutput + "Left: " + ThrustersLeft.Count.ToString() + "\n";
	LCDOutput = LCDOutput + TotalThrustLeft.ToString("0") + " N (" + TotalMassLeft.ToString("0") + " kg)" + "\n";
	LCDOutput = LCDOutput + "Right: " + ThrustersRight.Count.ToString() + "\n";
	LCDOutput = LCDOutput + TotalThrustRight.ToString("0") + " N (" + TotalMassRight.ToString("0") + " kg)" + "\n";
	LCDOutput = LCDOutput + "Up: " + ThrustersUp.Count.ToString() + "\n";
	LCDOutput = LCDOutput + TotalThrustUp.ToString("0") + " N (" + TotalMassUp.ToString("0") + " kg)" + "\n";
	LCDOutput = LCDOutput + "Down: " + ThrustersDown.Count.ToString() + "\n";
	LCDOutput = LCDOutput + TotalThrustDown.ToString("0") + " N (" + TotalMassDown.ToString("0") + " kg)" + "\n";
	
	//LCDOutput = LCDOutput + "F: " + ShipVelocity.Z.ToString("0.0") + " m/s " + StopDistanceForward.ToString("0.0") + " m (" + StopTimeForward.ToString("0.0") + " s)" + "\n";
	//LCDOutput = LCDOutput + "B: " + ShipVelocity.Z.ToString("0.0") + " m/s " + StopDistanceBackward.ToString("0.0") + " m (" + StopTimeBackward.ToString("0.0") + " s)" + "\n";
	//LCDOutput = LCDOutput + "L: " + ShipVelocity.X.ToString("0.0") + " m/s " + StopDistanceLeft.ToString("0.0") + " m (" + StopTimeLeft.ToString("0.0") + " s)" + "\n";
	//LCDOutput = LCDOutput + "R: " + ShipVelocity.X.ToString("0.0") + " m/s " + StopDistanceRight.ToString("0.0") + " m (" + StopTimeRight.ToString("0.0") + " s)" + "\n";
	//LCDOutput = LCDOutput + "U: " + ShipVelocity.Y.ToString("0.0") + " m/s " + StopDistanceUp.ToString("0.0") + " m (" + StopTimeUp.ToString("0.0") + " s)" + "\n";
	//LCDOutput = LCDOutput + "D: " + ShipVelocity.Y.ToString("0.0") + " m/s " + StopDistanceDown.ToString("0.0") + " m (" + StopTimeDown.ToString("0.0") + " s)" + "\n";
	
	LCDOutput = LCDOutput + "F: " + Forward.X.ToString("0.0") + ", " + Forward.Y.ToString("0.0") + ", "  + Forward.Z.ToString("0.0") + "\n";
	LCDOutput = LCDOutput + "L: " + Left.X.ToString("0.0") + ", " + Left.Y.ToString("0.0") + ", "  + Left.Z.ToString("0.0") + "\n";
	LCDOutput = LCDOutput + "U: " + Up.X.ToString("0.0") + ", " + Up.Y.ToString("0.0") + ", "  + Up.Z.ToString("0.0") + "\n";
	
	LCDOutput = LCDOutput + ActivityIndicator[ActivityIndex];
	
	//
	
	//Get LCD for interacting with the user
	UIDisplay = NamedLCD(UILCD);
	if(UIDisplay.Count > 0){
		foreach(IMyTextSurface Display in UIDisplay){
			Display.ContentType = ContentType.TEXT_AND_IMAGE;
			Display.Font = "Monospace";
			Display.FontColor = Color.Green;
			Display.FontSize = 0.75f;
			Display.WriteText(LCDOutput);
		}
	}
	
	ActivityIndex = ActivityIndex + 1;
	if(ActivityIndex == ActivityIndicator.Length){
		ActivityIndex = 0;
	}
}


List<IMyShipController> EnumerateConstructControllers(){
	List<IMyShipController> Controllers = new List<IMyShipController>();
	GridTerminalSystem.GetBlocksOfType<IMyShipController>(Controllers, Filter => (Filter.IsSameConstructAs(Me) && Filter.IsWorking));
	return Controllers;
}


//Returns a list of thrusters in the programmable block's construct
List<IMyThrust> EnumerateConstructThrusters(){
	List<IMyThrust> Thrusters = new List<IMyThrust>();
	GridTerminalSystem.GetBlocksOfType<IMyThrust>(Thrusters, Filter => (Filter.IsSameConstructAs(Me) && Filter.IsWorking));
	return Thrusters;
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

//Computes the retrofire distance
//Acceleration = acceleration due to gravity, in m/s/s
//TotalMass = mass of grid, in kg
//TotalThrust = stopping thrust, in Newtons
//Speed = speed of grid, in m/s
double RetrofireDistance(double Acceleration, double TotalMass, double TotalThrust, double Speed){
	double Downstairs = TotalThrust - Acceleration * TotalMass;
	double Distance = 0;
	if(Downstairs != 0){
		Distance = (Speed * Speed * TotalMass) / (2 * Downstairs);
	}
	return Distance;
}

//Computes the retrofire time
//Acceleration = acceleration due to gravity, in m/s/s
//TotalMass = mass of grid, in kg
//TotalThrust = stopping thrust, in Newtons
//Speed = speed of grid, in m/s
double RetrofireTime(double Acceleration, double TotalMass, double TotalThrust, double Speed){
	double Downstairs = TotalThrust - Acceleration * TotalMass;
	double Time = 0;
	if(Downstairs != 0){
		Time = (Speed * TotalMass) / Downstairs;
	}
	return Time;
}
