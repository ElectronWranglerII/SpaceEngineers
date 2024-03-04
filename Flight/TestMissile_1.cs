//My magical PID constants
const double proportionalConstant = 5;
const double derivativeConstant = 2;
 
bool driftCompensation = true; 
//set this to false if u want the missiles to drift like ass
 
 
// No touchey below
IMyRemoteControl missileReference = null;
 
bool fireMissile = false;
bool firstGuidance = true;
bool isGPS = false;

Vector3D vector;
Vector3D gps;
 
Vector3D targetPos = new Vector3D();
Vector3D missilePos = new Vector3D();
Vector3D gravVec = new Vector3D();
Vector3D StraightUpPosition;

double ArriveDistance = 100;
 
double lastYawAngle = 0;
double lastPitchAngle = 0;
double lastRollAngle = 0;
 
List<IMyThrust> thrust = new List<IMyThrust>();
List<IMyThrust> forwardThrust = new List<IMyThrust>();
List<IMyThrust> otherThrust = new List<IMyThrust>();
List<IMyGyro> gyros = new List<IMyGyro>();
List<IMyRemoteControl> remotes = new List<IMyRemoteControl>();
List<IMyRadioAntenna> antennas = new List<IMyRadioAntenna>();
List<IMyTimerBlock> timers = new List<IMyTimerBlock>();
List<IMyWarhead> WarheadList;

bool Arrived = false;

Program(){
	vector = new Vector3D();
    gps = new Vector3D();
	WarheadList = new List<IMyWarhead>();
	StraightUpPosition = new Vector3D(-17.04, -12482.01, -63977.31);
}
 
void Main(string arg){
	if(arg.Length > 0){
		isGPS = ParseGPS(arg, out gps);
		if(isGPS){
			Echo("GPS position recieved");
			targetPos = StraightUpPosition;
			if(!fireMissile){
				fireMissile = true;
			}
			Echo(targetPos.ToString());
			Runtime.UpdateFrequency = UpdateFrequency.Update1;
		}
	}

    if(fireMissile){
		if(Arrived){
			Arrived = false;
			firstGuidance = true;
			targetPos = gps;
			foreach(var Warhead in WarheadList){
				//Warhead.ApplyAction("OnOff_On");
				Warhead.IsArmed = true;
			}
		}else{
			bool isSetup = GetBlocks();
	 
			if (isSetup){
				GuideMissile(targetPos);
				//timers[0].ApplyAction("TriggerNow");
			}
		}
    }
}

class GPSEntry{
	private string Name;
	private Vector3D Coords;
	
	public GPSEntry(string GPSString){
		//GPS:Name:X:Y:Z:
		bool Failed = false;
		double X = 0;
		double Y = 0;
		double Z = 0;
		var Temp = GPSString.Split(':');
		if(Temp.Length == 6){
			if(Temp[0] == "GPS"){
				this.Name = Temp[1];
				if(double.TryParse(Temp[2], out X) && double.TryParse(Temp[3], out Y) && double.TryParse(Temp[4], out Z)){
					this.Coords = new Vector3D(X, Y, Z);
				}else{
					Failed = true;
				}
			}else{
				Failed = true;
			}
		}else{
			Failed = true;
		}
		if(Failed){
			throw new ArgumentException("Must be a valid GPS string of the format GPS:Name:X:Y:Z:", "GPSString");
		}
	}
	
	public GPSEntry(string InName, Vector3D InPosition){
		this.Name = InName;
		this.Coords = new Vector3D(InPosition);
	}
	
	public GPSEntry(string InName, double X, double Y, double Z){
		this.Name = InName;
		this.Coords = new Vector3D(X, Y, Z);
	}
	
	public Vector3D Position(){
		return this.Coords;
	}
	
	public void Position(Vector3D InPosition){
		this.Coords = InPosition;
	}
	
	public string ToString(){
		return "GPS:" + this.Name + ":" + this.Coords.X.ToString() + ":" + this.Coords.Y.ToString() + ":" + this.Coords.Z.ToString() + ":";
	}
	
	public double X(){
		return this.Coords.X;
	}
	
	public void X(double InX){
		this.Coords.X = InX;
	}
	
	public double Y(){
		return this.Coords.Y;
	}
	
	public void Y(double InY){
		this.Coords.Y = InY;
	}
	
	public double Z(){
		return this.Coords.Z;
	}
	
	public void Z(double InZ){
		this.Coords.Z = InZ;
	}
}
 
bool ParseGPS(string GPSEntry, out Vector3D Coords){
	//GPS:Enemy Base:-1430.78:-17387.08:-58354.01:#FF75C9F1:
	bool WasSuccessful = false;
	double X = 0;
	double Y = 0;
	double Z = 0;
    var Temp = GPSEntry.Split(':');
	if(Temp.Length == 7){
		if(double.TryParse(Temp[2], out X) && double.TryParse(Temp[3], out Y) && double.TryParse(Temp[4], out Z)){
			WasSuccessful = true;
		}
	}
	Coords = new Vector3D(X, Y, Z);
	return WasSuccessful;
}
 
bool TryParseVector3D(string vectorString, out Vector3D vector){
    vector = new Vector3D(0, 0, 0);
 
    vectorString = vectorString.Replace(" ", "").Replace("{", "").Replace("}", "").Replace("X", "").Replace("Y", "").Replace("Z", "");
    var vectorStringSplit = vectorString.Split(':');
 
    double x, y, z;
 
    if (vectorStringSplit.Length < 4)
        return false;
 
    bool passX = double.TryParse(vectorStringSplit[1], out x);
    bool passY = double.TryParse(vectorStringSplit[2], out y);
    bool passZ = double.TryParse(vectorStringSplit[3], out z);
 
    if (passX && passY && passZ)
    {
        vector = new Vector3D(x, y, z);
        return true;
    }
    else
        return false;
}
 
bool GetBlocks()
{
    bool successfulSetup = true;
 
    forwardThrust.Clear();
    otherThrust.Clear();
 
    GridTerminalSystem.GetBlocksOfType(gyros);
    GridTerminalSystem.GetBlocksOfType(thrust);
    GridTerminalSystem.GetBlocksOfType(remotes);
    GridTerminalSystem.GetBlocksOfType(antennas);
    GridTerminalSystem.GetBlocksOfType(timers);
 
    if (gyros.Count == 0)
    {
        Echo($"Error: No gyros");
        successfulSetup = false;
    }
 
    if (thrust.Count == 0)
    {
        Echo($"Error: No thrust");
        successfulSetup = false;
    }
 
    if (remotes.Count == 0)
    {
        Echo($"Error: No remotes");
        successfulSetup = false;
    }
 
    if (antennas.Count == 0)
    {
        Echo($"Error: No antenna");
        successfulSetup = false;
    }
 
    if (timers.Count == 0)
    {
        Echo($"Error: No timers");
        successfulSetup = false;
    }
 
    if (successfulSetup)
    {
        missileReference = remotes[0];
        GetThrusterOrientation(missileReference, thrust, out forwardThrust, out otherThrust);
 
        
        var myID = Me.EntityId;
        foreach (IMyRadioAntenna thisAntenna in antennas)
        {
            //thisAntenna.SetValue("PBList", myID);
            //thisAntenna.SetValue("Radius", float.MaxValue);
            //thisAntenna.SetValue("EnableBroadCast", true);
            thisAntenna.ApplyAction("OnOff_On");
        }
 
        foreach(IMyThrust thisThrust in otherThrust)
        {
            thisThrust.ApplyAction("OnOff_On");
        }
 
        foreach (IMyThrust thisThrust in forwardThrust)
        {
            thisThrust.ApplyAction("OnOff_On");
            thisThrust.SetValue("Override", float.MaxValue);
        }
        
    }
 
    return successfulSetup;
}
 
void GetThrusterOrientation(IMyRemoteControl refBlock, List<IMyThrust> thrusters, out List<IMyThrust> _forwardThrust, out List<IMyThrust> _otherThrust)
{
    var forwardDirn = refBlock.WorldMatrix.Forward;
 
    _forwardThrust = new List<IMyThrust>();
    _otherThrust = new List<IMyThrust>();
 
    foreach (IMyThrust thisThrust in thrusters)
    {
        var thrustDirn = thisThrust.WorldMatrix.Backward;
        bool sameDirn = thrustDirn == forwardDirn;
 
        if (sameDirn)
        {
            _forwardThrust.Add(thisThrust);
        }
        else
        {
            _otherThrust.Add(thisThrust);
        }
    }
}
 
void GuideMissile(Vector3D targetPos){
    missilePos = missileReference.GetPosition();
 
    //---Get orientation vectors of our missile 
    Vector3D missileFrontVec = missileReference.WorldMatrix.Forward;
    Vector3D missileLeftVec = missileReference.WorldMatrix.Left;
    Vector3D missileUpVec = missileReference.WorldMatrix.Up;
 
    //---Check if we have gravity 
    double rollAngle = 0; double rollSpeed = 0;
 
    var remote = remotes[0] as IMyRemoteControl;
    bool inGravity = false;
 
    gravVec = missileReference.GetNaturalGravity();
    double gravMagSquared = gravVec.LengthSquared();
    if (gravMagSquared != 0)
    {
        if (gravVec.Dot(missileUpVec) < 0)
        {
            rollAngle = Math.PI / 2 - Math.Acos(MathHelper.Clamp(gravVec.Dot(missileLeftVec) / gravVec.Length(), -1, 1));
        }
        else
        {
            rollAngle = Math.PI + Math.Acos(MathHelper.Clamp(gravVec.Dot(missileLeftVec) / gravVec.Length(), -1, 1));
        }
 
        if (firstGuidance) lastRollAngle = rollAngle;
 
        rollSpeed = Math.Round(proportionalConstant * rollAngle + derivativeConstant * (rollAngle - lastRollAngle) * 60, 2);
 
        inGravity = true;
    }
    else
    {
        rollSpeed = 0;
    }
 
    //---Find vector from missile to destinationVec    
    var missileToTargetVec = targetPos - missilePos;
	if(missileToTargetVec.Length() < 100){
		Arrived = true;
		return;
	}
 
    //---Get travel vector 
    var missileVelocityVec = missileReference.GetShipVelocities().LinearVelocity;
 
    //---Calc our new heading based upon our travel vector    
 
    var headingVec = CalculateHeadingVector(missileToTargetVec, missileVelocityVec, driftCompensation);
    
    //---Get pitch and yaw angles 
    double yawAngle; double pitchAngle;
    GetRotationAngles(headingVec, missileFrontVec, missileLeftVec, missileUpVec, out yawAngle, out pitchAngle);
 
    if (firstGuidance)
    {
        lastPitchAngle = pitchAngle;
        lastYawAngle = yawAngle;
        firstGuidance = false;
    }
 
    //---Angle controller
    double yawSpeed = Math.Round(proportionalConstant * yawAngle + derivativeConstant * (yawAngle - lastYawAngle) * 60, 2);
    double pitchSpeed = Math.Round(proportionalConstant * pitchAngle + derivativeConstant * (pitchAngle - lastPitchAngle) * 60, 2);
 
    //---Set appropriate gyro override 
    ApplyGyroOverride(pitchSpeed, yawSpeed, rollSpeed, gyros, missileReference);
 
    //---Store previous values 
    lastYawAngle = yawAngle;
    lastPitchAngle = pitchAngle;
    lastRollAngle = rollAngle;
	
	if(Vector3D.Distance(targetPos, missilePos) <= ArriveDistance){
		fireMissile = false;
		foreach (IMyThrust thisThrust in thrust){
            thisThrust.ApplyAction("OnOff_On");
            thisThrust.SetValue("Override", 0f);
        }
		foreach(var thisGyro in gyros){ 
			thisGyro.GyroOverride = false; 
		} 
	}
 
}
 
Vector3D CalculateHeadingVector(Vector3D targetVec, Vector3D velocityVec, bool driftComp)
{
    if (!driftComp)
    {
        return targetVec;
    }
 
    if (velocityVec.LengthSquared() < 100)
    {
        return targetVec;
    }
 
    if (targetVec.Dot(velocityVec) > 0)
    {
        return VectorReflection(velocityVec, targetVec);
    }
    else
    {
        return -velocityVec;
    }
}
 
Vector3D VectorReflection(Vector3D a, Vector3D b) //reflect a over b    
{
    Vector3D project_a = VectorProjection(a, b);
    Vector3D reject_a = a - project_a;
    return project_a - reject_a;
}
 
/*
/// Whip's Get Rotation Angles Method v9 - 12/1/17 ///
* Fix to solve for zero cases when a vertical target vector is input
* Fixed straight up case
*/
void GetRotationAngles(Vector3D v_target, Vector3D v_front, Vector3D v_left, Vector3D v_up, out double yaw, out double pitch)
{
    //Dependencies: VectorProjection() | VectorAngleBetween()
    var projectTargetUp = VectorProjection(v_target, v_up);
    var projTargetFrontLeft = v_target - projectTargetUp;
 
    yaw = VectorAngleBetween(v_front, projTargetFrontLeft);
 
    if (Vector3D.IsZero(projTargetFrontLeft) && !Vector3D.IsZero(projectTargetUp)) //check for straight up case
        pitch = MathHelper.PiOver2;
    else
        pitch = VectorAngleBetween(v_target, projTargetFrontLeft); //pitch should not exceed 90 degrees by nature of this definition
 
    //---Check if yaw angle is left or right  
    //multiplied by -1 to convert from right hand rule to left hand rule
    yaw = -1 * Math.Sign(v_left.Dot(v_target)) * yaw;
 
    //---Check if pitch angle is up or down    
    pitch = Math.Sign(v_up.Dot(v_target)) * pitch;
 
    //---Check if target vector is pointing opposite the front vector
    if (Math.Abs(yaw) <= 1E-6 && v_target.Dot(v_front) < 0)
    {
        yaw = Math.PI;
    }
}
 
Vector3D VectorProjection(Vector3D a, Vector3D b)
{
    if (Vector3D.IsZero(b))
        return Vector3D.Zero;
 
    return a.Dot(b) / b.LengthSquared() * b;  
}
 
double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
{
    if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
        return 0;
    else
        return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
}
 
//Whip's ApplyGyroOverride Method v9 - 8/19/17
void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list, IMyTerminalBlock reference) 
{ 
    var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed); //because keen does some weird stuff with signs 
    var shipMatrix = reference.WorldMatrix;
    var relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipMatrix); 
 
    foreach (var thisGyro in gyro_list) 
    { 
        var gyroMatrix = thisGyro.WorldMatrix;
        var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(gyroMatrix)); 
 
        thisGyro.Pitch = (float)transformedRotationVec.X;
        thisGyro.Yaw = (float)transformedRotationVec.Y; 
        thisGyro.Roll = (float)transformedRotationVec.Z; 
        thisGyro.GyroOverride = true; 
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
