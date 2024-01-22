/*//////////////////////////
 * Thank you for using:
 * [PAM] - Path Auto Miner
 * ————————————
 * Author:  Keks
 * Last update: 2019-12-20
 * ————————————
 * PamPlus by: SenX
 * Last Update: 30.08.2023
 * ————————————
 * Guide: https://steamcommunity.com/sharedfiles/filedetails/?id=1553126390
 * Script: https://steamcommunity.com/sharedfiles/filedetails/?id=1507646929
 * Youtube: https://youtu.be/ne_i5U2Y8Fk
 * ————————————
 * Please report bugs here: https://steamcommunity.com/sharedfiles/filedetails/?id=2775731229
 * ————————————
 * I hope you enjoy this script and don't forget
 * to leave a comment on the steam workshop
 *
 */ /////////////////////////

/*************************************
 * Updated By SenX
 *  Changed On Damage Default to STOP from Return home
 *
 *  Changed Minimums for:
 *          Uranium: Ignore
 *          Battery: Ignore
 *          Hydrogen: 20%
 *
 *  Disabled Toggle Sorters
 *
 *  Changed Acceleration Default from 70% to 60%
 *  Changed Work speed fwd/bwd max speed max to 40m/s
 *  Changed Work speed stepping from 0.5 to 0.20 for more accurate speed control with separate control for 0.1m/s faster or slower
 *
 *  Added Ore Tally Display feature (update for this pending)
 *
 *  Added Automatic Drill detection, sets forward Work speed and drill radius automatically
 *          DrillRadius override control on programming block custom data, instructions below.
 *          Work speed can be changed through normal menu or Programming Block custom data for custom values. Changes are live.
 *
 *  Slowed Down Grinder
 *  Disabled Grinder Stuck Detector, so grinding mk items wouldn't sent it back thinking it was stuck
 *  Resets Gyros to original settings when grinding stopped or finished, if you set them manually. (doesn't always work)
 *
 *  Adjusted Docking control, faster to dock and slowing shorter of actual connection
 *
 *  Fine tune control your forward speed by 0.1m/s using the run command FASTER or SLOWER
 *  Fine tune control your reverse speed by 0.1m/s using the run command REVFASTER or REVSLOWER
 *
 *  Returns to job start 5 minutes before scheduled server restarts, to prevent afk players from getting stuck in asteroids.
 *  (This is can be edited to work on any server )
 *
 *  Removed PAM-Controller, this function on a server with concealment (most Torch servers use this) has no use. Also required your antenna to broadcast which is
 *  not a good idea on PVP servers.
 *
 *  JumpDrive Monitor - Set minimum jump distance in the programing block custom data.  This will stop the job and return to job start location.
 *  Copy to programming block custom data >> MinJumpDistance=2000
 *  Change 2000 to what ever range you want in KM. Remove the entire line to remove the limit entirely.
 *
 *  WeaponCore Enemy Nearby Detected Warning System
 *      Make a group called OHNO_LIGHTS.  Add any lights to this group. This will be used to indicate an enemy is detected.
 *      Active by default, to enable this set UseWeaponsCore = true
 *      This does require a block weapon.  The range of this system is limited to the largest range of your weapon(s).
 *      Use run command SILENCE to make the alarm stop while enemies are in range.
 *
 * Added support for custom fuel types.
 *
 *************************************/

// Not playing on a server, no problem.  Set below to false and the script wont auto stop mining at
// change, remove, add times appropriate for your server below.
bool SERVER_RESTARTS = false;
// Want PAM to stop mining 5 minutes before your server restarts... set above to true and put the restart
// times in below using HH:mm:ss. Best to make the times 5 minutes before planned restart.
// Below are examples for Upside Down and Stone Industries Servers.
// (If you would like to add your server, leave me a note on Discord. Invite link => https://discord.gg/rSuxGrHrrt)
// { "23:55:00", "05:55:00", "11:55:00", "17:55:00" }; // UpsideDown and Prometheus
// { "03:44:00", "07:44:00", "11:44:00", "15:44:00", "19:44:00", "23:44:00" }; // Stone Industries - They turn off blocks 15 minutes before restart.
static readonly string[] RestartTimes = {
  ""
};

// Disable this if you are not using WeaponsCore on your server or personal game!!!
bool UseWeaponsCore = true;

const string VERSION = "1.3.1";
const string DATAREV = "14";
const string SenX_Version = "1.5.3";

// Custom Fuel Types, some servers use only Uranium, another ingot entirely, or both.  Add all the known ingot types below.
// Always in CAPITALS!!!
List < string > fuelList = new List < string > () {
  "ThUFuelCell",
  "Uranium"
};

const String pamTag = "[PAM]";
//Tag for LCD's of cockpits and other blocks: [PAM:<lcdIndex>] e.g: [PAM:1]

//For Inventory Status your LCD Panel must contain the name below.
string LCDInvPanel = "PAM-Inventory";
//Add :# to display on cockpit screens
// Example:  Industrial Cockpit [PAM:1] PAM-Inventory:1

const int gyroSpeedSmall = 15; //[RPM] small ship
const int gyroSpeedLarge = 5; //[RPM] large ship
const int generalSpeedLimit = 100; //[m/s] 0 = no limit (if set to 0 ignore unreachable code warning)
const double dockingSpeed = 1 f; //[m/s]

//multiplied with ship size
const double dockDist = 1.0 f; //position in front of the home connector
const double followPathDock = 2 f; //stop following path, fly direct to target
const double followPathJob = 1 f; //same as dock
const double useDockDirectionDist = 1 f; //Override way-point direction, use docking dir
const double useJobDirectionDist = 0 f; //same as dock

//other distances
const double wpReachedDist = 2 f; //[m]
double drillRadius = 5.0 f; // You can force this number by putting DrillRadius=true in the custom data. Without it
// PAM will auto-select a number based on your lowest tier drill on the grid.

//This is set automatically depending on which tier drills you have.
//To override this and set the number above (or leave it for vanilla drills) and add DrillRadius=True to the programming block custom data on its own line.

//Other Optional Overrides
//Put these in the programming blocks Custom Data, changes are live so stop pam before adding, removing, or modifying any existing overrides.
//Careful to enter the information exactly as you see below in the correct format.
/*
 *    MaxFwdDrillSpeed=2.0
 *
 *
 */

//grinding
const float sensorRange = 0.3 f; //fly slow when blocks found in this range
const float fastSpeed = 10 f; //speed when no blocks are detected

//minimum acceleration in space before ship becomes too heavy
const float minAccelerationSmall = 0.2 f; //[m/s²] small ship
const float minAccelerationLarge = 0.5 f; //[m/s²] large ship

//stone ejection
const float minEjection = 25 f; //[%] Min amount of ejected cargo to continue job

//LCD
const bool setLCDFontAndSize = true;

//Check if blocks are connected with conveyors
const bool checkConveyorSystem = false; //temporarily disabled because of a SE bug

public String GetElementCode(String itemName) {
  //Here you can define custom element codes for the PAM-Controller
  //You can extend this when you are using mods which adds new materials
  //This is not necessary for any function of PAM, it is just a little detail on the controller screen
  //It only needs to be changed in the controller pb
  switch (itemName) {
  case "IRON":
    return "Fe";
  case "NICKEL":
    return "Ni";
  case "COBALT":
    return "Co";
  case "MAGNESIUM":
    return "Mg";
  case "SILICON":
    return "Si";
  case "SILVER":
    return "Ag";
  case "GOLD":
    return "Au";
  case "PLATINUM":
    return "Pt";
  case "URANIUM":
    return "U";
  case "TITANIUM":
    return "Ti";
  case "THORIUM":
    return "Th";

    //add new entries here

    //example:
    //New material: ExampleOre
    //Element code: Ex

    //case "EXAMPLEORE": return "Ex";

  default:
    return ""; //don't change this!
  }
}
int ऌ = 0;
int ऋ = 5;
bool ऊ = false;
static IMyGridTerminalSystem उ;
static List < IMyTerminalBlock > ई = new List < IMyTerminalBlock > ();
static List < IMyTerminalBlock > इ = new List < IMyTerminalBlock > ();
static Dictionary < string, MyFixedPoint > आ = new Dictionary < string,
  MyFixedPoint > ();
static List < IMyGyro > अ = new List < IMyGyro > ();
static List < float > ऄ = new List < float > ();
static List < IMyShipDrill > ࢬ = new List <
  IMyShipDrill > ();
static List < IMyShipGrinder > ࢫ = new List < IMyShipGrinder > ();
static StringBuilder ࢪ = new StringBuilder();
const string ࢩ =
  " PAM Plus ";
static List < IMyJumpDrive > ࢨ = new List < IMyJumpDrive > ();
static List < IMyLightingBlock > ࢧ = new List < IMyLightingBlock > ();
static
IMyBlockGroup ࢦ;
List < ࢣ > ࢥ = new List < ࢣ > ();
Dictionary < string, double > ࢤ = new Dictionary < string, double > ();
class ࢣ {
  public Color ࢢ;
  public bool
  ࠉ;
  public float ࡄ;
  public float ࡃ;
  public long ࡂ;
  public string ࡁ;
  public void ࡀ(Color ࠨ, bool ࠤ, float ࠚ, float ࠕ, long ࠔ, string ࠓ) {
    ࢢ = ࠨ;
    ࠉ = ࠤ;
    ࡄ = ࠚ;
    ࡃ = ࠕ;
    ࡂ = ࠔ;
    ࡁ = ࠓ;
  }
}
public enum ࠒ {
  ࠑ,
  ࠎ,
  ࠐ,
  ࠏ
}
ࠒ ࠎ = ࠒ.ࠑ;
static إ ࠍ = new إ();
void ࠌ() {
  if (!UseWeaponsCore) return;
  if (ࢥ.Count == 0 && (ࠎ == ࠒ.ࠎ || ࠎ == ࠒ.ࠐ)) {
    foreach(IMyLightingBlock ࠋ in ࢧ) {
      ࢣ ࠊ = new ࢣ();
      ࠊ.ࡀ(ࠋ.Color, ࠋ.Enabled, ࠋ.Radius, ࠋ.Intensity, ࠋ.GetId(), ࠋ.CustomName);
      ࢥ.Add(ࠊ);
    }
  }
  Random ࡅ = new Random();
  foreach(IMyLightingBlock ࡇ in ࢧ) {
    if (ࠎ == ࠒ.ࠎ) {
      if (ࢥ.Count == 0) {
        foreach(
          IMyLightingBlock ࠋ in ࢧ) {
          ࢣ ࠊ = new ࢣ();
          ࠊ.ࡀ(ࠋ.Color, ࠋ.Enabled, ࠋ.Radius, ࠋ.Intensity, ࠋ.GetId(), ࠋ.CustomName);
          ࢥ.Add(ࠊ);
        }
      }
      int ࢠ = ࡅ.Next(0, 3);
      ࡇ.
      Enabled = true;
      ࡇ.Intensity = 10 f;
      ࡇ.Radius = 20 f;
      switch (ࢠ) {
      case 0:
        ࡇ.Color = Color.DarkRed;
        break;
      case 1:
        ࡇ.Color = Color.Yellow;
        break;
      case 2:
        ࡇ.Color = Color.DarkViolet;
        break;
      }
    }
  }
}
void ࡘ() {
  if (ࢬ.Count > 0) {
    ઠ = થ.ણ;
    ܭ = "Miner mode enabled!";
    try {
      string[] ࡗ = Me.CustomData.
      Split(new string[] {
        "\r",
        "\r\n",
        "\n"
      }, StringSplitOptions.RemoveEmptyEntries);
      bool ࡖ = false;
      foreach(string ࡕ in ࡗ) {
        if (ࡕ.ToUpper() == "DRILLRADIUS=TRUE") {
          ࡖ = true;
          break;
        }
      }
      if (ࡖ != true) {
        foreach(IMyShipDrill ࡔ in ࢬ) {
          switch (ࡔ.BlockDefinition.SubtypeId.ToUpper()) {
          case "LARGE_DRILL_TIER3":
            ࡓ(4);
            break;
          case "LARGE_DRILL_TIER2":
            ࡓ(3);
            break;
          case "LARGE_DRILL_TIER1":
            ࡓ(2);
            break;
          case "LARGEBLOCKDRILL":
            ࡓ(1);
            break;
          }
        }
        switch (ऋ) {
        case 4:
          drillRadius = 5.0 f;
          ޜ = 2.0 f;
          break;
        case 3:
          drillRadius = 4.8 f;
          ޜ = 2.30 f;
          break;
        case 2:
          drillRadius =
            2.0 f;
          ޜ = 1.00 f;
          break;
        case 1:
          drillRadius = 2.0 f;
          ޜ = 0.75 f;
          break;
        }
      }
    } catch (Exception e) {
      Echo(e.Message);
    }
  } else if (ࢫ.Count > 0) {
    Echo(ऊ.ToString());
    ઠ = થ.ઢ;
    ܭ = "Grinder mode enabled!";
    ऊ = true;
    ޝ = 0.1 f;
    drillRadius = 2.0 f;
  } else {
    ઠ = થ.ކ;
    ޅ(ދ.ކ);
  }
}
void ࡓ(int ࡒ) {
  if (ࡒ < ऋ) {
    ऋ = ࡒ;
  }
}
Program() {
  उ = GridTerminalSystem;
  try {
    ࢦ = GridTerminalSystem.GetBlockGroupWithName("OHNO_LIGHTS");
    ࢦ.GetBlocksOfType(ࢧ);
  } catch {
    if (
      UseWeaponsCore) Echo("NO GROUP \"OHNO_LIGHTS\"");
  }
  उ.GetBlocksOfType(ࢬ, m => m.CubeGrid == Me.CubeGrid);
  उ.GetBlocksOfType(ࢫ, m => m.CubeGrid == Me
    .CubeGrid);
  Runtime.UpdateFrequency = UpdateFrequency.Update10;
  उ.GetBlocksOfType(ࢨ, m => m.CubeGrid == Me.CubeGrid);
  if (ઠ != થ.ડ) {
    ܭ =
      "Welcome to [PAM Plus]!";
    ڷ ࡑ = ۍ();
    ࡘ();
    if (ઠ == થ.ત) {
      ࡘ();
    }
    if (ࡑ == ڷ.ڶ) ऍ = false;
    if (ࡑ == ڷ.ڿ) ܭ = "Data restore failed!";
    if (ࡑ == ڷ.ǁ) ܭ = "New version, wipe data";
  }
}
Vector3D ࡐ = new Vector3D();
Vector3D ࡏ = new Vector3D();
Vector3D ࡎ = new Vector3D();
Vector3D ࡍ = new Vector3D();
Vector3D ࡌ = new
Vector3D();
DateTime ࡋ = new DateTime();
bool ࡊ = true;
int ࡉ = 0;
bool ࡆ = false;
bool ࡈ = false;
bool ऍ = true;
bool ן = false;
bool ॡ = false;
bool ॠ =
  false;
double य़ = 0;
double फ़ = 0;
int ढ़ = 0;
int ǁ = 0;
int ड़ = 0;
int ज़ = 0;
double ग़ = 0;
double ख़ = 0;
double क़ = 0;
double ॐ = 0;
List < int > ऽ = new List <
  int > ();
List < int > ह = new List < int > ();
Dictionary < MyDetectedEntityInfo, float > स = new Dictionary < MyDetectedEntityInfo, float > ();
void
ष() {
  if (!SERVER_RESTARTS) return;
  string श = DateTime.Now.ToString("HH:mm:ss");
  foreach(string व in RestartTimes) {
    if (श == व) {
      this
        .ఙ();
      this.க();
      ܭ = "Server Restart!";
      break;
    }
  }
}
void Main(string ۂ, UpdateType ऴ) {
  if (UseWeaponsCore) {
    try {
      ࠍ.ՙ(Me);
    } catch {
      Echo(
        "WeaponsCore is not on the system,");
      Echo("set UseWeaponsCore=false in script.");
    }
    switch (ࠎ) {
    case ࠒ.ࠎ:
      Echo("Enemy Detected!");
      break;
    case ࠒ.ࠏ:
      Echo(
        "Alarm Off");
      break;
    case ࠒ.ࠐ:
      Echo("Alarm Silenced");
      break;
    }
    ࠍ.չ(Me, स);
    if ((स).Any(Ǫ => Ǫ.Key.Relationship ==
        MyRelationsBetweenPlayerAndBlock.Enemies)) {
      Echo(स.Count.ToString());
      if (ࠎ != ࠒ.ࠏ) {
        if (ࠎ != ࠒ.ࠐ) {
          foreach(KeyValuePair < MyDetectedEntityInfo, float > ळ in स) {
            if (ळ.Key.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies) {
              ࠎ = ࠒ.ࠎ;
              ࠌ();
              break;
            }
          }
        }
      }
    } else {
      ࠎ = ࠒ.ࠑ;
    }
    स.Clear();
    if (ࢥ.Count > 0 && ࠎ != ࠒ.ࠎ) {
      foreach(ࢣ ࠋ in ࢥ) {
        IMyLightingBlock ঈ = (IMyLightingBlock) GridTerminalSystem.GetBlockWithId(ࠋ.ࡂ);
        ঈ.Color = ࠋ.ࢢ;
        ঈ.Intensity = ࠋ
          .ࡃ;
        ঈ.Radius = ࠋ.ࡄ;
        ঈ.Enabled = ࠋ.ࠉ;
      }
      ࢥ.Clear();
    }
  }
  string[] আ = Me.CustomData.Split(new string[] {
      "\r",
      "\r\n",
      "\n"
    },
    StringSplitOptions.RemoveEmptyEntries);
  foreach(string ࡕ in আ) {
    if (ࡕ.ToUpper().Contains("MINJUMPDISTANCE") && ࢨ.Count > 0) {
      Echo(
        "Current Jump Distance: " + ࢨ[0].MaxJumpDistanceMeters / 1000);
      string[] অ = ࡕ.Split('=');
      Echo("Set Jump Distance: " + double.Parse(অ[1].ToString()));
      if (ࢨ[
          0].MaxJumpDistanceMeters <= (double.Parse(অ[1]) / 1000) && ధ == ఏ.ఋ) {
        this.ఙ();
        this.க();
        ܭ = "Minimum Jump Distance!";
      }
    }
  }
  if (ధ == ఏ.ఋ || (
      ऊ == true && ధ != ఏ.ఎ && ధ != ఏ.ఌ)) {
    ष();
  }
  try {
    if (ऌ == 12) ॷ();
    if (ऌ == 6) {
      string[] ࡗ = Me.CustomData.Split(new string[] {
          "\r",
          "\r\n",
          "\n"
        },
        StringSplitOptions.RemoveEmptyEntries);
      foreach(string ङ in ࡗ) {
        string[] ॿ = ङ.Split('=');
        if (ॿ[0].ToUpper() == "MAXFWDDRILLSPEED") {
          ޜ = double.Parse(ॿ[1]);
        }
      }
    }
  } catch (Exception e) {
    Echo("MaxFwdDrillSPeed parse error.. " + e.Message);
  }
  try {
    string ॾ = ۂ.ToUpper();
    if (ॾ.StartsWith("DRILLRADIUS")) {
      try {
        int ॽ = ॾ.IndexOf(":");
        string ॼ = ॾ.Substring(ॽ, (ॾ.Length - ॽ));
        drillRadius = double.Parse(ॼ);
        ڿ = 2;
        ܭ =
          "Drill Radius Set: " + ॼ;
      } catch (Exception e) {
        Echo("Error with Drill Radius Command");
        Echo(e.Message);
      }
    }
    if (ٿ != null) {
      ఙ();
      ڍ();
      return;
    }
    ࡈ = (ऴ &
      UpdateType.Update10) != 0;
    if (ࡈ) ࡉ++;
    ࡆ = ࡉ >= 10;
    if (ࡆ) ࡉ = 0;
    if (ࡈ) {
      ݎ++;
      if (ݎ > 4) ݎ = 0;
      ड़ = Math.Max(0, 10 - (DateTime.Now - ࡋ).Seconds);
    }
    if (ઠ != થ.ડ) ख(ۂ);
    try {
      int ॻ = Runtime.CurrentInstructionCount;
      double ই = ǹ(ॻ, Runtime.MaxInstructionCount);
      if (ই > 0.90) ܭ = "Max. instructions >90%";
      if (ই > ग़) ग़ = ই;
      if (घ) {
        ऽ.Add(ॻ);
        while (ऽ.Count > 10) ऽ.RemoveAt(0);
        ख़ = 0;
        for (int z = 0; z < ऽ.Count; z++) {
          ख़ += ऽ[z];
        }
        ख़ = ǹ(ǹ(ख़, ऽ.Count), Runtime.MaxInstructionCount);
        double ॹ = Runtime.LastRunTimeMs;
        if (ॠ && ॹ > क़) क़ = ॹ;
        ह.Add((int)(ॹ * 1000 f));
        while (ह.Count > 10) ह.RemoveAt(0);
        ॐ = 0;
        for (int z = 0; z < ह.Count; z++) {
          ॐ += ह[z];
        }
        ॐ = ǹ(ॐ, ह.Count) / 1000 f;
      }
    } catch {
      ग़ = 0;
    }
  } catch (Exception e) {
    ٿ = e;
  }
  if (ऌ > 12) {
    ऌ = 0;
  } else {
    ऌ++;
  }
}
void ॷ() {
  आ.Clear();
  GridTerminalSystem.GetBlocks(ई);
  for (int च = 0; च < ई.Count - 1; च++) {
    IMyTerminalBlock غ = ई[च];
    if (غ.HasInventory) {
      झ(غ.GetInventory(0));
    }
  }
  ॶ();
}
void ॶ
  () {
    int ॵ = 0;
    IMyTextSurface ॴ;
    ॴ = ((IMyTextSurfaceProvider) Me).GetSurface(ॵ);
    GridTerminalSystem.SearchBlocksOfName(
      LCDInvPanel, इ);
    if (इ.Count != 0) {
      StringBuilder ॳ = ऎ(आ);
      foreach(IMyTerminalBlock ॲ in इ) {
        if (ॲ is IMyTextSurface) {
          ॴ = (IMyTextSurface) ॲ;
        } else if (ॲ is IMyTextSurfaceProvider) {
          string[] ॱ = ॲ.CustomName.Split(new string[] {
            " "
          }, StringSplitOptions.RemoveEmptyEntries);
          foreach(string ल in ॱ) {
            string[] ठ = ल.Split(':');
            if (ठ[0] == LCDInvPanel) {
              ॵ = int.Parse(ठ[1]);
            }
          }
          ॴ = ((IMyTextSurfaceProvider) ॲ).
          GetSurface(ॵ);
        }
        ॴ.ContentType = ContentType.TEXT_AND_IMAGE;
        ॴ.WriteText("", false);
        ॴ.FontSize = 1.2 f;
        ॴ.Font = "Monospace";
        ॴ.WriteText(ॳ,
          false);
        ॳ.Clear();
      }
    }
  }
StringBuilder ऎ(Dictionary < string, MyFixedPoint > ञ) {
  ࢪ.Clear();
  ࢪ.AppendLine("░░░░░░░░░░░░░░░░░░░░░");
  ࢪ.
  AppendLine("░░  Mining  Tally  ░░");
  ࢪ.AppendLine("");
  if (आ.Count > 0) {
    foreach(KeyValuePair < string, MyFixedPoint > ङ in ञ) {
      if ((decimal) ङ.Value > 999 && (decimal) ङ.Value < 1000000) {
        ࢪ.Append(" ≡ ");
        ࢪ.Append(ङ.Key);
        ࢪ.Append(": ");
        ࢪ.Append(Math.Round((decimal) ङ.Value / 1000, 2).ToString());
        ࢪ.AppendLine(" K");
      } else if ((decimal) ङ.Value > 999999 && (decimal) ङ.Value < 1000000000) {
        ࢪ.Append(" ≡ ");
        ࢪ.
        Append(ङ.Key);
        ࢪ.Append(": ");
        ࢪ.Append(Math.Round((decimal) ङ.Value / 1000000, 2).ToString());
        ࢪ.AppendLine(" Mil");
      } else if ((
          decimal) ङ.Value > 999999999) {
        ࢪ.Append(" ≡ ");
        ࢪ.Append(ङ.Key);
        ࢪ.Append(": ");
        ࢪ.Append(Math.Round((decimal) ङ.Value / 1000000000, 2).ToString());
        ࢪ.AppendLine(" Bil");
      } else if ((decimal) ङ.Value < 1) {} else {
        ࢪ.Append(" ≡ ");
        ࢪ.Append(ङ.Key);
        ࢪ.Append(": ");
        ࢪ.AppendLine(
          Math.Round((decimal) ङ.Value, 2).ToString());
      }
    }
    ࢪ.AppendLine();
    ࢪ.Append("░░    ");
    ࢪ.Append(ࢩ);
    ࢪ.AppendLine("   ░░");
    ࢪ.Append(
      "░░░░░░░░░░░░░░░░░░░░░");
  } else {
    ࢪ.AppendLine();
    ࢪ.AppendLine(" EH!,  you broke!!");
    ࢪ.AppendLine();
    ࢪ.AppendLine(" Go mine something");
    ࢪ.AppendLine();
    ࢪ.AppendLine();
    ࢪ.AppendLine();
    ࢪ.AppendLine();
    ࢪ.AppendLine("░       ");
    ࢪ.Append(ࢩ);
    ࢪ.Append("       ░░");
    ࢪ.AppendLine(
      "░░░░░░░░░░░░░░░░░░░░░");
  }
  return ࢪ;
}
void झ(IMyInventory ज) {
  List < MyInventoryItem > छ = new List < MyInventoryItem > ();
  ज.GetItems(छ);
  for (int च = 0; च < छ.Count; च++) {
    var ङ = छ[च];
    if (ङ.Amount > 0) {
      if (ङ.Type.ToString().Contains("Ore")) {
        if (आ.ContainsKey(ङ.Type.SubtypeId.ToString())) {
          आ[ङ
            .Type.SubtypeId.ToString()] += ङ.Amount;
          continue;
        }
        आ.Add(ङ.Type.SubtypeId.ToString(), ङ.Amount);
      }
    }
  }
}
bool घ = false;
bool ग = false;
void ख(string ۂ) {
  bool क = false;
  String औ = "";
  if (ڿ <= 1) ऒ(ۂ);
  bool ओ = ॠ && ஷ == ஒ.ஐ && !ॡ && !क && ढ़ == 0 && !ଭ;
  if (ࡆ && ஷ != ஒ.ஐ) क = true;
  if ((ࡈ && !ओ) ||
    (ࡆ && ओ)) {
    if (ढ़ == 0 && (ड़ <= 0 || ࡊ)) {
      ן = ڿ > 0;
      ڿ = 0;
      ढ़ = 1;
      ڃ();
      ਗ਼();
      ڂ("Scan 1");
    } else if (ढ़ == 1) {
      ढ़ = 2;
      ڃ();
      ҍ();
      ڂ("Scan 2");
    } else if (ढ़ == 2) {
      ढ़ = 0;
      ڃ();
      ј();
      ڂ("Scan 3");
      ࡋ = DateTime.Now;
      if (ڿ <= 1 && ऍ) ࡐ = ۏ(ਸ਼, ਸ਼.CenterOfMass);
      ऍ = false;
      if (ࡊ) {
        ࡊ = false;
        ఙ();
      }
      if (ן && ڿ == 0) ܭ =
        "Setup complete";
    } else {
      if (ధ == ఏ.ఋ && ઠ != થ.ކ) {
        ڃ();
        Ҫ();
        ڂ("Inv balance");
      }
      ڃ();
      switch (ǁ) {
      case 0:
        Տ();
        break;
      case 1:
        Ց();
        break;
      case 2:
        Ռ();
        break;
      case 3:
        ԛ();
        break;
      case 4:
        ԟ();
        break;
      case 5:
        ӌ();
        break;
      case 6:
        Ɇ(ਸ਼);
        break;
      }
      ڂ("Update: " + ǁ);
      ǁ++;
      if (ǁ > 6) {
        ǁ = 0;
        ॠ = true;
        if (ڸ != ఏ.ఎ) {
          switch (ڸ) {
          case ఏ.ఔ:
            ఘ();
            break;
          case ఏ.ఆ:
            ఘ();
            break;
          case ఏ.ఋ:
            ఘ();
            break;
          case ఏ.ఈ:
            ஓ();
            break;
          case ఏ.ఇ:
            க();
            break;
          }
          ڸ = ఏ.ఎ;
        }
      }
    }
    if (!ࡊ) {
      if (!
        ԡ(ਸ਼, true)) {
        ਸ਼ = null;
        ऍ = true;
        ڿ = 2;
      }
      if (ڿ >= 2 && ஷ != ஒ.ஐ) ఙ();
      if (ڿ <= 1) {
        फ़ = ਸ਼.CalculateShipMass().PhysicalMass;
        य़ = (double) ਸ਼.GetShipSpeed();
        ࡏ = ۑ(ਸ਼, ࡐ);
        ࡎ = ਸ਼.WorldMatrix.Forward;
        ࡍ = ਸ਼.WorldMatrix.Left;
        ࡌ = ਸ਼.WorldMatrix.Down;
        ଧ();
        if (ஷ != ஒ.ஐ) {
          ॡ = false;
          g(false);
          Ɯ(false);
          String ƶ = ݻ(ஷ) + " " + (int) ஷ;
          ڃ();
          ਐ();
          બ(false);
          ڂ(ƶ);
          ڃ();
          Ȅ();
          ڂ("Thruster");
          ڃ();
          ڡ();
          ڂ("Gyroscope");
        } else {
          if (ॡ) {
            if (ڇ()) {
              ڴ(ࡌ, ࡎ, ࡍ, 0.25 f,
                true);
              ڡ();
              ܭ = "Aligning to planet: " + Math.Round(ڥ - 0.25 f, 2) + "°";
              if (ک) म(true, true);
            } else म(true, true);
          }
        }
        ग = false;
      }
    }
  }
  ڃ();
  ژ();
  ڂ(
    "Print");
  if (क || ज़ <= 0) {
    ڃ();
    ע(औ);
    ज़ = 4;
  } else if (ࡆ) ज़--;
}
void ऒ(string ۂ) {
  if (ۂ == "") return;
  var ǵ = ۂ.ToUpper().Split(' ');
  ǵ.
  DefaultIfEmpty("");
  var ऑ = ǵ.ElementAtOrDefault(0);
  var ऐ = ǵ.ElementAtOrDefault(1);
  var ए = ǵ.ElementAtOrDefault(2);
  var ट = ǵ.
  ElementAtOrDefault(3);
  String ड = "Invalid argument: " + ۂ;
  bool र = false;
  switch (ऑ) {
  case "UP":
    this.ݦ(false);
    break;
  case "DOWN":
    this.ݤ(false);
    break;
  case "UPLOOP":
    this.ݦ(true);
    break;
  case "DOWNLOOP":
    this.ݤ(true);
    break;
  case "APPLY":
    this.ܣ(true);
    break;
  case "MRES":
    ތ = 0;
    break;
  case "STOP":
    this.ఙ();
    break;
  case "PATHHOME": {
    this.ఙ();
    this.ଯ();
  }
  break;
  case "PATH": {
    this.ఙ();
    this.ଯ();
    ଷ.ଣ = true;
  }
  break;
  case "START": {
    this
      .ఙ();
    ట();
  }
  break;
  case "ALIGN": {
    డ();
    म(!ॡ, false);
  }
  break;
  case "CONT": {
    డ();
    this.ఙ();
    this.ఘ();
  }
  break;
  case "JOBPOS": {
    డ();
    this.ఙ();
    this.க();
  }
  break;
  case "HOMEPOS": {
    డ();
    this.ఙ();
    this.ஓ();
  }
  break;
  case "FULL": {
    డ();
    ƫ = true;
  }
  break;
  case "RESET": {
    ڹ = true;
    ڿ = 2;
  }
  break;
  case "SILENCE": {
    ࠎ = ࠒ.ࠐ;
    break;
  }
  default:
    र = true;
    break;
  }
  if (ઠ != થ.ކ) {
    switch (ऑ.ToUpper()) {
    case "SHUTTLE": {
      य();
    }
    break;
    case "CFGS": {
      if (!ब(ऐ, ए, ट)) ܭ = ड;
    }
    break;
    case "CFGB": {
      if (!द(ऐ, ए)) ܭ = ड;
    }
    break;
    case "CFGL": {
      if (!ޑ(ref ޏ, true, ޘ.d, ऐ, "") || !ध(ए)) ܭ = ड;
    }
    break;
    case "CFGE": {
      if (!ޑ(ref ޟ, true, ޘ.ޔ, ऐ, "IG") || !ޑ(ref ޠ, true, ޘ.ޕ, ए, "IG") || !ޑ(ref ޞ, true, ޘ.ޓ, ट, "IG")) ܭ = ड;
    }
    break;
    case "CFGA": {
      if (!ޑ(ref ޝ,
          false, ޘ.ǵ, ऐ, "")) ܭ = ड;
    }
    break;
    case "CFGW": {
      if (!ޑ(ref ޜ, false, ޘ.ޗ, ऐ, "") || !ޑ(ref ޛ, false, ޘ.ޗ, ए, "")) ܭ = ड;
    }
    break;
    case "NEXT": {
      ޱ(false);
    }
    break;
    case "PREV": {
      ޱ(true);
    }
    break;
    case "FASTER": {
      ޑ(ref ޜ, 0.1 f, ޘ.ޗ, false);
      ܭ = "Fwd speed is now " + ޜ + "m/s";
      break;
    }
    case "SLOWER": {
      ޑ(ref ޜ, -0.1 f, ޘ.ޗ, false);
      ܭ = "Fwd speed is now " + ޜ + "m/s";
      break;
    }
    case "REVFASTER": {
      ޑ(ref ޛ, 0.10 f, ޘ.ޗ, false);
      ܭ =
        "Reverse speed now " + ޛ + "m/s";
      break;
    }
    case "REVSLOWER": {
      ޑ(ref ޛ, -0.10 f, ޘ.ޗ, false);
      ܭ = "Reverse speed now " + ޛ + "m/s";
      break;
    }
    case "SILENCE": {
      ࠎ = ࠒ.ࠐ;
      break;
    }
    default:
      if (र) ܭ = ड;
      break;
    }
  } else {
    switch (ऑ) {
    case "UNDOCK": {
      ग = true;
    }
    break;
    default:
      if (र) ܭ = ड;
      break;
    }
  }
}
String ऱ() {
  String ƶ =
    "\n\n" + "Run-arguments: (Type without:[ ])\n" + "———————————————\n" + "[UP] Menu navigation up\n" + "[DOWN] Menu navigation down\n" +
    "[APPLY] Apply menu point\n\n" + "[UPLOOP] \"UP\" + looping\n" + "[DOWNLOOP] \"DOWN\" + looping\n" + "[PATHHOME] Record path, set home\n" +
    "[PATH] Record path, use old home\n" + "[START] Start job\n" + "[STOP] Stop every process\n" + "[CONT] Continue last job\n" + "[JOBPOS] Move to job position\n" +
    "[HOMEPOS] Move to home position\n" + "[FULL] Simulate ship is full\n" + "[ALIGN] Align the ship to planet\n" + "[RESET] Reset all data\n" +
    "[FASTER] Increases speed by 0.1m/s\n" + "[SLOWER] Decreases speed by 0.1m/s\n\n" + "[ALARMOFF] Turns off any active alarm\n";
  if (ઠ != થ.ކ) ƶ +=
    "[SHUTTLE] Enable shuttle mode\n" + "[NEXT] Next hole\n" + "[PREV] Previous hole\n\n" + "[CFGS width height depth]*\n" + "[CFGB done damage]*\n" +
    "[CFGL maxload weightLimit]*\n" + "[CFGE minUr minBat minHyd]*\n" + "[CFGW forward backward]*\n" + "[CFGA acceleration]*\n" + "———————————————\n" +
    "*[CFGS] = Config Size:\n" + " e.g.: \"CFGS 5 3 20\"\n\n" + "*[CFGB] = Config Behaviour:\n" + " When done: [HOME,STOP]\n" +
    " On Damage: [HOME,JOB,STOP,IG]\n" + " e.g.: \"CFGB HOME IG\"\n\n" + "*[CFGL] = Config max load:\n" + " maxload: [10..95]\n" + " " + "weight limit: [On/Off]\n" +
    " e.g.: \"CFGL 70 on\"\n\n" + "*[CFGE] = Config energy:\n" + " minUr (Uranium): [1..25, IG]\n" + " minBat (Battery): [5..30, IG]\n" +
    " minHyd (Hydrogen): [10..90, IG]\n" + " e.g.: \"CFGE 20 10 IG\"\n\n" + "*[CFGW] = Config work speed:\n" + " fwd: [0.1..4]\n" + " bwd: [0.1..4]\n" +
    " e.g.: \"CFGW 1.5 2\"\n\n" + "*[CFGA] = Config acceleration:\n" + " acceleration: [10..100]\n" + " e.g.: \"CFGA 80\"\n";
  else ƶ +=
    "[UNDOCK] Leave current connector\n\n";
  return ƶ;
}
void य() {
  ఙ();
  ઠ = થ.ކ;
  ޅ(ދ.ކ);
  ا.ଣ = false;
  ଷ.ଣ = false;
  ਵ = null;
  ਯ.Clear();
  ధ = ఏ.ఎ;
}
void म(bool न, bool भ) {
  if (!न) ܭ =
    "Aligning canceled";
  if (भ) ܭ = "Aligning done";
  if (भ || !न) {
    ॡ = false;
    ڵ();
    ɣ(false, 0, 0, 0, 0);
    return;
  }
  if (ڇ()) ॡ = true;
}
bool ब(String फ, String प, String ऩ) {
  bool न = ధ == ఏ.ఋ;
  int Ǳ, ǰ, ܔ;
  if (int.TryParse(फ, out Ǳ) && int.TryParse(प, out ǰ) && int.TryParse(ऩ, out ܔ)) {
    this.ఙ();
    ߦ = Ǳ;
    ߥ = ǰ;
    ߤ = ܔ;
    ߗ(
      false);
    ప(false, false);
    if (न) ఘ();
    return true;
  }
  return false;
}
bool ध(String ޗ) {
  if (ޗ == "ON") {
    ߢ = true;
    return true;
  }
  if (ޗ == "OFF") {
    ߢ =
      false;
    return true;
  }
  return false;
}
bool द(String ܔ, String थ) {
  bool ƹ = true;
  if (ܔ == "HOME") ߨ = true;
  else if (ܔ == "STOP") ߨ = false;
  else ƹ =
    false;
  if (थ == "HOME") ߩ = ߝ.ߞ;
  else if (थ == "STOP") ߩ = ߝ.ࠇ;
  else if (थ == "JOB") ߩ = ߝ.ࠆ;
  else if (थ == "IG") ߩ = ߝ.ࠅ;
  else ƹ = false;
  return ƹ;
}
public
enum त {
  ण,
  ढ,
  ࠈ,
  ݸ,
  ݑ,
  ݷ,
  ݶ,
  ݵ,
  ݴ,
  ݳ,
  ݲ,
  ݱ,
  Ծ,
  ݰ,
  ݯ,
  ݮ,
  ݭ,
  ݬ,
  ݫ
}
int[] ݪ = new int[Enum.GetValues(त.Ծ.GetType()).Length];
bool ݩ = false;
void ݨ(त ݧ) {
  ݪ
    [(int) ލ] = ތ;
  ތ = ݪ[(int) ݧ];
  if (ݧ == त.ݲ) ތ = 0;
  ލ = ݧ;
  if (ઠ != થ.ડ) ǘ(ލ == त.ࠈ, false, 0, 0);
  ݩ = true;
}
void ݦ(bool ݥ) {
  if (ތ > 0) ތ--;
  else if (ݥ) ތ = ݹ - 1;
}
void ݤ(bool ݥ) {
  if (ތ < ݹ - 1) ތ++;
  else if (ݥ) ތ = 0;
}
int ݹ = 0;
int ތ = 0;
त ލ = त.ण;
public enum ދ {
  ފ,
  މ,
  ވ,
  އ,
  ކ
}
String ޅ(ދ ן) {
  switch (ן) {
  case
  ދ.ފ:
    ܭ = "Job is running ";
    break;
  case ދ.މ:
    ܭ = "Connector not ready!";
    break;
  case ދ.ވ:
    ܭ = "Ship modified, path outdated!";
    break;
  case ދ.އ:
    ܭ = "Interrupted by player!";
    break;
  case ދ.ކ:
    ܭ = "Shuttle mode enabled!";
    break;
  }
  return "";
}
String ރ(ߑ ބ) {
  switch (ބ) {
  case ߑ
  .ߐ:
    return "Top-Left";
  case ߑ.ߏ:
    return "Center";
  default:
    return "";
  }
}
String ރ(ߕ ނ) {
  switch (ނ) {
  case ߕ.ߓ:
    return "Auto" + (ઠ == થ.ણ ?
      " (Ore)" : "");
  case ߕ.ߒ:
    return "Auto (+Stone)";
  case ߕ.ߔ:
    return "Default";
  default:
    return "";
  }
}
String ށ(ఏ ݺ) {
  switch (ݺ) {
  case ఏ.ఎ:
    return "No job";
  case ఏ.ఌ:
    return "Job paused";
  case ఏ.ఋ:
    return "Job active";
  case ఏ.ఔ:
    return "Job active";
  case ఏ.ఆ:
    return "Job active";
  case ఏ.
  ఊ:
    return "Job done";
  case ఏ.ఉ:
    return "Job changed";
  case ఏ.ఈ:
    return "Move home";
  case ఏ.ఇ:
    return "Move to job";
  }
  return "";
}
String
ހ(ߝ ݿ) {
  switch (ݿ) {
  case ߝ.ߞ:
    return "Return home";
  case ߝ.ࠆ:
    return "Fly to job pos";
  case ߝ.ࠇ:
    return "Stop";
  case ߝ.ࠅ:
    return "Ignore";
  }
  return "";
}
String ݾ(ࠄ ܒ) {
  switch (ܒ) {
  case ࠄ.ۀ:
    return "Off";
  case ࠄ.ࠁ:
    return "Drop pos (Stone) ";
  case ࠄ.ࠀ:
    return "Drop pos (Sto.+Ice)";
  case ࠄ.ࠃ:
    return "Cur. pos (Stone)";
  case ࠄ.ࠂ:
    return "Cur. pos (Sto.+Ice)";
  case ࠄ.ߺ:
    return "In motion (Stone)";
  case ࠄ.ߵ:
    return "In motion (Sto.+Ice)";
  }
  return "";
}
String ݽ(ન ݼ) {
  switch (ݼ) {
  case ન.ۀ:
    return "No batteries";
  case ન.o:
    return "Charging";
  case ન
  .દ:
    return "Discharging";
  }
  return "";
}
String ݻ(ஒ ݺ) {
  String ƶ = ઠ == થ.ކ ? "target" : "job";
  switch (ݺ) {
  case ஒ.ஐ:
    return "Idle";
  case ஒ.ஏ:
    return "Flying to XY position";
  case ஒ.எ:
    return ઠ == થ.ઢ ? "Grinding" : "Mining";
  case ஒ.ஊ:
    return "Returning";
  case ஒ.అ:
    return "Flying to drop pos";
  case ஒ.உ:
    return "Returning to dock";
  case ஒ.ஈ:
    return "Flying to dock area";
  case ஒ.இ:
    return "Flying to job area";
  case ஒ.ଡ଼:
    return "Flying to path";
  case ஒ.ஆ:
    return "Flying to job position";
  case ஒ.அ:
    return "Approaching dock";
  case ஒ.ஃ:
    return "Docking";
  case
  ஒ.ୱ:
    return "Aligning";
  case ஒ.ୡ:
    return "Aligning";
  case ஒ.ୠ:
    return "Retry docking";
  case ஒ.ୟ:
    return "Unloading";
  case ஒ.ஸ:
    return
    Ʋ;
  case ஒ.ଢ଼:
    return "Undocking";
  case ஒ.ଽ:
    return "Charging batteries";
  case ஒ.ஔ:
    return "Waiting for fuel";
  case ஒ.ହ:
    return "Filling up hydrogen";
  case ஒ.ங:
    return "Waiting for ejection";
  case ஒ.ௐ:
    return "Waiting for ejection";
  case ஒ.ஹ:
    return "Flying to drop pos";
  }
  return "";
}
String ݣ(Կ Բ) {
  switch (Բ) {
  case Կ.Ծ:
    return "On \"Undock\" command";
  case Կ.Խ:
    return "On player entered cockpit";
  case Կ.Ի:
    return "On ship is full";
  case Կ.Ժ:
    return "On ship is empty";
  case Կ.Լ:
    return "On time delay";
  case Կ.Ը:
    return "On batteries empty(<25%)";
  case Կ.Է:
    return "On batteries empty(=0%)";
  case Կ.Թ:
    return "On batteries full";
  case Կ.Ե:
    return "On hydrogen empty(<25%)";
  case Կ.Դ:
    return "On hydrogen empty(=0%)";
  case Կ.Զ:
    return "On hydrogen full";
  }
  return "";
}
int ݎ = 0;
int ݍ = 0;
int ܯ = 0;
int ܮ = 0;
String
ܭ = "";
bool ܬ(ref String ƹ, int ܫ, int ܪ, bool ܢ, String u) {
  ݹ += 1;
  if (ܫ == ܪ) u = ">" + u + (ݎ >= 2 ? " ." : "");
  else u = " " + u;
  ƹ += u + "\n";
  return ܫ ==
    ܪ && ܢ;
}
int ܩ = 0;
int ܨ = 0;
int ܧ = 0;
int ܦ = 0;
int ܥ = 0;
int ܤ = 0;
String ܣ(bool ܢ) {
  int ܡ = 0;
  int ܠ = ތ;
  ݹ = 0;
  String ݏ = "———————————————\n";
  String ݐ = "--------------------------------------------\n";
  String ݡ = "";
  ݡ += ށ(ధ) + " | " + (ଷ.ଣ ? "Ready to dock" : "No dock") + "\n";
  ݡ += ݏ;
  double ݢ = Math.Max(Math.Round(ম), 0);
  if (ލ == त.ण) {
    bool ƶ = ઠ == થ.ކ;
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Record path & set home")) ଯ();
    if (ઠ == થ.ણ)
      if (ܬ(
          ref ݡ, ܠ, ܡ++, ܢ, " Setup mining job")) ݨ(त.ࠈ);
    if (ઠ == થ.ઢ)
      if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Setup grinding job")) ݨ(त.ࠈ);
    if (ઠ == થ.ކ)
      if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Setup shuttle job")) ݨ(त.ݬ);
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Continue job")) ఘ();
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Fly to home position")) ஓ();
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Fly to job position")) க();
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Behavior settings"))
      if (ƶ) ݨ(त.ݭ);
      else ݨ(त.ݷ);
    if (ܬ(
        ref ݡ, ܠ, ܡ++, ܢ, " Info")) ݨ(त.ݵ);
    if (ઠ != થ.ކ)
      if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Help")) ݨ(त.ݳ);
  } else if (ލ == त.ࠈ) {
    double ݠ = Math.Round(ߦ * ઐ, 1);
    double ݟ = Math.Round(ߥ * એ, 1);
    String ݜ = "";
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Start new job!")) ట();
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Change current job")) {
      ప(
        false, false);
      ݨ(त.ण);
    }
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Width + (Width: " + ߦ + " = " + ݠ + "m)")) {
      Ն(ref ߦ, 5, 20, 1);
      ߗ(true);
    }
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ,
        " Width -")) {
      Ն(ref ߦ, -5, 20, -1);
      ߗ(true);
    }
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Height + (Height: " + ߥ + " = " + ݟ + "m)")) {
      Ն(ref ߥ, 5, 20, 1);
      ߗ(true);
    }
    if (ܬ(
        ref ݜ, ܠ, ܡ++, ܢ, " Height -")) {
      Ն(ref ߥ, -5, 20, -1);
      ߗ(true);
    }
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Depth + (" + (ߣ == ߕ.ߔ ? "Depth" : "Min") + ": " + ߤ + "m)")) {
      Ն(ref ߤ, 5, 50, 2);
      ߗ(true);
    }
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Depth -")) {
      Ն(ref ߤ, -5, 50, -2);
      ߗ(true);
    }
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Depth mode: " + ރ(ߣ))) {
      ߣ = ޤ(ߣ);
    }
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Start pos: " + ރ(ߧ))) {
      ߧ = ޤ(ߧ);
    }
    if (ઠ == થ.ઢ && ߣ == ߕ.ߒ) ߣ = ޤ(ߣ);
    ݡ += ז(8, ݜ, ܠ, ref ܥ);
  } else if (ލ == त.ݬ) {
    double[] ݒ = new double[] {
      0,
      3,
      10,
      30,
      60,
      300,
      600,
      1200,
      1800
    };
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Next")) {
      ݨ(त.ݫ);
    }
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Back")) {
      ݨ(त.ण);
    }
    ݡ += " Leave connector 1:\n";
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " - " + ݣ(ߴ.Բ))) ߴ.Բ = ޤ(ߴ.Բ);
    if (!ߴ.ǀ()) ݡ += "\n";
    else if (ܬ(ref ݡ, ܠ, ܡ++,
        ܢ, " - Delay: " + ט((int) ߴ.Ա))) ߴ.Ա = ݔ(ߴ.Ա, ݒ);
    ݡ += " Leave connector 2:\n";
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " - " + ݣ(ߪ.Բ))) ߪ.Բ = ޤ(ߪ.Բ);
    if (!ߪ.ǀ()) ݡ += "\n";
    else if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " - Delay: " + ט((int) ߪ.Ա))) ߪ.Ա = ݔ(ߪ.Ա, ݒ);
  } else if (ލ == त.ݫ) {
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Start job!")) ట();
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Back")) {
      ݨ(त.ݬ);
    }
    ݡ += " Timer: \"Docking connector 1\":\n";
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " = " + (ߴ.Ԧ != "" ? ߴ.Ԧ :
        "-"))) ߴ.Ԧ = ޡ(ref ܩ);
    ݡ += " Timer: \"Leaving connector 1\":\n";
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " = " + (ߴ.ԥ != "" ? ߴ.ԥ : "-"))) ߴ.ԥ = ޡ(ref ܧ);
    ݡ +=
      " Timer: \"Docking connector 2\":\n";
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " = " + (ߪ.Ԧ != "" ? ߪ.Ԧ : "-"))) ߪ.Ԧ = ޡ(ref ܨ);
    ݡ += " Timer: \"Leaving connector 2\":\n";
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ,
        " = " + (ߪ.ԥ != "" ? ߪ.ԥ : "-"))) ߪ.ԥ = ޡ(ref ܦ);
  } else if (ލ == त.ݸ) {
    String ݞ = ޏ + " %";
    if (ࡆ) ݍ++;
    if (ݍ > 1) {
      ݍ = 0;
      ܯ++;
      if (ܯ > 1) ܯ = 0;
      bool[] ݝ = new bool[] {
        ਭ.Count == 0, Ս == ન.ۀ, ਫ.Count == 0, ࢨ.Count == 0
      };
      int Ƙ = 0;
      while (true) {
        Ƙ++;
        ܮ++;
        if (ܮ > ݝ.Length - 1) ܮ = 0;
        if (Ƙ >= ݝ.Length) break;
        if (!ݝ[ܮ])
          break;
      }
    }
    bool ƶ = ઠ == થ.ކ;
    if (!ƶ && ߢ && Ƭ != -1 && ܯ == 0) ݞ = Ƭ < 1000000 ? Math.Round(Ƭ) + " Kg" : Math.Round(Ƭ / 1000) + " t";
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ,
        " Stop!")) {
      ఙ();
      ݨ(त.ण);
    }
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Behavior settings"))
      if (!ƶ) ݨ(त.ݷ);
      else ݨ(त.ݭ);
    if (!ƶ) {
      if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Next hole")) ޱ(false);
    } else if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Undock")) ग = true;
    ݡ += ݐ;
    if (!ƶ) ݡ += "Progress: " + Math.Round(ణ, 1) + " %\n";
    ݡ += "State: " + ݻ(ஷ) +
      " " + ݢ + "m \n";
    ݡ += "Load: " + Մ + " % Max: " + ݞ + " \n";
    if (ܮ == 0) ݡ += "Reactor Fuel: " + (ਭ.Count == 0 ? "No reactors" : Math.Round(ԝ, 1) + "Kg " + (
      ޟ == -1 ? "" : " Min: " + ޟ + " Kg")) + "\n";
    if (ܮ == 1) ݡ += "Battery: " + (Ս == ન.ۀ ? ݽ(Ս) : Վ + "% " + (ޠ == -1 || ƶ ? "" : " Min: " + ޠ + " %")) + "\n";
    if (ܮ == 2) ݡ += "Hydrogen: " + (ਫ.Count == 0 ? "No tanks" : Math.Round(Ӈ, 1) + "% " + (ޞ == -1 || ƶ ? "" : " Min: " + ޞ + " %")) + "\n";
    if (ܮ == 3) ݡ += "Jump Range: " + (
      ࢨ[0].MaxJumpDistanceMeters / 1000).ToString("N0") + " KM\n";
  } else if (ލ == त.ݷ) {
    String ݜ = "";
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Back")) {
      if (ధ == ఏ
        .ఋ) ݨ(त.ݸ);
      else ݨ(त.ण);
    }
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Max load: " + ޏ + "%")) ޑ(ref ޏ, ޏ <= 80 ? -10 : -5, ޘ.d, false);
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ,
        " Weight limit: " + (ߢ ? "On" : "Off"))) ߢ = !ߢ;
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Ejection: " + ݾ(ߎ))) {
      ߎ = ޤ(ߎ);
    }
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Toggle sorters: " + (ߟ ? "On" :
        "Off"))) {
      ߟ = !ߟ;
      if (ߟ) r(s);
    }
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Unload ice: " + (ߍ ? "On" : "Off"))) ߍ = !ߍ;
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Reactor Fuel: " + (ޟ == -1 ?
        "Ignore" : "Min " + ޟ))) ޑ(ref ޟ, (ޟ > 5 ? -5 : -1), ޘ.ޔ, true);
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Battery: " + (ޠ == -1 ? "Ignore" : "Min " + ޠ + "%"))) ޑ(ref ޠ, -5, ޘ.ޕ,
      true);
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Hydrogen: " + (ޞ == -1 ? "Ignore" : "Min " + ޞ + "%"))) ޑ(ref ޞ, -10, ޘ.ޓ, true);
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ,
        " When done: " + (ߨ ? "Return home" : "Stop"))) ߨ = !ߨ;
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " On damage: " + ހ(ߩ))) {
      ߩ = ޤ(ߩ);
    }
    if (ܬ(ref ݜ, ܠ, ܡ++, ܢ, " Advanced...")) ݨ(त.ݶ);
    ݡ += ז(8, ݜ, ܠ, ref ܤ);
  } else if (ލ == त.ݶ) {
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Back")) {
      if (ధ == ఏ.ఋ) ݨ(त.ݸ);
      else ݨ(त.ण);
    }
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, (ઠ == થ
        .ઢ ? " Grinder" : " Drill") + " inv. balancing: " + (ߡ ? "On" : "Off"))) ߡ = !ߡ;
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Enable" + (ઠ == થ.ઢ ? " grinders" :
        " drills") + ": " + (ߠ ? "Fwd + Bwd" : "Fwd"))) ߠ = !ߠ;
    if (ऊ == true) {
      ޜ = 0.1 f;
      ޛ = 2.0 f;
      ޝ = 0.10 f;
      ޚ = 0.1 f;
      ޙ = 0.1 f;
    }
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ,
        " Work speed fwd.: " + ޜ.ToString("0.00") + "m/s")) ޑ(ref ޜ, 0.20 f, ޘ.ޗ, false);
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Work speed bwd.: " + ޛ.ToString("0.00") + "m/s")) ޑ(
      ref ޛ, 0.20 f, ޘ.ޗ, false);
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Acceleration: " + Math.Round(ޝ * 100 f) + "%" + (ޝ > 0.80 f ? " (risky)" : ""))) {
      ޑ(ref ޝ, 0.1 f, ޘ
        .ǵ, false);
    }
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Width overlap: " + (ޚ * 100 f).ToString("0.00") + "%")) ݗ(true, 0.05 f);
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ,
        " Height overlap: " + (ޙ * 100 f).ToString("0.00") + "%")) ݗ(false, 0.05 f);
  } else if (ލ == त.ݭ) {
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Back")) {
      if (ధ == ఏ.ఋ) ݨ(त.ݸ);
      else ݨ(त.ण);
    }
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Max load: " + ޏ + "%")) ޑ(ref ޏ, ޏ <= 80 ? -10 : -5, ޘ.d, false);
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Unload ice: " + (ߍ ? "On" :
        "Off"))) ߍ = !ߍ;
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Reactor Fuel: " + (ޟ == -1 ? "Ignore" : "Min " + ޟ + "Kg"))) ޑ(ref ޟ, (ޟ > 5 ? -5 : -1), ޘ.ޔ, true);
    if (ܬ(ref ݡ, ܠ,
        ܡ++, ܢ, " Battery: " + (ޠ == -1 ? "Ignore" : "Charge up"))) ޠ = (ޠ == -1 ? 1 : -1);
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Hydrogen: " + (ޞ == -1 ? "Ignore" :
        "Fill up"))) ޞ = (ޞ == -1 ? 1 : -1);
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " On damage: " + ހ(ߩ))) {
      ߩ = ޤ(ߩ);
    }
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Acceleration: " + Math.Round(ޝ *
        100 f) + "%" + (ޝ > 0.80 f ? " (risky)" : ""))) {
      ޑ(ref ޝ, 0.1 f, ޘ.ǵ, false);
    }
  } else if (ލ == त.ढ) {
    double ݛ = 0;
    if (ટ.Count > 0) ݛ = Vector3D.Distance(ટ.Last().ࡏ, ࡏ);
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Stop path recording")) ମ();
    if (ઠ != થ.ކ) {
      if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Home: " + (ଳ ? "Use old home" : (ଷ.ଣ ?
          "Was set! " : "none ")))) ଳ = !ଳ;
    } else {
      if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Connector 1: " + (ଳ ? "Use old connector" : (ଷ.ଣ ? "Was set! " : "none ")))) ଳ = !ଳ;
      if (ܬ(
          ref ݡ, ܠ, ܡ++, ܢ, " Connector 2: " + (ଲ ? "Use old connector" : (ا.ଣ ? "Was set! " : "none ")))) ଲ = !ଲ;
    }
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Path: " + (ର ?
        "Use old path" : (ટ.Count > 1 ? "Count: " + ટ.Count : "none ")))) ର = !ର;
    ݡ += ݐ;
    ݡ += "Wp spacing: " + Math.Round(ନ) + "m\n";
  } else if (ލ == त.ݑ) {
    if (ܬ(ref ݡ, ܠ, ܡ
        ++, ܢ, " Stop")) {
      ఙ();
      ݨ(त.ण);
    }
    ݡ += ݐ;
    ݡ += "State: " + ݻ(ஷ) + " \n";
    ݡ += "Speed: " + Math.Round(य़, 1) + "m/s\n";
    ݡ += "Target dist: " + ݢ + "m\n";
    ݡ
      += "Wp count: " + ટ.Count + "\n";
    ݡ += "Wp left: " + ব + "\n";
  } else if (ލ == त.ݵ) {
    List < IMyTerminalBlock > ݚ = ӊ();
    if (ࡆ) ݍ++;
    if (ݍ >= ݚ.Count) ݍ = 0;
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Next")) ݨ(त.ݴ);
    ݡ += ݐ;
    ݡ += "Version: " + VERSION + "\n" + "PamPlus Version: " + SenX_Version + "\n";
    ݡ += "Ship load: " +
      Math.Round(Մ, 1) + "% " + Math.Round(Ճ, 1) + " / " + Math.Round(Ղ, 1) + "\n";
    ݡ += "Battery: " + (Ս == ન.ۀ ? "" : Վ + "% ") + ݽ(Ս) + "\n";
    ݡ +=
      "Hydrogen: " + (ਫ.Count == 0 ? "No tanks" : Math.Round(Ӈ, 1) + "% ") + "\n";
    ݡ += "Jump Range: " + (ࢨ[0].MaxJumpDistanceMeters / 1000).ToString("N0") +
      " KM\n";
    ݡ += "Damage: " + (ݚ.Count == 0 ? "None" : "" + (ݍ + 1) + "/" + ݚ.Count + " " + ݚ[ݍ].CustomName) + "\n";
  } else if (ލ == त.ݴ) {
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ,
        " Back")) ݨ(त.ण);
    ݡ += ݐ;
    ݡ += "Next scan: " + ड़ + "s\n";
    ݡ += "Max Instructions: " + Math.Round(ग़ * 100 f, 1) + "% \n";
    ݡ += "Reactor Fuel: " + (ਭ.Count ==
      0 ? "No reactors" : Math.Round(ԝ, 1) + " Units") + "\n";
    foreach(KeyValuePair < string, double > ݙ in ࢤ) {
      ݡ +=
        $ "{ݙ.Key} [{ݙ.Value.ToString("
      N2 ")}] \n";
    }
  } else if (ލ == त.ݳ) {
    if (ܬ(ref ݡ, ܠ, ܡ++, ܢ, " Back")) ݨ(त.ण);
    ݡ += ݐ;
    ݡ += "1. Dock to your docking station\n";
    ݡ +=
      "2. Select Record path & set home\n";
    ݡ += "3. Fly the path to the ores\n";
    ݡ += "4. Select stop path recording\n";
    ݡ += "5. Align ship in mining direction\n";
    ݡ +=
      "6. Select Setup job and start\n";
  }
  if (ڿ == 2) ݡ = "Fatal setup error\nNext scan: " + ड़ + "s\n";
  if (ڹ) ݡ = "Recompile script now";
  int z = ݡ.Split('\n').Length;
  for (int ݘ =
    z; ݘ <= 10; ݘ++) ݡ += "\n";
  ݡ += ݏ;
  ݡ += "Last: " + ܭ + "\n";
  return ݡ;
}
void ݗ(bool ݖ, double ݕ) {
  ఙ();
  ప(true, false);
  if (ݖ) ޑ(ref ޚ, ݕ, ޘ.ޒ, false);
  else ޑ(ref ޙ, ݕ, ޘ.ޒ, false);
  ઈ();
  ǘ(true, true, 0, 0);
}
double ݔ(double ݓ, double[] ݒ) {
  double ƹ = ݒ[0];
  for (int ގ = ݒ.Length - 1; ގ >= 0; ގ--)
    if (ݓ < ݒ[ގ]) ƹ = ݒ[ގ];
  return ƹ;
}
String ޡ(ref int ɧ) {
  String ƶ = "";
  if (ɧ >= ਲ਼.Count) ɧ = -1;
  if (ɧ >= 0) {
    ƶ = ਲ਼[ɧ].CustomName;
  }
  ɧ++;
  return ƶ;
}
void ߜ(string Х) {
  if (ధ != ఏ.ఋ) return;
  if (Х == "") return;
  IMyTerminalBlock m = उ.GetBlockWithName(Х);
  if (m == null || !(m is IMyTimerBlock)) {
    ܭ = "Timerblock " + Х + " not found!";
    return;
  }((IMyTimerBlock) m).Trigger();
}
void Ն(ref int ݕ, int ߛ, int ߚ, int ߙ) {
  if (ߛ == 0)
    return;
  if (ݕ < ߚ && ߙ > 0 || ݕ <= ߚ && ߙ < 0) {
    ݕ += ߙ;
    return;
  }
  int ߘ = Math.Abs(ߛ);
  int б = 0;
  int ݥ = 1;
  while (true) {
    б += ݥ * ߘ * 10;
    if (ߛ < 0 && ݕ - ߚ <= б) break;
    if (ߛ >
      0 && ݕ - ߚ < б) break;
    ݥ++;
  }
  ݕ += ݥ * ߛ;
}
void ߗ(bool ߖ) {
  ߦ = Math.Max(ߦ, 1);
  ߥ = Math.Max(ߥ, 1);
  ߤ = Math.Max(ߤ, 0);
  ǘ(ލ == त.ࠈ, false, 0, 0);
}
public
enum ߕ {
  ߔ,
  ߓ,
  ߒ
}
public enum ߑ {
  ߐ,
  ߏ
}
public enum ߝ {
  ߞ,
  ࠆ,
  ࠇ,
  ࠅ
}
public enum ࠄ {
  ۀ,
  ࠃ,
  ࠂ,
  ࠁ,
  ࠀ,
  ߺ,
  ߵ
}
Գ ߴ = new Գ();
Գ ߪ = new Գ();
ߝ ߩ = ߝ.ࠇ;
bool ߨ = true;
ߑ ߧ = ߑ.ߐ;
int ߦ = 3;
int ߥ = 3;
int ߤ = 30;
ߕ ߣ = ߕ.ߔ;
bool ߢ = true;
bool ߡ = true;
bool ߠ = true;
bool ߟ = false;
ࠄ ߎ = ࠄ.ۀ;
bool ߍ = false;
double ޏ =
  100;
double ޠ = -1;
double ޟ = -1;
double ޞ = 20;
double ޝ = 0.60 f;
double ޜ = 0.50 f;
double ޛ = 1.0 f;
double ޚ = 0 f;
double ޙ = 0 f;
public enum ޘ {
  ޗ,
  ǵ,
  d,
  ޖ,
  ޕ,
  ޔ,
  ޓ,
  ĵ,
  ޒ
};
bool ޑ(ref double c, bool ސ, ޘ Ծ, String ƶ, String ߋ) {
  if (ƶ == "") return false;
  double ƹ = -1;
  bool ߌ = false;
  if (ƶ.ToUpper() == ߋ) ߌ = true;
  else if (!double.TryParse(ƶ, out ƹ)) return false;
  else ƹ = Math.Max(0, ƹ);
  if (ސ) ƹ = (double) Math.Round(ƹ);
  ޑ(ref c, ƹ,
    Ծ, ߌ, false);
  return true;
}
void ޑ(ref double c, double Ն, ޘ Ծ, bool ߋ) {
  ޑ(ref c, c + Ն, Ծ, ߋ, true);
}
void ޑ(ref double c, double ݧ, ޘ Ծ,
  bool ߋ, bool ߊ) {
  double б = 0;
  double ɰ = 0;
  if (Ծ == ޘ.ޗ) {
    ɰ = 0.1 f;
    б = 40 f;
  }
  if (Ծ == ޘ.ǵ) {
    ɰ = 0.1 f;
    б = 1 f;
  }
  if (Ծ == ޘ.ޖ) {
    ɰ = 50 f;
    б = 100 f;
  }
  if (Ծ == ޘ.ޕ) {
    ɰ =
      5 f;
    б = 30 f;
  }
  if (Ծ == ޘ.ޔ) {
    ɰ = 1 f;
    б = 25 f;
  }
  if (Ծ == ޘ.d) {
    ɰ = 10 f;
    б = 95 f;
  }
  if (Ծ == ޘ.ޓ) {
    ɰ = 10 f;
    б = 90 f;
  }
  if (Ծ == ޘ.ĵ) {
    ɰ = 10 f;
    б = 1800;
  }
  if (Ծ == ޘ.ޒ) {
    ɰ =
      0.0 f;
    б = 0.75 f;
  }
  if (ݧ == -1 && ߋ) {
    c = -1;
    return;
  }
  if (c == -1) ߋ = false;
  bool w = ݧ < ɰ || ݧ > б;
  if (w && ߊ) {
    if (ݧ < c) c = б;
    else if (ݧ > c) c = ɰ;
  } else c = ݧ;
  if (w &&
    ߋ) c = -1;
  else c = Math.Max(ɰ, Math.Min(c, б));
  c = (double) Math.Round(c, 2);
}
void ޱ(bool ޥ) {
  if (ޥ) স = Math.Max(0, স - 1);
  else স++;
  બ(true);
}
ǈ ޤ < ǈ > (ǈ ޣ) {
  int ǵ = Array.IndexOf(Enum.GetValues(ޣ.GetType()), ޣ);
  ǵ++;
  if (ǵ >= ॺ(ޣ)) ǵ = 0;
  return (ǈ) Enum.GetValues(ޣ.GetType())
    .GetValue(ǵ);
}
int ॺ < ǈ > (ǈ ޣ) {
  return Enum.GetValues(ޣ.GetType()).Length;
}
class ଥ {
  public bool ଣ = false;
  public Vector3D ࡏ = new
  Vector3D();
  public Vector3D ڢ = new Vector3D();
  public Vector3D Ƣ = new Vector3D();
  public Vector3D ڣ = new Vector3D();
  public Vector3D ơ =
    new Vector3D();
  public Vector3D ଢ = new Vector3D();
  public double ଡ = 0;
  public double ଠ = 0;
  public double[] ଟ = null;
  public ଥ() {}
  public ଥ(ଥ ଞ) {
    ଣ = ଞ.ଣ;
    ࡏ = ଞ.ࡏ;
    ڢ = ଞ.ڢ;
    Ƣ = ଞ.Ƣ;
    ڣ = ଞ.ڣ;
    ơ = ଞ.ơ;
    ଢ = ଞ.ଢ;
    ଟ = ଞ.ଟ;
  }
  public ଥ(Vector3D ࡏ, Vector3D Ƣ, Vector3D ڢ, Vector3D ڣ, Vector3D ơ) {
    this.ࡏ = ࡏ;
    this.ڢ = ڢ;
    this.Ƣ = Ƣ;
    this.ڣ = ڣ;
    this.ଡ = 0;
    this.ơ = ơ;
  }
  public void ଝ(List < IMyThrust > ଜ, List < string > ତ) {
    ଟ = new double[ତ.Count];
    for (int c = 0; c < ଟ.Length; c++) ଟ[c] = -1;
    for (int c = 0; c < ଜ.Count; c++) {
      string ƶ = ɬ(ଜ[c]);
      int ɧ = ତ.IndexOf(ƶ);
      if (ɧ != -1) ଟ[ɧ] = ǹ(ଜ[c]
        .MaxEffectiveThrust, ଜ[c].MaxThrust);
    }
  }
}
ଥ ଷ = new ଥ();
ଥ ଶ = new ଥ();
ଥ ଵ = new ଥ();
bool ଳ = false;
bool ଲ = false;
bool ର = false;
void ଯ() {
  ଫ.Clear();
  for (int c = 0; c < ટ.Count; c++) ଫ.Add(ટ[c]);
  ટ.Clear();
  ଭ = true;
  ଶ = new ଥ(ଷ);
  ଵ = new ଥ(ا);
  ଷ.ଣ = false;
  if (ઠ == થ.ކ) ا.ଣ = false;
  for (int c = 0; c < ɇ.Count; c++)
    if (!ବ.Contains(ɇ.Keys.ElementAt(c))) ବ.Add(ɇ.Keys.ElementAt(c));
  ଳ = false;
  ଲ = false;
  ର = false;
  ݨ(त.ढ);
}
void ମ() {
  if (ଳ) ଷ = ଶ;
  if (ଲ) ا = ଵ;
  if (ର) {
    ટ.Clear();
    for (int c = 0; c < ଫ.Count; c++) ટ.Add(ଫ[c]);
  }
  ଭ = false;
  ఙ();
  ݨ(त.ण);
}
bool ଭ = false;
List <
  String > ବ = new List < string > ();
List < ଥ > ટ = new List < ଥ > ();
List < ଥ > ଫ = new List < ଥ > ();
int ପ = 0;
double ନ = 0;
void ଧ() {
  if (!ଭ) return;
  if (ஷ != ஒ.ஐ) {
    ମ();
    return;
  }
  if (!ଶ.ଣ) ଳ = false;
  if (!ଵ.ଣ) ଲ = false;
  if (ଫ.Count <= 1) ର = false;
  IMyShipConnector Ǻ = b(MyShipConnectorStatus.Connectable);
  if (Ǻ == null) Ǻ = b(MyShipConnectorStatus.Connected);
  if (Ǻ != null) {
    if (Math.Round(य़, 2) <= 0.20) ପ++;
    else ପ = 0;
    if (ପ >= 5) {
      if (ઠ == થ.ކ && (ଷ
          .ଣ || ଳ) && Vector3D.Distance(ଷ.ࡏ, Ǻ.GetPosition()) > 5) {
        ا.Ƣ = ਸ਼.WorldMatrix.Forward;
        ا.ڣ = ਸ਼.WorldMatrix.Left;
        ا.ڢ = ਸ਼.WorldMatrix.Down;
        ا.ơ = ਸ਼.GetNaturalGravity();
        ا.ࡏ = Ǻ.GetPosition();
        ا.ଣ = true;
        ا.ଢ = Ǻ.Position;
      } else {
        ଷ.Ƣ = ਸ਼.WorldMatrix.Forward;
        ଷ.ڣ = ਸ਼.WorldMatrix.
        Left;
        ଷ.ڢ = ਸ਼.WorldMatrix.Down;
        ଷ.ơ = ਸ਼.GetNaturalGravity();
        ଷ.ࡏ = Ǻ.GetPosition();
        ଷ.ଣ = true;
        ଷ.ଢ = Ǻ.Position;
      }
    }
  }
  double ଦ = -1;
  if (ટ.Count >
    0) {
    ଦ = Vector3D.Distance(ࡏ, ટ.Last().ࡏ);
  }
  double ڬ = Math.Max(1.5, Math.Pow(य़ / 100.0, 2));
  double ଛ = Math.Max(य़ * ڬ, 2);
  ନ = ଛ;
  if ((ଦ == -1) ||
    ଦ >= ଛ) {
    ଥ ř = new ଥ(ࡏ, ࡎ, ࡌ, ࡍ, ਸ਼.GetNaturalGravity());
    ř.ଝ(ਰ, ବ);
    ટ.Add(ř);
  }
}
int ય(Vector3D ڈ, int ૡ) {
  if (ૡ == -1) return 0;
  double ૠ = -1;
  int સ = -1;
  for (int c = ટ.Count - 1; c >= 0; c--) {
    double ݛ = Vector3D.Distance(ટ[c].ࡏ, ڈ);
    if (ૠ == -1 || ݛ < ૠ) {
      સ = c;
      ૠ = ݛ;
    }
  }
  return Math.Sign(સ -
    ૡ);
}
bool ૐ(Vector3D ࡏ) {
  List < Vector3D > ƹ = new List < Vector3D > ();
  for (int c = 0; c < ટ.Count; c++) {
    ƹ.Add(ટ[c].ࡏ);
  }
  if (ଷ.ଣ && ટ.Count >= 1) {
    Vector3D ઽ = new Vector3D();
    ଚ(ଷ, dockDist * ਠ, false, out ઽ);
    if (Vector3D.Distance(ଷ.ࡏ, ટ.First().ࡏ) < Vector3D.Distance(ଷ.ࡏ, ટ.Last().ࡏ)) {
      ƹ.Insert(0, ઽ);
      ƹ.Insert(0, ଷ.ࡏ);
    } else {
      ƹ.Add(ઽ);
      ƹ.Add(ଷ.ࡏ);
    }
  }
  if (ઠ == થ.ކ) {
    if (ا.ଣ && ટ.Count >= 1) {
      Vector3D હ = new Vector3D();
      ଚ(ا, dockDist * ਠ, false, out હ);
      if (Vector3D.Distance(ا.ࡏ, ટ.First().ࡏ) < Vector3D.Distance(ا.ࡏ, ટ.Last().ࡏ)) {
        ƹ.Insert(0, હ);
        ƹ.
        Insert(0, ا.ࡏ);
      } else {
        ƹ.Add(હ);
        ƹ.Add(ا.ࡏ);
      }
    }
  } else {
    if (ధ != ఏ.ఎ)
      if (ટ.Count > 0 && Vector3D.Distance(ا.ࡏ, ટ.First().ࡏ) < Vector3D.Distance(ا
          .ࡏ, ટ.Last().ࡏ)) ƹ.Insert(0, ا.ࡏ);
      else ƹ.Add(ا.ࡏ);
  }
  int સ = -1;
  double ષ = -1;
  for (int c = 0; c < ƹ.Count; c++) {
    double ݛ = Vector3D.
    Distance(ƹ[c], ࡏ);
    if (ݛ < ષ || ષ == -1) {
      ષ = ݛ;
      સ = c;
    }
  }
  if (ƹ.Count == 0) return false;
  double શ = Vector3D.Distance(ƹ[સ], ࡏ);
  double વ = Vector3D.
  Distance(ƹ[Math.Max(0, સ - 1)], ƹ[સ]) * 1.5 f;
  double ળ = Vector3D.Distance(ƹ[Math.Min(ƹ.Count - 1, સ + 1)], ƹ[સ]) * 1.5 f;
  return શ < વ || શ < ળ;
}
ଥ લ =
  null;
void ર(ଥ ř, ఏ ଆ) {
  લ = ř;
  if (ధ == ఏ.ఋ) ஶ = ଆ;
}
ଥ ଙ() {
  if (ઠ != થ.ކ) return ଷ;
  return લ;
}
bool ଚ(ଥ ଘ, double ݛ, bool ଗ, out Vector3D ଖ) {
  if (ଗ) {
    Vector3I Ƴ = new Vector3I((int) ଘ.ଢ.X, (int) ଘ.ଢ.Y, (int) ଘ.ଢ.Z);
    IMySlimBlock ত = Me.CubeGrid.GetCubeBlock(Ƴ);
    if (ত == null || !(ত.FatBlock is IMyShipConnector)) {
      ଖ = new Vector3D();
      return false;
    }
    Vector3D Ǜ = ܐ(ਸ਼, ত.FatBlock.GetPosition() - ࡏ);
    Vector3D କ = ܐ(ਸ਼, ত.FatBlock.WorldMatrix.Forward);
    ଖ = ଘ.ࡏ - ܛ(ଘ.Ƣ, ଘ.ڢ * -1, Ǜ) - ܛ(ଘ.Ƣ, ଘ.ڢ * -1, କ) * ݛ;
    return true;
  } else {
    ଖ = ଘ.ࡏ;
    return true;
  }
}
Vector3D ଔ = new Vector3D();
bool ଓ = false;
Vector3D ଐ(int Ǳ, int ǰ, bool ଏ) {
  if (!ଏ && ଓ) return ଔ;
  double d = ((థ - 1 f) / 2 f) - Ǳ;
  double ĵ = ((త - 1 f) / 2 f) - ǰ;
  ଔ = ا.ࡏ + ا.ڣ * d * ઐ + న * -1 * ĵ * એ;
  ଓ = true;
  return ଔ;
}
Vector3D ଌ(Vector3D ଋ, double ଊ) {
  return ଋ + (ఫ * ଊ);
}
public enum ଉ {
  ଈ,
  ଅ,
  ଇ,
  শ,
  క,
  ఓ
}
ଉ ఒ() {
  double ݛ = -1;
  ଉ ǵ = ଉ.ଈ;
  if (ઠ != થ.ކ) {
    if (ధ !=
      ఏ.ఎ) {
      Vector3D ܔ = ܞ(ఫ, న * -1, ࡏ - ا.ࡏ);
      if (Math.Abs(ܔ.X) <= (double)(థ * ઐ) / 2 f && Math.Abs(ܔ.Y) <= (double)(త * એ) / 2 f) {
        if (ܔ.Z <= -1 && ܔ.Z >= -শ *
          2) return ଉ.শ;
        if (ܔ.Z > -1 && ܔ.Z < ਠ * 2) return ଉ.ଈ;
      }
      if (ఐ(ا.ࡏ, ref ݛ)) ǵ = ଉ.ଈ;
    }
    if (ଷ.ଣ) {
      if (ఐ(ଷ.ࡏ, ref ݛ)) ǵ = ଉ.ଇ;
      for (int c = 0; c < ટ.Count; c
        ++) {
        if (ఐ(ટ[c].ࡏ, ref ݛ)) ǵ = ଉ.ଅ;
      }
      if (Vector3D.Distance(ࡏ, ଷ.ࡏ) < dockDist * ਠ) ǵ = ଉ.ଇ;
      if (b(MyShipConnectorStatus.Connectable) != null ||
        b(MyShipConnectorStatus.Connected) != null) ǵ = ଉ.ଇ;
    }
  } else {
    Vector3D ࡏ = new Vector3D();
    IMyShipConnector Ƙ = b(
      MyShipConnectorStatus.Connected);
    if (ଷ.ଣ) {
      if (ఐ(ଷ.ࡏ, ref ݛ)) ǵ = ଉ.ଇ;
      if (ଚ(ଷ, dockDist, true, out ࡏ))
        if (ఐ(ࡏ, ref ݛ)) ǵ = ଉ.ଇ;
      if (Ƙ != null && Vector3D.Distance(
          Ƙ.GetPosition(), ଷ.ࡏ) < 5) return ଉ.క;
    }
    for (int c = 0; c < ટ.Count; c++)
      if (Vector3D.Distance(ટ[c].ࡏ, ଷ.ࡏ) > dockDist * ਠ && Vector3D.Distance(ટ[c].ࡏ, ا.ࡏ) > dockDist * ਠ)
        if (ఐ(ટ[c].ࡏ, ref ݛ)) ǵ = ଉ.ଅ;
    if (ا.ଣ) {
      if (ఐ(ا.ࡏ, ref ݛ)) ǵ = ଉ.ଈ;
      if (ଚ(ا, dockDist, true, out ࡏ))
        if (ఐ(ࡏ, ref ݛ)) ǵ = ଉ.ଈ;
      if (Ƙ != null && Vector3D.Distance(Ƙ.GetPosition(), ا.ࡏ) < 5) return ଉ.ఓ;
    }
  }
  return ǵ;
}
bool ఐ(Vector3D چ, ref double ݛ) {
  double
  ܔ = Vector3D.Distance(چ, ࡏ);
  if (ܔ < ݛ || ݛ == -1) {
    ݛ = ܔ;
    return true;
  }
  return false;
}
public enum ఏ {
  ఎ,
  ఌ,
  ఋ,
  ఊ,
  ఉ,
  ఈ,
  ఇ,
  ఔ,
  ఆ
}
ଥ ا = new ଥ();
Vector3D ఫ;
Vector3D న;
ఏ ధ = ఏ.ఎ;
ߑ ద = ߑ.ߐ;
int థ = 0;
int త = 0;
double ణ = 0;
bool ఢ = false;
void డ() {
  foreach(IMyGyro ఠ in अ) {
    ऄ.Add(ఠ.GyroPower);
  }
}
void ట() {
  డ();
  if (ڿ > 0) {
    ܭ = "Setup error! Can't start";
    return;
  }
  if (ઠ == થ.ކ) {
    ఘ();
    return;
  }
  ا.ࡏ = ࡏ;
  ا.ơ = ਸ਼.GetNaturalGravity();
  ا.Ƣ =
    ࡎ;
  ا.ڢ = ࡌ;
  ا.ڣ = ࡍ;
  ఫ = ਣ.WorldMatrix.Forward;
  న = ا.ڢ;
  if (ఫ == ਸ਼.WorldMatrix.Down) న = ਸ਼.WorldMatrix.Backward;
  ప(true, true);
  வ(ஒ.ஏ);
  చ();
}
void ప(bool w, bool ఞ) {
  డ();
  if (ధ == ఏ.ఎ && !w) return;
  bool ఝ = w || ధ == ఏ.ఊ || థ != ߦ || త != ߥ || ద != ߧ;
  if (ఝ) {
    if (ధ != ఏ.ఎ) {
      ధ = ఏ.ఉ;
      ଐ(ঽ, হ, ఞ);
      ܭ =
        "Job changed, lost progress";
    }
    ద = ߧ;
    థ = ߦ;
    త = ߥ;
    হ = 0;
    ঽ = 0;
    ਙ = 0;
    ষ = 0;
    ਚ = 0;
    স = 0;
    બ(true);
  }
}
void జ() {
  ڌ(ࡏ, 0);
  R(ਰ, true);
}
int ఛ = 0;
void చ() {
  ޅ(ދ.ފ);
  జ();
  n(ਮ, false);
  r(s);
  ధ
    = ఏ.ఋ;
  Ɯ(true);
  ஶ = ధ;
  ݨ(त.ݸ);
  ਡ();
  ఢ = true;
  ఛ = 0;
  for (int c = ਪ.Count - 1; c >= 0; c--)
    if (ӈ(ਪ[c], false)) ఛ++;
  if (ఛ > 0) ܭ = "Started with damage";
}
void ఙ() {
  if (ధ == ఏ.ఋ) {
    ధ = ఏ.ఌ;
    ܭ = "Job paused";
  }
  வ(ஒ.ஐ);
  ஶ = ధ;
  ɣ(false, 0, 0, 0, 0);
  گ();
  Ǵ(new Vector3D(), false);
  ڵ();
  p(ChargeMode.Auto);
  ǋ(
    false);
  g(true);
  ள(ஒ.ஐ);
  ǘ(false, false, 0, 0);
  R(ਯ, false);
  R(ਲ, true);
  r(true);
  ভ = false;
  ఢ = false;
  ƫ = false;
  ग = false;
  if (ލ != त.ण && ލ != त.ݷ && ލ != त
    .ݶ && ލ != त.ݭ) ݨ(त.ण);
}
void ఘ() {
  ଉ ସ = ఒ();
  if (ઠ == થ.ކ) {
    if (!ا.ଣ || !ଷ.ଣ) return;
    చ();
    bool గ = Vector3D.Distance(ࡏ, ଷ.ࡏ) < Vector3D.Distance(ࡏ, ا.ࡏ);
    if (ڸ == ఏ.ఔ) గ = true;
    if (ڸ == ఏ.ఆ) గ = false;
    if (గ) {
      ર(ଷ, ఏ.ఔ);
      switch (ସ) {
      case ଉ.క:
        வ(ஒ.ஸ);
        break;
      case ଉ.ଅ:
        வ(ஒ.இ);
        break;
      case ଉ.ଇ:
        வ(ஒ.அ);
        break;
      default:
        வ(ஒ.ଢ଼);
        break;
      }
    } else {
      ર(ا, ఏ.ఆ);
      switch (ସ) {
      case ଉ.ఓ:
        வ(ஒ.ஸ);
        break;
      case ଉ.ଈ:
        வ(ஒ.அ);
        break;
      case ଉ.ଅ:
        வ(ஒ.இ);
        break;
      default:
        வ(ஒ.ଢ଼);
        break;
      }
    }
  } else {
    if (ధ != ఏ.ఌ && ధ != ఏ.ఉ) return;
    bool ఖ = ధ == ఏ.ఉ;
    చ();
    bool ଇ = ƪ(false) && ଷ.ଣ;
    switch (ସ) {
    case ଉ.ଈ:
      வ(ଇ ? ஒ.ଡ଼ :
        ஒ.ஏ);
      break;
    case ଉ.ଅ:
      வ(ଇ ? ஒ.ஈ : ஒ.இ);
      break;
    case ଉ.ଇ:
      வ(ଇ ? ஒ.அ : ஒ.ୟ);
      break;
    case ଉ.শ: {
      if (ষ != স || ఖ) வ(ஒ.ஊ);
      else வ(ஒ.எ);
    }
    break;
    default:
      break;
    }
  }
}
void க() {
  if (ధ == ఏ.ఎ && !ଷ.ଣ) return;
  if (ઠ == થ.ކ && (!ا.ଣ || !ଷ.ଣ)) return;
  ܭ = "Move to job";
  ଉ ସ = ఒ();
  if (ઠ == થ.ކ) {
    ર(ا, ఏ.ఆ);
    switch (ସ) {
    case ଉ.ଈ:
      வ(ஒ.அ);
      break;
    case ଉ.ଅ:
      வ(ஒ.இ);
      break;
    case ଉ.ఓ:
      return;
    default:
      வ(ஒ.ଢ଼);
      break;
    }
    ள(ஒ.ஸ);
  } else {
    switch (ସ) {
    case ଉ.ଈ:
      வ(
        ஒ.இ);
      break;
    case ଉ.ଅ:
      வ(ஒ.இ);
      break;
    case ଉ.ଇ:
      வ(ஒ.ୟ);
      break;
    case ଉ.শ:
      வ(ஒ.ஊ);
      break;
    default:
      break;
    }
    if (ధ == ఏ.ఎ) ள(ஒ.இ);
    else ள(ஒ.ୡ);
    ভ = true;
  }
  జ();
  ݨ(त.ݑ);
  n(ਮ, false);
  ஶ = ఏ.ఇ;
}
void ஓ() {
  if (!ଷ.ଣ) return;
  ܭ = "Move home";
  ଉ ସ = ఒ();
  if (ઠ == થ.ކ) {
    ર(ଷ, ఏ.ఔ);
    switch (ସ) {
    case ଉ.ଅ:
      வ(ஒ.ஈ);
      break;
    case ଉ.ଇ:
      வ(ஒ.அ);
      break;
    case ଉ.క:
      return;
    default:
      வ(ஒ.ଢ଼);
      break;
    }
    ள(ஒ.ஸ);
  } else {
    if (b(MyShipConnectorStatus.Connected) != null) return;
    if (b(MyShipConnectorStatus.Connectable) != null) {
      வ(ஒ.ஃ);
      ள(ஒ.ୟ);
      return;
    }
    switch (ସ) {
    case ଉ.ଈ:
      வ(ஒ.ଡ଼);
      break;
    case
    ଉ.ଅ:
      வ(ஒ.ஈ);
      break;
    case ଉ.ଇ:
      வ(ஒ.ஈ);
      break;
    case ଉ.শ:
      வ(ஒ.உ);
      break;
    default:
      break;
    }
    ள(ஒ.ୟ);
  }
  జ();
  ݨ(त.ݑ);
  n(ਮ, false);
  ஶ = ఏ.ఈ;
}
public
enum ஒ {
  ஐ,
  ஏ,
  எ,
  ஊ,
  உ,
  ஈ,
  இ,
  ஆ,
  அ,
  ஃ,
  ୱ,
  ୡ,
  ୠ,
  ୟ,
  ଢ଼,
  ଡ଼,
  ଽ,
  ହ,
  ஔ,
  ங,
  ௐ,
  అ,
  ஹ,
  ஸ,
}
ஒ ஷ;
ఏ ஶ;
void வ(ஒ ழ) {
  if (ழ == ஒ.ஐ) ல = ஒ.ஐ;
  if (ல != ஒ.ஐ && ஷ == ல && ழ != ல) {
    ఙ();
    return;
  }
  র = true;
  ஷ = ழ;
}
ஒ ல;
void ள(ஒ ல) {
  this.ல = ல;
}
ர ற = null;
class ர {
  public ଥ ய = null;
  public List < Vector3D > ம = new List < Vector3D > ();
  public double ப = 0;
  public double ன = 0;
  public double ந = 0;
  public double த = 0;
  public Vector3D ண = new Vector3D();
}
public enum ட {
  ஞ,
  ஜ,
  ச
}
int[] મ = null;
ட ਅ(int চ, bool w) {
  if (w) {
    મ = null;
    ঽ = 0;
    হ = 0;
  }
  if (ߧ == ߑ.ߐ) {
    int ৰ = চ + 1;
    হ = (int) Math.Floor(ǹ(চ, థ));
    if (হ % 2 == 0) ঽ = চ - (হ * థ);
    else
      ঽ = థ - 1 - (চ - (হ * థ));
    if (হ >= త) return ட.ஜ;
    else return ட.ஞ;
  } else if (ߧ == ߑ.ߏ) {
    if (મ == null) મ = new int[] {
      0,
      -1,
      0,
      0
    };
    int ৡ = (int) Math.
    Ceiling(థ / 2 f);
    int ৠ = (int) Math.Ceiling(త / 2 f);
    int য় = (int) Math.Floor(థ / 2 f);
    int ঢ় = (int) Math.Floor(త / 2 f);
    int ড় = 0;
    while (મ[2] < Math.Pow(Math.Max(థ, త), 2)) {
      if (ড় > 200) return ட.ச;
      ড়++;
      મ[2]++;
      if (-ৡ < ঽ && ঽ <= য় && -ৠ < হ && হ <= ঢ়) {
        if (મ[3] == চ) {
          this.ঽ = ঽ - 1 + ৡ;
          this.হ = হ - 1 + ৠ;
          return
          ட.ஞ;
        }
        મ[3]++;
      }
      if (ঽ == হ || (ঽ < 0 && ঽ == -হ) || (ঽ > 0 && ঽ == 1 - হ)) {
        int ৎ = મ[0];
        મ[0] = -મ[1];
        મ[1] = ৎ;
      }
      ঽ += મ[0];
      হ += મ[1];
    }
  }
  return ட.ஜ;
}
int ঽ = 0;
int হ = 0;
int স = 0;
int ষ = 0;
int শ = 30;
int ল = 0;
bool র = true;
Vector3D য;
double ম = 0;
bool ভ = false;
int ব = 0;
int ফ = 0;
int প = 0;
int ৱ = 0;
int
ਆ = 0;
Vector3D ਞ = new Vector3D();
double ਟ = 0;
double ਝ = 0;
double ਜ = 0;
double ਛ = 0;
double ਚ = 0;
double ਙ = 0;
bool ਘ = false;
bool ਗ = false;
bool ਖ = false;
bool ਕ = false;
bool ਔ = false;
DateTime Լ = new DateTime();
ଥ ਓ = null;
void ਐ() {
  if (ஷ == ஒ.ஏ) {
    if (র) {
      ফ = 0;
      if (ষ != স) {
        ਙ = 0;
      }
      ষ =
        স;
    }
    if (ফ == 0) {
      ட ƹ = ਅ(স, র);
      if (ƹ == ட.ஜ) {
        ధ = ఏ.ఊ;
        ܭ = "Job done";
        if (ߨ && ଷ.ଣ) {
          வ(ஒ.ଡ଼);
          ள(ஒ.ୟ);
          ஶ = ఏ.ఈ;
        } else {
          வ(ஒ.ஆ);
          ள(ஒ.ୡ);
          ஶ = ఏ.ఇ;
        }
        return;
      }
      if (ƹ == ட.ஞ) {
        ফ = 1;
        R(ਯ, true);
        য = ଐ(ঽ, হ, true);
        ڌ(য, 10);
        ڴ(ا.ڢ, ا.Ƣ, ا.ڣ, false);
      }
    } else {
      if (ম < wpReachedDist) {
        வ(ஒ.எ);
        return;
      }
    }
  }
  if (ஷ == ஒ.எ) {
    if (র) {
      R(ਯ, true);
      r(false);
      য = ଐ(ঽ, হ, false);
      ڌ(ଌ(য, 0), 0);
      ڴ(ا.ڢ, ا.Ƣ, ا.ڣ, false);
      ফ = 1;
      ਟ = 0;
      ਝ = 0;
      ਛ = 0;
      ਜ = -1;
      শ = ߤ;
      ਘ = true;
    }
    if (!Ӌ()) {
      வ(ஒ.உ);
      return;
    }
    if (ƪ(true)) {
      ਆ = Յ("", "ORE", Ъ.Ч);
      if ((ߎ == ࠄ.ࠁ || ߎ == ࠄ.ࠀ || ߎ == ࠄ.ߺ || ߎ == ࠄ.ߵ) && ઠ != થ.ઢ) வ(ஒ.అ);
      else if ((ߎ == ࠄ.ࠃ || ߎ == ࠄ.ࠂ) && ઠ != થ.ઢ) வ(ஒ.ங);
      else வ(ஒ.உ);
      return;
    }
    ਚ = Vector3D.Distance(ࡏ, য);
    if (ਚ > ਙ) {
      ਙ = ਚ;
      ਘ = false;
    }
    if (ઠ == થ.ઢ && ફ() == MyDetectedEntityType.SmallGrid) ਝ += 2;
    else ਝ -= 2;
    ਝ = Math.Max(100, Math.Min(400, ਝ));
    if (ফ > 0 && ফ < ਝ) {
      if (ਚ > ਟ) {
        if (ਝ > 150) ਟ = ਚ;
        else ਟ = (double) Math.Ceiling(ਚ);
        ফ = 1;
      } else ফ++;
    } else {}
    ǘ(false, true, ਠ * sensorRange, 0);
    Vector3D ਏ = য + ఫ * ਚ;
    bool ਊ = false;
    if (Vector3D.Distance(ਏ, ࡏ) > 0.3 f) {
      Vector3D ਉ = য + ఫ * (ਚ + 0.1 f);
      ڌ(
        ਉ, 4);
      ਊ = true;
    } else {
      double य़ = પ(true);
      Vector3D ਈ = ଌ(য, Math.Max(ߤ + 1, ਚ + 1));
      ڌ(true, false, false, ਈ, ਈ - য, य़, य़);
    }
    bool भ = false;
    if (ߣ == ߕ.ߒ || ߣ == ߕ.ߓ) {
      if (!ਊ) {
        double ਇ = 0;
        foreach(IMyTerminalBlock m in ਯ) ਇ += Ь(m, "", "", ߣ == ߕ.ߓ ? new string[] {
          "STONE"
        } : null);
        if (ਇ > ਜ || ਚ < ߤ ||
          ਘ) {
          প = 0;
          ਛ = ਚ;
          শ = (int)(Math.Max(শ, ਛ) + ਠ / 2);
        } else {
          भ = ਚ - ਛ > 2 && প >= 20;
          প++;
        }
        ਜ = ਇ;
      }
    } else भ = ਚ >= শ;
    if (ষ != স) {
      வ(ஒ.ஊ);
      ਚ = 0;
      return;
    }
    if (भ) {
      স++;
      வ(ஒ.ஊ);
      ਚ = 0;
      return;
    }
  }
  if (ஷ == ஒ.ஹ) {
    bool भ = false;
    if (র) {
      r(true);
      if ((ߎ == ࠄ.ࠁ || ߎ == ࠄ.ࠀ) && ڇ() && ۿ(ఫ, ਸ਼.GetNaturalGravity()) < 25 && థ >= 2 &&
        త >= 2) {
        Vector3D ন = ࡏ;
        if (ঽ > 0 && হ < త - 1) ন = ଐ(ঽ - 1, হ + 1, true);
        else if (ঽ < థ - 1 && হ < త - 1) ন = ଐ(ঽ + 1, হ + 1, true);
        else if (ঽ < థ - 1 && হ > 0) ন = ଐ(ঽ + 1, হ - 1,
          true);
        else if (ঽ > 0 && হ > 0) ন = ଐ(ঽ - 1, হ - 1, true);
        else भ = true;
        if (!भ) ڌ(ন, 10);
      } else भ = true;
    }
    if (ম < wpReachedDist / 2) भ = true;
    if (भ) {
      வ(ஒ.ௐ);
      return;
    }
  }
  if (ஷ == ஒ.ங || ஷ == ஒ.ௐ) {
    if (র) {
      ڌ(true, true, false, ࡏ, 0);
      R(ਯ, false);
      r(true);
      ফ = -1;
      ਝ = ߎ == ࠄ.ߺ || ߎ == ࠄ.ߵ ? 0 : -1;
    }
    bool ƹ = !Ӌ();
    int ʵ = Յ(
      "STONE", "ORE", Ъ.Ч);
    if (ߎ == ࠄ.ࠂ || ߎ == ࠄ.ߵ || ߎ == ࠄ.ࠀ) ʵ += Յ("ICE", "ORE", Ъ.Ч);
    bool ঘ = ʵ > 0;
    bool ڿ = false;
    if (ਝ >= 0) {
      double Ǳ = (double) Math.Sin(ە(ਝ)) * ઐ / 3 f;
      double ǰ = (double) Math.Cos(ە(ਝ)) * એ / 3 f;
      Vector3D গ = ଐ(ঽ, হ, true) + ܛ(ఫ, న * -1, new Vector3D(Ǳ, ǰ, 0));
      ڌ(গ, 0.3 f);
      if (ম < Math.Min(ઐ, એ) / 10 f) ਝ += 5 f;
      if (ਝ >= 360) ਝ = 0;
    }
    if (ফ == -1 || ʵ < ফ) {
      ফ = ʵ;
      ਟ = 0;
    } else {
      ਟ++;
      if (ਟ > 50) ڿ = true;
    }
    if (!ঘ || ƹ || ڿ) {
      if (!ƹ) {
        int খ = Յ("", "ORE", Ъ.Ч);
        if (ƪ(true)) ƹ = true;
        else if (100 - (ǹ(খ, ਆ) * 100) < minEjection) {
          ƹ = true;
        } else ޅ(ދ.ފ);
      }
      if (ڿ && ƹ) ܭ = "Ejection failed";
      if (ஷ == ஒ.ௐ) {
        if (
          ƹ) {
          if (ଷ.ଣ) வ(ஒ.ଡ଼);
          else {
            ఙ();
            க();
            ܭ = "Can´t return, no dock found";
          }
        } else வ(ஒ.ஏ);
      } else if (ƹ) வ(ஒ.உ);
      else வ(ஒ.எ);
      return;
    }
  }
  if (ஷ ==
    ஒ.ஊ || ஷ == ஒ.உ || ஷ == ஒ.అ) {
    if (র) {
      য = ଐ(ঽ, হ, false);
      ڴ(ا.ڢ, ا.Ƣ, ا.ڣ, false);
      R(ਯ, ߠ);
      r(false);
      ਟ = Vector3D.Distance(ࡏ, য);
      ǘ(false, true, 0, ਠ *
        sensorRange);
    }
    ڌ(য, પ(false));
    if (Vector3D.Distance(ࡏ, য) >= ਟ + 5) {
      R(ਯ, false);
      r(true);
      ܭ = "Can´t return!";
    }
    if (ম < wpReachedDist) {
      if (ஷ == ஒ.ஊ && ভ)
        வ(ஒ.ஆ);
      if (ஷ == ஒ.ஊ) வ(ஒ.ஏ);
      if (ஷ == ஒ.అ) வ(ஒ.ஹ);
      if (ஷ == ஒ.உ) {
        if (ଷ.ଣ) வ(ஒ.ଡ଼);
        else {
          ఙ();
          க();
          ܭ = "Can´t return, no dock found";
        }
      }
      return;
    }
  }
  if (ஷ == ஒ.ଡ଼) {
    if (র) {
      r(true);
      R(ਯ, false);
      int ɧ = -1;
      double ক = -1;
      for (int c = ટ.Count - 1; c >= 0; c--) {
        double ݛ = Vector3D.Distance(ટ[c].ࡏ, ࡏ);
        if (ক == -1 || ݛ < ক) {
          ɧ = c;
          ক = ݛ;
        }
      }
      if (ɧ == -1) {
        வ(ஒ.ஈ);
        return;
      }
      Ȉ = ટ[ɧ].ࡏ;
      ڌ(Ȉ, 10);
      ڴ(ا.ڢ, ا.Ƣ, ا.ڣ, false);
    }
    if (ম < wpReachedDist) {
      வ(ஒ.ஈ);
      return;
    }
  }
  if (ஷ == ஒ.ஈ || ஷ == ஒ.இ) {
    if (ஷ == ஒ.இ && ధ == ఏ.ఋ && ઠ != થ.ކ) {
      if (!Ӌ() || ƪ(true)) {
        வ(ஒ.ஈ);
        return;
      }
    }
    bool भ = false;
    bool ঔ = false;
    bool ও =
      false;
    double ঐ = 0;
    bool এ = false;
    ଥ ř = null;
    if (র) {
      if (ஷ == ஒ.ஈ || ઠ == થ.ކ) {
        ଥ ঌ = ଙ();
        ற = new ர();
        ற.ய = ঌ;
        ற.ப = followPathDock * ਠ;
        ற.ன =
          useDockDirectionDist * ਠ;
        ற.ந = 10;
        ற.ம.Add(ঌ.ࡏ);
        Vector3D ঋ = new Vector3D();
        if (ଚ(ঌ, dockDist * ਠ, true, out ঋ)) ற.ம.Add(ঋ);
        else ற.ப *= 1.5 f;
        if (ઠ == થ.ކ) {
          if (ঌ ==
            ଷ) ற.ண = ا.ࡏ;
          if (ঌ == ا) ற.ண = ଷ.ࡏ;
          ற.த = dockDist * ਠ * 1.1 f;
        }
      } else if (ஷ == ஒ.இ) {
        ற = new ர();
        ற.ய = ا;
        ற.ப = followPathJob * ਠ;
        ற.ன =
          useJobDirectionDist * ਠ;
        ற.ந = 10;
        ற.ண = ଷ.ࡏ;
        ற.த = dockDist * ਠ * 1.1 f;
        ற.ம.Add(ا.ࡏ);
        if (ధ == ఏ.ఎ) {
          if (!ଷ.ଣ || ટ.Count == 0) {
            ఙ();
            return;
          }
          double ঊ = Vector3D.
          Distance(ટ.First().ࡏ, ଷ.ࡏ);
          double উ = Vector3D.Distance(ટ.Last().ࡏ, ଷ.ࡏ);
          if (ঊ < উ) ற.ய = ટ.Last();
          else ற.ய = ટ.First();
        }
      }
      ਞ = new Vector3D();
      এ
        = !ૐ(ࡏ);
      R(ਯ, false);
      r(true);
      ল = -1;
      double ক = -1;
      for (int c = ટ.Count - 1; c >= 0; c--) {
        if (Vector3D.Distance(ટ[c].ࡏ, ற.ண) <= ற.த) continue;
        double ݛ = Vector3D.Distance(ટ[c].ࡏ, ࡏ);
        if (ক == -1 || ݛ < ক) {
          ল = c;
          ক = ݛ;
        }
      }
      ৱ = ય(ற.ய.ࡏ, ল);
      ਓ = null;
    }
    ਹ(ટ, ৱ, ற.ம, ற.ப, র, ref ফ);
    for (int c = 0; c < ற.ம.Count; c++) {
      double ݛ = Vector3D.Distance(ࡏ, ற.ம[c]);
      if (ݛ <= ற.ப) भ = true;
      if (ݛ <= ற.ன) ঔ = true;
    }
    if (ঔ) ঐ = ற.ந;
    double ধ = ਓ != null ? ਓ.ଡ : य़;
    double দ
      = (double) Math.Max(य़ * 0.1 f * ਠ, wpReachedDist);
    if ((ম < দ) || র) {
      if (!র) ল += ৱ;
      if (ৱ == 0 || ল > ટ.Count - 1 || ল < 0) भ = true;
      else {
        ব = ৱ > 0 ? ટ.Count - 1 - ল :
          ল;
        ř = ટ[ল];
        ਓ = ř;
        if (ল >= 1 && ল < ટ.Count - 1) ਞ = ř.ࡏ - ટ[ল - ৱ].ࡏ;
        else ਓ = null;
        Ȉ = ř.ࡏ;
        ও = true;
      }
    }
    if (ঔ) ڴ(ற.ய.ڢ, ற.ய.Ƣ, ற.ய.ڣ, false);
    else if (এ) ڲ(
      ற.ய.ڢ, 10, true);
    else if (ও && ř != null)
      if (ৱ > 0) ڴ(ř.ڢ, ř.Ƣ, ř.ڣ, 90, false);
      else ڴ(ř.ڢ, -ř.Ƣ, -ř.ڣ, 90, false);
    ڌ(true, false, true, Ȉ, ਞ, ਓ ==
      null ? 0 : ਓ.ଡ, ঐ);
    if (भ) {
      ব = 0;
      if (ஷ == ஒ.ஈ || ઠ == થ.ކ) {
        வ(ஒ.அ);
        return;
      }
      if (ஷ == ஒ.இ && ভ) {
        வ(ஒ.ஆ);
        return;
      }
      if (ஷ == ஒ.இ) {
        வ(ஒ.ஏ);
        return;
      }
    }
  }
  if (ஷ == ஒ.அ ||
    ஷ == ஒ.ୠ) {
    ଥ ঌ = ଙ();
    if (র) {
      if (!ଚ(ঌ, dockDist * ਠ, true, out Ȉ)) {
        ޅ(ދ.މ);
        ఙ();
        return;
      }
      ڌ(Ȉ, 0);
      ڲ(ঌ.ڢ, 90, true);
    }
    if (ম < followPathDock * ਠ && ম !=
      -1) {
      ڌ(Ȉ, 10);
      ڴ(ঌ.ڢ, ঌ.Ƣ, ঌ.ڣ, false);
    }
    if (b(MyShipConnectorStatus.Connectable) != null || b(MyShipConnectorStatus.Connected) !=
      null) {
      வ(ஒ.ஃ);
      return;
    }
    if (ম < wpReachedDist / 2 && ম != -1) {
      வ(ஒ.ୱ);
      return;
    }
  }
  if (ஷ == ஒ.ୱ || ஷ == ஒ.ୡ) {
    if (র) {
      if (ஷ == ஒ.ୱ) {
        ଥ ঌ = ଙ();
        if (!ଚ(ঌ,
            dockDist * ਠ, true, out Ȉ)) {
          ޅ(ދ.މ);
          ఙ();
          return;
        }
        ڌ(true, true, false, Ȉ, 0);
        ڴ(ঌ.ڢ, ঌ.Ƣ, ঌ.ڣ, 10, false);
      }
      if (ஷ == ஒ.ୡ) {
        ڴ(ا.ڢ, ا.Ƣ, ا.ڣ, 0.5 f, false);
        Ȉ = ا.ࡏ;
        ڌ(true, true, false, Ȉ, 0);
      }
    }
    if (ک) {
      ɣ(false, 0, 0, 0, 0);
      if (ஷ == ஒ.ୱ) வ(ஒ.ஃ);
      if (ஷ == ஒ.ୡ) ఙ();
      return;
    }
  }
  if (ஷ == ஒ.ஃ) {
    if (b(
        MyShipConnectorStatus.Connected) != null) {
      if (ઠ == થ.ކ) வ(ஒ.ஸ);
      else வ(ஒ.ୟ);
      return;
    }
    ଥ ঌ = ଙ();
    if (র) {
      ਝ = 0;
      Լ = DateTime.Now;
      ফ = 0;
      ڴ(ঌ.ڢ, ঌ.Ƣ, ঌ.ڣ, false);
    }
    Vector3I থ = new Vector3I((int) ঌ.ଢ.X, (int) ঌ.ଢ.Y, (int) ঌ.ଢ.Z);
    IMySlimBlock ত = Me.CubeGrid.GetCubeBlock(থ);
    double ণ = dockingSpeed;
    double ঢ = dockingSpeed * 5;
    double ড = Math.Max(1.5 f, Math.Min(5 f, ਠ * 0.15 f));
    if (!ଚ(ঌ, 0, true, out Ȉ) || !ଚ(ঌ, ড, true, out ਞ) || ত == null || !ত.FatBlock.IsFunctional) {
      ޅ(ދ.މ);
      ఙ();
      return;
    }
    if (ਝ == 1 || (Vector3D.Distance(ࡏ, Ȉ) <= ড * 1.1 f && !র)) ਝ = 1;
    else {
      Vector3D ঠ = ܐ(ਸ਼, ਞ - ࡏ);
      Vector3D ট =
        ܐ(ਸ਼, ਸ਼.GetNaturalGravity());
      double ঞ = ǜ(ঠ, ট, null);
      ণ = Math.Min(ঢ, ঞ);
    }
    ڌ(true, false, false, Ȉ, Ȉ - ࡏ, dockingSpeed, ণ);
    if (র) ਟ = (double)
    ম;
    IMyShipConnector Ƙ = b(MyShipConnectorStatus.Connectable);
    if (Ƙ != null) {
      ڌ(false, false, false, Ȉ, 0);
      if (ফ > 0) ফ = 0;
      ফ--;
      if (ফ < -5) {
        Ƙ.
        Connect();
        if (Ƙ.Status == MyShipConnectorStatus.Connected) {
          if (ઠ == થ.ކ) வ(ஒ.ஸ);
          else வ(ஒ.ୟ);
          گ();
          n(ਮ, true);
          return;
        }
      }
    } else {
      double ܔ = (
        double) Math.Round(ম, 1);
      if (ܔ < ਟ) {
        ফ = -1;
        ਟ = ܔ;
      } else ফ++;
      if (ফ > 20) {
        வ(ஒ.ୠ);
        return;
      }
    }
  }
  if (ஷ == ஒ.ୟ || ஷ == ஒ.ஸ || ஷ == ஒ.ஔ || ஷ == ஒ.ହ || ஷ == ஒ.ଽ) {
    bool ঝ =
      false;
    bool জ = false;
    if (ઠ == થ.ކ) {
      if (ଙ() == ଷ) ঝ = true;
      else if (ଙ() == ا) জ = true;
    }
    if (র) {
      ƫ = false;
      if (b(MyShipConnectorStatus.Connected) ==
        null) {
        வ(ஒ.ଢ଼);
        return;
      }
      گ();
      if (ঝ) ߜ(ߴ.Ԧ);
      if (জ) ߜ(ߪ.Ԧ);
      ਗ = false;
      ਔ = false;
      ਕ = false;
      ਖ = false;
    }
    if (b(MyShipConnectorStatus.Connected) ==
      null) {
      ఙ();
      ޅ(ދ.އ);
      return;
    }
    if (ధ != ఏ.ఋ || ޠ == -1 || Ս == ન.ۀ) ਔ = true;
    else if (Վ >= 100 f) ਔ = true;
    else if (Վ <= 99 f) ਔ = false;
    if (ధ != ఏ.ఋ || ޞ == -1 || ਫ.Count == 0) ਕ = true;
    else if (Ӈ >= 100 f) ਕ = true;
    else if (Ӈ <= 99) ਕ = false;
    if (ధ != ఏ.ఋ || ޟ == -1 || ਭ.Count == 0) ਖ = true;
    else ਖ = ԝ >= ޟ;
    Գ ƺ = null;
    if (ঝ) ƺ =
      ߴ;
    if (জ) ƺ = ߪ;
    if (ƺ != null && (ƺ.Բ == Կ.Ը || ƺ.Բ == Կ.Է)) ਔ = true;
    if (ƺ != null && (ƺ.Բ == Կ.Թ))
      if (!ਗ) ਔ = false;
    if (ƺ != null && (ƺ.Բ == Կ.Ե || ƺ.Բ == Կ.Դ))
      ਕ = true;
    if (ƺ != null && (ƺ.Բ == Կ.Զ))
      if (!ਗ) ਕ = false;
    if (ࡆ) {
      ChargeMode ছ = ਔ ? ChargeMode.Auto : ChargeMode.Recharge;
      if (ƺ != null && (ƺ.Բ == Կ.Է || ƺ.Բ == Կ.Ը)) ছ = ChargeMode.Discharge;
      p(ছ);
      ǋ(!ਕ);
    }
    if (!ਗ) {
      if (ઠ == થ.ކ) ਗ = ధ != ఏ.ఋ || ư(র, true) || ग;
      else ਗ = ధ != ఏ.ఋ || ǂ();
    } else {
      if (!ਔ) வ(
        ஒ.ଽ);
      if (!ਕ) வ(ஒ.ହ);
      if (!ਖ) வ(ஒ.ஔ);
      র = false;
    }
    if (ਗ && ਔ && ਕ && ਖ) {
      p(ChargeMode.Auto);
      ǋ(false);
      if (ధ == ఏ.ఋ) {
        if (ઠ == થ.ކ) {
          if (ଙ() == ଷ) ߜ(ߴ.ԥ);
          else if (ଙ() == ا) ߜ(ߪ.ԥ);
          if (ଙ() == ଷ) ર(ا, ఏ.ఆ);
          else ર(ଷ, ఏ.ఔ);
        }
      }
      வ(ஒ.ଢ଼);
      return;
    }
  }
  if (ஷ == ஒ.ଢ଼) {
    if (র) {
      IMyShipConnector Ƙ = b(
        MyShipConnectorStatus.Connected);
      if (Ƙ == null) {
        வ(ஒ.இ);
        return;
      }
      IMyShipConnector ঙ = Ƙ.OtherConnector;
      R(Ƙ, false);
      n(ਮ, false);
      ଥ ř = null;
      if (Vector3D.Distance(Ƙ.GetPosition(), ଷ.ࡏ) < 5 f && ଷ.ଣ) ř = ଷ;
      if (Vector3D.Distance(Ƙ.GetPosition(), ا.ࡏ) < 5 f && ا.ଣ) ř = ا;
      if (ř != null) {
        if (!ଚ(ř, dockDist * ਠ,
            true, out Ȉ)) {
          ޅ(ދ.މ);
          ఙ();
          return;
        }
        ڌ(Ȉ, 5);
        ڴ(ř.ڢ, ř.Ƣ, ř.ڣ, false);
      } else ڌ(ࡏ + ঙ.WorldMatrix.Forward * dockDist * ਠ, 5);
      if (ధ == ఏ.ఋ) ޅ(ދ.ފ);
    }
    if (ম < wpReachedDist) {
      R(ਲ, true);
      வ(ஒ.இ);
      return;
    }
  }
  if (ஷ == ஒ.ஆ) {
    if (র) {
      r(true);
      R(ਯ, false);
      Ȉ = ا.ࡏ;
      ڌ(Ȉ, 20);
      ڴ(ا.ڢ, ا.Ƣ, ا.ڣ, false);
    }
    if (ম <
      wpReachedDist / 2) {
      வ(ஒ.ୡ);
      return;
    }
  }
  র = false;
}
void ਹ(List < ଥ > ટ, int ৱ, List < Vector3D > ઝ, double ݛ, bool w, ref int ݪ) {
  if (w) {
    for (int z = 0; z < ટ.Count; z++) ટ[z].ଡ = 0;
    ݪ = -1;
    return;
  }
  if (ৱ == 0) return;
  int જ = ৱ * -1;
  if (ݪ == -1) ݪ = જ > 0 ? 1 : ટ.Count - 2;
  int Ǎ = 0;
  while (ݪ >= 1 && ݪ < ટ.Count - 1) {
    if (Ǎ > 50) return;
    Ǎ++;
    try {
      if ((જ < 0 && ݪ >= 1) || (જ > 0 && ݪ <= ટ.Count - 2)) {
        ଥ ݓ = ટ[ݪ];
        bool છ = false;
        for (int ݘ = 0; ݘ < ઝ.Count; ݘ++) {
          if (
            Vector3D.Distance(ݓ.ࡏ, ઝ[ݘ]) <= ݛ) {
            છ = true;
            break;
          }
        }
        if (!છ) {
          ଥ ચ = ટ[ݪ - જ];
          ଥ ઙ = ટ[ݪ + જ];
          Vector3D ઘ = ݓ.ࡏ - ઙ.ࡏ;
          Vector3D ગ = ચ.ࡏ - ݓ.ࡏ;
          Vector3D ખ = ݓ.ࡏ +
            Vector3D.Normalize(ઘ) * ગ.Length();
          Vector3D ક = ચ.ࡏ - ખ;
          Vector3D ઔ = ܞ(ৱ > 0 ? ݓ.Ƣ : ݓ.Ƣ * -1, ݓ.ڢ * -1, ક);
          Vector3D ઓ = ܞ(ৱ > 0 ? ݓ.Ƣ : ݓ.Ƣ * -1, ݓ.ڢ *
            -1, ગ);
          Vector3D ઑ = ܞ(ৱ > 0 ? ݓ.Ƣ : ݓ.Ƣ * -1, ݓ.ڢ * -1, ݓ.ơ);
          ݓ.ଡ = (double) Math.Sqrt(Math.Pow(ચ.ଡ, 2) + Math.Pow(ǜ(-ઓ, ઑ, ݓ), 2));
          for (int ݘ = 0; ݘ <
            ઝ.Count; ݘ++)
            if (Vector3D.Distance(ચ.ࡏ, ઝ[ݘ]) <= ݛ) {
              Vector3D ઞ = ܞ(ৱ > 0 ? ݓ.Ƣ : ݓ.Ƣ * -1, ݓ.ڢ * -1, ઝ[ݘ] - ݓ.ࡏ);
              double ਵ = ǜ(-ઞ, ઑ, ݓ);
              ݓ.ଡ = Math.
              Min(ݓ.ଡ, ਵ) / 2 f;
            } if (ઔ.Length() == 0) ઔ = new Vector3D(0, 0, 1);
          Vector3D ભ = ܞ(ݓ.Ƣ, ݓ.ڢ * -1, ݓ.ơ);
          double y = ɦ(ઔ, ભ, ݓ);
          double ǵ = ǹ(y, फ़);
          double
          ĵ = (double) Math.Sqrt(ઔ.Length() * 1.0 f / (0.5 f * ǵ));
          ݓ.ଡ = Math.Min(ݓ.ଡ, (ગ.Length() / ĵ) * ޝ);
        }
      }
    } catch {
      return;
    }
    ݪ += જ;
  }
  ݪ = -1;
}
void બ(bool w) {
  if (w) {
    ణ = 0;
    return;
  }
  if (ઠ == થ.ކ) return;
  double ݓ = স * Math.Max(1, ߤ);
  if (ষ == স) ݓ += Math.Min(ߤ, ਚ);
  double Ч = థ * త * Math.Max(1, ߤ);
  ణ = Math
    .Max(ణ, (double) Math.Min(ݓ / Ч * 100.0, 100));
}
MyDetectedEntityType ફ() {
  try {
    if (ԡ(ਵ, true) && !ਵ.LastDetectedEntity.IsEmpty())
      return ਵ.LastDetectedEntity.Type;
  } catch {};
  return MyDetectedEntityType.None;
}
double પ(bool Ƣ) {
  if (ઠ == થ.ઢ && ફ() ==
    MyDetectedEntityType.None && !ӈ(ਵ, true)) return fastSpeed;
  else return Ƣ ? ޜ : ޛ;
}
public enum ન {
  ۀ,
  o,
  ધ,
  દ
}
public enum થ {
  ત,
  ણ,
  ઢ,
  ડ,
  ކ
}
થ ઠ = થ.ત;
int ڿ = 0;
double ઐ = 0;
double એ = 0;
float ਠ = 0;
IMyRemoteControl ਸ਼ = null;
IMySensorBlock ਵ;
List < IMyTimerBlock > ਲ਼ = new List < IMyTimerBlock > ();
List <
  IMyShipConnector > ਲ = new List < IMyShipConnector > ();
List < IMyThrust > ਰ = new List < IMyThrust > ();
List < IMyTerminalBlock > ਯ = new List < IMyTerminalBlock >
  ();
List < IMyLandingGear > ਮ = new List < IMyLandingGear > ();
List < IMyReactor > ਭ = new List < IMyReactor > ();
List < IMyConveyorSorter > ਬ =
  new List < IMyConveyorSorter > ();
List < IMyGasTank > ਫ = new List < IMyGasTank > ();
List < IMyTerminalBlock > ਪ = new List < IMyTerminalBlock > ();
List < IMyTerminalBlock > ਨ = new List < IMyTerminalBlock > ();
List < IMyTerminalBlock > ਧ = new List < IMyTerminalBlock > ();
List <
  IMyBatteryBlock > ਦ = new List < IMyBatteryBlock > ();
List < IMyTextPanel > ਥ = new List < IMyTextPanel > ();
List < IMyTextSurface > Ƥ = new List <
  IMyTextSurface > ();
List < IMyTextPanel > ਤ = new List < IMyTextPanel > ();
IMyTerminalBlock ਣ = null;
bool ਢ(IMyTerminalBlock m) => m.CubeGrid == Me.
CubeGrid;
void ਡ() {
  उ.GetBlocksOfType(ਪ, ਢ);
}
void ਸ() {
  ਤ.Clear();
  for (int c = ਥ.Count - 1; c >= 0; c--) {
    String ڔ = ਥ[c].CustomData.ToUpper();
    bool ઍ = false;
    if (ڔ == ڝ) {
      ઍ = true;
      घ = true;
    }
    if (ڔ == ڜ) ઍ = true;
    if (ઍ) {
      ਤ.Add(ਥ[c]);
      ਥ.RemoveAt(c);
    }
  }
  ǉ(ਤ, false, 1, false);
}
void ઌ(List <
  IMyTerminalBlock > ƥ) {
  Ƥ.Clear();
  for (int c = 0; c < ƥ.Count; c++) {
    IMyTerminalBlock m = ƥ[c];
    try {
      String ઋ = pamTag.Substring(0, pamTag.Length - 1) + ":";
      int ɧ = m.CustomName.IndexOf(ઋ);
      int ઊ = -1;
      if (ɧ < 0 || !int.TryParse(m.CustomName.Substring(ɧ + ઋ.Length, 1), out ઊ)) continue;
      if (ઊ == -1)
        continue;
      ઊ--;
      IMyTextSurfaceProvider ઉ = (IMyTextSurfaceProvider) m;
      if (ઊ < ઉ.SurfaceCount && ઊ >= 0) {
        Ƥ.Add(ઉ.GetSurface(ઊ));
      }
    } catch {}
  }
}
void ઈ() {
  if (ਸ਼ == null) return;
  ਣ = null;
  double ઇ = 0, આ = 0, અ = 0, ੴ = 0, ੳ = 0, ੲ = 0;
  List < IMyTerminalBlock > ਫ਼ = ң(ਯ, pamTag, true);
  bool ੜ = ਫ਼.Count == 0;
  if (ਫ਼.Count > 0) ਣ = ਫ਼[0];
  else if (ਯ.Count > 0) ਣ = ਯ[0];
  int Ǎ = 0;
  for (int c = 0; c < ਯ.Count; c++) {
    if (ਯ[c].WorldMatrix.Forward != ਣ.WorldMatrix
      .Forward) {
      if (ੜ) {
        ڿ = 2;
        ܭ = "Mining direction is unclear!";
        return;
      }
      continue;
    }
    Ǎ++;
    Vector3D ਜ਼ = ۏ(ਸ਼, ਯ[c].GetPosition());
    if (c == 0) {
      ઇ =
        ਜ਼.X;
      આ = ਜ਼.X;
      અ = ਜ਼.Y;
      ੴ = ਜ਼.Y;
      ੳ = ਜ਼.Z;
      ੲ = ਜ਼.Z;
    }
    આ = Math.Max(ਜ਼.X, આ);
    ઇ = Math.Min(ਜ਼.X, ઇ);
    ੴ = Math.Max(ਜ਼.Y, ੴ);
    અ = Math.Min(ਜ਼.Y, અ);
    ੲ = Math.Max(ਜ਼.Z, ੲ);
    ੳ = Math.Min(ਜ਼.Z, ੳ);
  }
  ઐ = (((આ - ઇ) * (1 - ޚ) + drillRadius * 2));
  એ = (ੴ - અ) * (1 - ޙ) + drillRadius * 2;
  if (ਣ != null && ਣ.WorldMatrix.Forward == ਸ਼.WorldMatrix.Down) એ = (ੲ - ੳ) * (1 - ޙ) + drillRadius * 2;
}
void ਗ਼() {
  if (ڹ) {
    ڿ = 2;
    return;
  }
  List < IMyRemoteControl > ਖ਼ = new List < IMyRemoteControl > ();
  List <
    IMySensorBlock > ޢ = new List < IMySensorBlock > ();
  List < IMyTerminalBlock > ܟ = new List < IMyTerminalBlock > ();
  उ.GetBlocksOfType(ਖ਼, ਢ);
  उ.
  GetBlocksOfType(ਥ, ਢ);
  उ.GetBlocksOfType(ޢ, ਢ);
  उ.SearchBlocksOfName(pamTag.Substring(0, pamTag.Length - 1) + ":", ܟ, m => m.CubeGrid == Me.CubeGrid &&
    m is IMyTextSurfaceProvider);
  ਥ = ң(ਥ, pamTag, true);
  ਸ();
  ઌ(ܟ);
  ǉ(ਥ, setLCDFontAndSize, 1.4 f, false);
  ǉ(Ƥ, setLCDFontAndSize, 1.4 f,
    true);
  List < IMySensorBlock > ҡ = ң(ޢ, pamTag, true);
  if (ҡ.Count > 0) ਵ = ҡ[0];
  else ਵ = null;
  if (ઠ == થ.ણ) {
    उ.GetBlocksOfType(ਯ, m => m.CubeGrid ==
      Me.CubeGrid && m is IMyShipDrill);
    if (ਯ.Count == 0) {
      ڿ = 1;
      ܭ = "Drills are missing";
    }
  } else if (ઠ == થ.ઢ) {
    उ.GetBlocksOfType(ਯ, m => m.CubeGrid == Me.CubeGrid && m is IMyShipGrinder);
    if (ਯ.Count == 0) {
      ڿ = 1;
      ܭ = "Grinders are missing";
    }
    if (ઠ == થ.ઢ && ਵ == null) {
      ڿ = 1;
      ܭ =
        "Sensor is missing";
    }
  } else if (ઠ == થ.ކ) {
    उ.GetBlocksOfType(ਲ਼, m => m.CubeGrid == Me.CubeGrid);
  }
  List < IMyRemoteControl > ҝ = ң(ਖ਼, pamTag, true);
  if (ҝ.Count >
    0) ਖ਼ = ҝ;
  if (ਖ਼.Count > 0) ਸ਼ = ਖ਼[0];
  else {
    ਸ਼ = null;
    ڿ = 2;
    ܭ = "Remote is missing";
    return;
  }
  ਠ = (float)(ਸ਼.CubeGrid.WorldVolume.Radius * 2);
  ਣ = null;
  if (ઠ != થ.ކ) {
    ઈ();
    if (ਯ.Count > 0 && ਣ != null) {
      if (ਵ != null && (ਣ.WorldMatrix.Forward != ਵ.WorldMatrix.Forward || !(ਸ਼.WorldMatrix.Forward ==
          ਵ.WorldMatrix.Up || ਸ਼.WorldMatrix.Down == ਵ.WorldMatrix.Down))) {
        ڿ = 1;
        ܭ = "Wrong sensor direction";
      }
      if (ਣ.WorldMatrix.Forward != ਸ਼.WorldMatrix.Forward && ਣ.WorldMatrix.Forward != ਸ਼.WorldMatrix.Down) {
        ڿ = 2;
        ܭ = "Wrong remote direction";
      }
    }
  }
}
void ҍ() {
  उ.GetBlocksOfType(ਮ, ਢ);
  उ.GetBlocksOfType(ਲ, ਢ);
  उ.GetBlocksOfType(ਰ, ਢ);
  उ.GetBlocksOfType(अ, ਢ);
  उ.GetBlocksOfType(ਦ, ਢ);
  उ.GetBlocksOfType(ਭ, ਢ);
  उ.
  GetBlocksOfType(ਫ, m => m.CubeGrid == Me.CubeGrid && m.BlockDefinition.ToString().ToUpper().Contains("HYDROGEN"));
  उ.GetBlocksOfType(ਬ, ਢ);
  if (Me
    .CubeGrid.GridSizeEnum == MyCubeSize.Small) ਲ = ң(ਲ, "ConnectorMedium", false);
  else ਲ = ң(ਲ, "Connector", false);
  List <
    IMyShipConnector > Ҋ = ң(ਲ, pamTag, true);
  if (Ҋ.Count > 0) ਲ = Ҋ;
  if (ڿ <= 1) {
    if (ਲ.Count == 0) {
      ڿ = 1;
      ܭ = "Connector is missing";
    }
    if (अ.Count == 0) {
      ڿ = 1;
      ܭ =
        "Gyros are missing";
    }
    if (ਰ.Count == 0) {
      ڿ = 1;
      ܭ = "Thrusters are missing";
    }
  }
  List < IMyConveyorSorter > ҁ = ң(ਬ, pamTag, true);
  if (ҁ.Count > 0) ਬ = ҁ;
  List <
    IMyLandingGear > ѿ = ң(ਮ, pamTag, true);
  if (ѿ.Count > 0) ਮ = ѿ;
  for (int c = 0; c < ਮ.Count; c++) ਮ[c].AutoLock = false;
  List < IMyBatteryBlock > ѵ = ң(ਦ, pamTag,
    true);
  if (ѵ.Count > 0) ਦ = ѵ;
  List < IMyGasTank > Ѧ = ң(ਫ, pamTag, true);
  if (Ѧ.Count > 0) ਫ = Ѧ;
}
void ј() {
  उ.GetBlocksOfType(ਧ, m => m.CubeGrid == Me.CubeGrid && m.InventoryCount > 0);
  ਨ.Clear();
  for (int c = ਧ.Count - 1; c >= 0; c--) {
    if (ь(ਧ[c])) {
      ਨ.Add(ਧ[c]);
      ਧ.RemoveAt(c);
    }
  }
}
bool э(
  IMyTerminalBlock m) {
  if (m.InventoryCount == 0) return false;
  if (ઠ == થ.ކ) return true;
  for (int z = 0; z < ਯ.Count; z++) {
    IMyTerminalBlock ĵ = ਯ[z];
    if (ĵ ==
      null || !ԡ(ĵ, true) || ĵ.InventoryCount == 0) continue;
    if (!checkConveyorSystem || ĵ.GetInventory(0).IsConnectedTo(m.GetInventory(0))) {
      return true;
    }
  }
  return false;
}
bool ь(IMyTerminalBlock m) {
  if (m is IMyCargoContainer) return true;
  if (m is IMyShipDrill) return true;
  if (m is IMyShipGrinder) return true;
  if (m is IMyShipConnector) {
    if (((IMyShipConnector) m).ThrowOut) return false;
    if (Me.CubeGrid
      .GridSizeEnum != MyCubeSize.Large && Ұ(m, "ConnectorSmall", false)) return false;
    else return true;
  }
  return false;
}
List < ǈ > ң < ǈ > (
  List < ǈ > ӆ, String Ӄ, bool ҭ) {
  List < ǈ > ұ = new List < ǈ > ();
  for (int c = 0; c < ӆ.Count; c++)
    if (Ұ(ӆ[c], Ӄ, ҭ)) ұ.Add(ӆ[c]);
  return ұ;
}
bool Ұ < ǈ > (ǈ ү, String Ү, bool ҭ) {
  IMyTerminalBlock m = (IMyTerminalBlock) ү;
  if (ҭ && m.CustomName.ToUpper().Contains(Ү.ToUpper())) return true;
  if (!ҭ && m.BlockDefinition.ToString().ToUpper().Contains(Ү.ToUpper())) return true;
  return false;
}
Dictionary < String, double > Ҭ =
  new Dictionary < String, double > ();
int ҫ = 0;
void Ҫ() {
  if (!ߡ) return;
  if (ਯ.Count <= 1) return;
  double ҩ = 0;
  double Ҩ = 0;
  for (int c = 0; c < ਯ.Count; c++) {
    IMyTerminalBlock Н = ਯ[c];
    if (ӈ(Н, true)) continue;
    ҩ += (double) Н.GetInventory(0).MaxVolume;
    Ҩ += (double) Н.GetInventory(0).
    CurrentVolume;
  }
  double ҧ = (double) Math.Round(ǹ(Ҩ, ҩ), 5);
  for (int c = 0; c < Math.Max(1, Math.Floor(ਯ.Count / 10 f)); c++) {
    double Ҧ = 0;
    double ҥ = 0;
    double Ҥ = 0;
    double в = 0;
    IMyTerminalBlock б = null;
    IMyTerminalBlock ɰ = null;
    for (int z = 0; z < ਯ.Count; z++) {
      IMyTerminalBlock Н = ਯ[z];
      if (ӈ(
          Н, true)) continue;
      double Л = (double) Н.GetInventory(0).MaxVolume;
      double ϕ = ǹ((double) Н.GetInventory(0).CurrentVolume, Л);
      if (б ==
        null || ϕ > Ҧ) {
        б = Н;
        Ҧ = ϕ;
        Ҥ = Л;
      }
      if (ɰ == null || ϕ < ҥ) {
        ɰ = Н;
        ҥ = ϕ;
        в = Л;
      }
    }
    if (б == null || ɰ == null || б == ɰ) return;
    if (checkConveyorSystem && !б.GetInventory(0).IsConnectedTo(ɰ.GetInventory(0))) {
      if (ҫ > 20) ܭ = "Inventory balancing failed";
      else ҫ++;
      return;
    }
    ҫ = 0;
    List < MyInventoryItem > ώ = new List < MyInventoryItem > ();
    б.GetInventory(0).GetItems(ώ);
    double ʱ = 0;
    if (ώ.Count == 0) continue;
    MyInventoryItem ύ = ώ[0];
    String ω = ύ.Type.TypeId + ύ.Type.SubtypeId;
    if (!Ҭ.TryGetValue(ω, out ʱ)) {
      if (ʴ(б.GetInventory(0), 0, ɰ.GetInventory(0), out ʱ)) {
        Ҭ.Add(ω, ʱ);
      } else {
        return;
      }
    }
    double χ = ((Ҧ - ҧ) * Ҥ / ʱ);
    double ʶ = ((ҧ - ҥ) * в / ʱ);
    int ʵ = (int) Math.Min(ʶ, χ);
    if (ʵ <= 0) return;
    if ((double) ύ.Amount <
      ʵ) б.GetInventory(0).TransferItemTo(ɰ.GetInventory(0), 0, null, null, ύ.Amount);
    else б.GetInventory(0).TransferItemTo(ɰ.GetInventory(0), 0, null, null, ʵ);
  }
}
bool ʴ(IMyInventory ʳ, int ɧ, IMyInventory ʲ, out double ʱ) {
  ʱ = 0;
  double ʰ = (double) ʳ.CurrentVolume;
  List <
    MyInventoryItem > ɵ = new List < MyInventoryItem > ();
  ʳ.GetItems(ɵ);
  double ɴ = 0;
  for (int c = 0; c < ɵ.Count; c++) ɴ += (double) ɵ[c].Amount;
  ʳ.
  TransferItemTo(ʲ, ɧ, null, null, 1);
  double ɳ = ʰ - (double) ʳ.CurrentVolume;
  ɵ.Clear();
  ʳ.GetItems(ɵ);
  double О = 0;
  for (int c = 0; c < ɵ.Count; c++) О += (
    double) ɵ[c].Amount;
  if (ɳ == 0 f || !ے(0.9999, ɴ - О, 1.0001)) {
    return false;
  }
  ʱ = ɳ;
  return true;
}
double Ь(IMyTerminalBlock Э, String Х, String Ф, String[] Ǘ) {
  double ƹ = 0;
  for (int z = 0; z < Э.InventoryCount; z++) {
    IMyInventory Ы = Э.GetInventory(z);
    List < MyInventoryItem > ώ = new
    List < MyInventoryItem > ();
    Ы.GetItems(ώ);
    foreach(MyInventoryItem ύ in ώ) {
      if (Ǘ != null && (Ǘ.Contains(ύ.Type.TypeId.ToUpper()) || Ǘ.Contains(ύ.Type.SubtypeId.ToUpper()))) continue;
      if ((Х == "" || ύ.Type.TypeId.ToUpper() == Х) && (Ф == "" || ύ.Type.SubtypeId.ToUpper() == Ф)) ƹ += (double) ύ.Amount;
    }
  }
  return ƹ;
}
public enum Ъ {
  Щ,
  Ш,
  Ч
}
class Ц {
  public String Х = "";
  public String Ф = "";
  public int ʵ = 0;
  public Ъ П
    = Ъ.Ч;
  public Ц(String Х, String Ф, int ʵ, Ъ П) {
    this.Х = Х;
    this.Ф = Ф;
    this.ʵ = ʵ;
    this.П = П;
  }
}
Ц Р(String Х, String Ф, Ъ П, bool Ն) {
  Х = Х.
  ToUpper();
  Ф = Ф.ToUpper();
  for (int c = 0; c < Ձ.Count; c++) {
    Ц ύ = Ձ[c];
    if (ύ.Х.ToUpper() == Х && ύ.Ф.ToUpper() == Ф && (ύ.П == П || П == Ъ.Ч)) return ύ;
  }
  Ц
  ƹ = null;
  if (Ն) {
    ƹ = new Ц(Х, Ф, 0, П);
    Ձ.Add(ƹ);
  }
  return ƹ;
}
int Յ(String Х, String Ф, Ъ П) {
  return Յ(Х, Ф, П, null);
}
int Յ(String Х,
  String Ф, Ъ П, String[] Ǘ) {
  int ʵ = 0;
  Х = Х.ToUpper();
  Ф = Ф.ToUpper();
  for (int c = 0; c < Ձ.Count; c++) {
    Ц ύ = Ձ[c];
    if (Ǘ != null && Ǘ.Contains(ύ.Х.ToUpper())) continue;
    if ((Х == "" || ύ.Х.ToUpper() == Х) && (Ф == "" || ύ.Ф.ToUpper() == Ф) && (ύ.П == П || П == Ъ.Ч)) ʵ += ύ.ʵ;
  }
  return ʵ;
}
double Մ = 0;
double Ճ = 0;
double Ղ = 0;
List < Ц > Ձ = new List < Ц > ();
void Շ(IMyTerminalBlock m, Ъ П) {
  for (int c = 0; c < m.InventoryCount; c++) {
    List <
      MyInventoryItem > Ք = new List < MyInventoryItem > ();
    m.GetInventory(c).GetItems(Ք);
    for (int z = 0; z < Ք.Count; z++) {
      Р(Ք[z].Type.SubtypeId, Ք[z].Type.TypeId.Replace("MyObjectBuilder_", ""), П, true).ʵ += (int) Ք[z].Amount;
    }
  }
}
void Փ(List < Ц > ƥ) {
  for (int Ւ = ƥ.Count - 1; Ւ > 0; Ւ--) {
    for (int c = 0; c < Ւ; c++) {
      Ц ǵ = ƥ[c];
      Ц m = ƥ[c + 1];
      if (ǵ.ʵ < m.ʵ) ƥ.Move(c, c + 1);
    }
  }
}
void Ց() {
  try {
    Ձ.Clear();
    for (int c = 0; c < ਨ.Count; c++) {
      IMyTerminalBlock m = ਨ[c];
      if (!ԡ(m, true)) continue;
      Շ(m, Ъ.Щ);
    }
    if (ߎ != ࠄ.ۀ) {
      for (int c = 0; c < ਧ.Count; c++) {
        IMyTerminalBlock m = ਧ[c];
        if (!ԡ(m, true))
          continue;
        Շ(m, Ъ.Ш);
      }
    }
    Փ(Ձ);
  } catch (Exception e) {
    ٿ = e;
  }
}
void Տ() {
  Ղ = 0;
  Ճ = 0;
  try {
    for (int c = 0; c < ਨ.Count; c++) {
      IMyTerminalBlock m = ਨ[c];
      if (!ԡ(m, true)) continue;
      Ճ += (double) m.GetInventory(0).CurrentVolume;
      Ղ += (double) m.GetInventory(0).MaxVolume;
    }
    Մ = (double) Math.Min(
      Math.Round(ǹ(Ճ, Ղ) * 100, 1), 100.0);
  } catch (Exception e) {
    ٿ = e;
  }
}
double Վ = 0;
ન Ս;
void Ռ() {
  double Ջ = 0, Պ = 0, Չ = 0, Ո = 0;
  for (int c = 0; c < ਦ.Count; c++) {
    IMyBatteryBlock m = ਦ[c];
    if (!ԡ(m, true)) continue;
    Ջ += m.MaxStoredPower;
    Պ += m.CurrentStoredPower;
    Չ += m.CurrentInput;
    Ո += m.
    CurrentOutput;
  }
  Վ = (double) Math.Round(ǹ(Պ, Ջ) * 100, 1);
  if (Չ >= Ո) Ս = ન.o;
  else Ս = ન.દ;
  if (Չ == 0 && Ո == 0 || Վ == 100.0) Ս = ન.ધ;
  if (ਦ.Count == 0) Ս = ન.ۀ;
}
double
Ӈ = 0;
void ԟ() {
  double Ԟ = 0;
  for (int c = 0; c < ਫ.Count; c++) {
    IMyGasTank m = ਫ[c];
    if (!ԡ(m, true)) continue;
    Ԟ += (double) m.FilledRatio;
  }
  Ӈ = ǹ(Ԟ, ਫ.Count) * 100 f;
}
double ԝ = 0;
String Ԝ = "";
void ԛ() {
  ԝ = 0;
  try {
    double Ԛ = 0;
    ࢤ.Clear();
    for (int ԙ = 0; ԙ < ਭ.Count; ԙ++) {
      IMyReactor Ԙ = ਭ[
        ԙ];
      List < MyInventoryItem > ԗ = new List < MyInventoryItem > ();
      Ԙ.GetInventory(0).GetItems(ԗ);
      for (int z = 0; z < ԗ.Count; z++) {
        MyInventoryItem Ԗ = ԗ[z];
        if (fuelList.Contains(Ԗ.Type.SubtypeId) && Ԗ.Type.TypeId.ToUpper().Contains("_INGOT")) {
          Ԛ += (double) Ԗ.Amount;
          Me.
          CustomData += $ "\n {Ԗ.Type.SubtypeId} added.";
          if (ࢤ.ContainsKey(Ԗ.Type.SubtypeId)) {
            ࢤ[Ԗ.Type.SubtypeId] += (double) Ԗ.Amount;
          } else {
            ࢤ.Add(
              Ԗ.Type.SubtypeId, (double) Ԗ.Amount);
          }
        }
      }
    }
    ԝ = Ԛ;
  } catch (Exception e) {
    ٿ = e;
  }
}
void ӌ() {
  if (ఢ) {
    if (ӊ().Count > ఛ) {
      ఢ = false;
      if (ߩ != ߝ.ࠅ) {
        ఙ();
        if (ߩ == ߝ.ࠆ) க();
        if (ߩ == ߝ.ߞ)
          if (ଷ.ଣ) ஓ();
          else க();
      }
      ܭ = "Damage detected";
    }
  }
}
bool Ӌ() {
  if (!ॠ) return true;
  if (ధ == ఏ.ఋ) {
    if (ޠ > 0 && Ս != ન.ۀ) {
      if (Վ <= ޠ) {
        ܭ = "Low energy! Move home";
        return false;
      }
    }
    if (ޟ > 0 && ਭ.Count > 0) {
      if (ԝ <= ޟ) {
        ܭ = "Low reactor fuel!";
        return false;
      }
    }
    if (
      ޞ > 0 && ਫ.Count > 0) {
      if (Ӈ <= ޞ) {
        ܭ = "Low hydrogen";
        return false;
      }
    }
  }
  return true;
}
List < IMyTerminalBlock > ӊ() {
  List < IMyTerminalBlock > Ӊ =
    new List < IMyTerminalBlock > ();
  for (int c = 0; c < ਪ.Count; c++) {
    IMyTerminalBlock m = ਪ[c];
    if (ӈ(m, false)) Ӊ.Add(m);
  }
  return Ӊ;
}
bool ӈ(
  IMyTerminalBlock Э, bool Ԡ) {
  return (!ԡ(Э, Ԡ) || !Э.IsFunctional);
}
bool ԡ(IMyTerminalBlock m, bool Ԡ) {
  if (m == null) return false;
  try {
    IMyCubeBlock
    Հ = Me.CubeGrid.GetCubeBlock(m.Position).FatBlock;
    if (Ԡ) return Հ == m;
    else return Հ.GetType() == m.GetType();
  } catch {
    return false;
  }
}
public enum Կ {
  Ծ,
  Խ,
  Լ,
  Ի,
  Ժ,
  Թ,
  Ը,
  Է,
  Զ,
  Ե,
  Դ
}
class Գ {
  public Կ Բ = Կ.Ծ;
  public double Ա = 0;
  public double ԧ = 0;
  public string Ԧ = "";
  public string ԥ = "";
  DateTime Ԥ;
  public bool ԣ = false;
  private bool Ԣ = false;
  public bool ɭ(bool w) {
    if (w) {
      ԧ = 0;
      Ԣ = false;
      return false;
    }
    Ԣ
      = true;
    return ԧ > Ա;
  }
  public void w() {
    ɭ(true);
    ԣ = false;
  }
  public void ǁ() {
    if (Ԣ)
      if ((DateTime.Now - Ԥ).TotalSeconds > 1) {
        ԧ++;
        Ԥ =
          DateTime.Now;
      }
  }
  public bool ǀ() {
    switch (Բ) {
    case Կ.Խ:
      return true;
    case Կ.Լ:
      return true;
    }
    return false;
  }
}
bool ƾ(Գ ƺ, bool w, bool Ʈ) {
  if (
    w) ƺ.w();
  ƺ.ǁ();
  bool ƹ = false;
  String ƶ = "";
  switch (ƺ.Բ) {
  case Կ.Ծ: {
    ƶ = "Waiting for command";
    ƹ = false;
    break;
  }
  case Կ.Ի: {
    ƶ =
      "Waiting for cargo";
    ƹ = ƪ(true);
    break;
  }
  case Կ.Ժ: {
    ƶ = "Unloading";
    ƹ = ǂ();
    break;
  }
  case Կ.Լ: {
    ƹ = true;
    break;
  }
  case Կ.Թ: {
    ƶ = "Charging batteries";
    ƹ = Վ >=
      100 f;
    break;
  }
  case Կ.Ը: {
    ƶ = "Discharging batteries";
    ƹ = Վ <= 25 f;
    break;
  }
  case Կ.Է: {
    ƶ = "Discharging batteries";
    ƹ = Վ <= 0 f;
    break;
  }
  case Կ.Զ: {
    ƶ = "Filling up hydrogen";
    ƹ = Ӈ >= 100 f;
    break;
  }
  case Կ.Ե: {
    ƶ = "Unloading hydrogen";
    ƹ = Ӈ <= 25 f;
    break;
  }
  case Կ.Դ: {
    ƶ =
      "Unloading hydrogen";
    ƹ = Ӈ <= 0 f;
    break;
  }
  case Կ.Խ: {
    bool Ƴ = Ƨ();
    if (!Ƴ) ƺ.ԣ = true;
    ƹ = ƺ.ԣ && Ƴ;
    ƶ = "Waiting for passengers";
    break;
  }
  }
  if (!ƹ) ƺ.ɭ(true);
  if (ƹ && ƺ.ǀ()) {
    ƹ = ƺ.ɭ(false);
    ƶ = "Undocking in: " + ט((int) Math.Max(0, ƺ.Ա - ƺ.ԧ));
  }
  if (Ʈ) Ʋ = ƶ;
  return ƹ;
}
String Ʋ = "";
bool ư(bool w, bool Ʈ) {
  IMyShipConnector ƭ = b(MyShipConnectorStatus.Connected);
  if (ƭ == null) return false;
  if (Vector3D.Distance(ଷ.ࡏ, ƭ.GetPosition()) < 5) return ƾ(ߴ, w, Ʈ);
  if (Vector3D.Distance(ا.ࡏ, ƭ.GetPosition()) < 5) return ƾ(ߪ, w, Ʈ);
  return false;
}
double Ƭ = 0;
bool ƫ = false;
bool ƪ(bool Ʃ) {
  if (ߢ &&
    ઠ != થ.ކ)
    if (Ƭ != -1 && फ़ >= Ƭ) {
      ܭ = "Ship too heavy";
      return true;
    } if (Մ >= ޏ || ƫ) {
    ƫ = false;
    ܭ = "Ship is full";
    return true;
  }
  return false;
}
bool Ƨ() {
  List < IMyCockpit > ƥ = new List < IMyCockpit > ();
  उ.GetBlocksOfType(ƥ, m => m.CubeGrid == Me.CubeGrid);
  for (int c = 0; c < ƥ.Count; c++)
    if (ƥ[c].IsUnderControl) return true;
  return false;
}
bool ǂ() {
  String[] Ǘ = null;
  if (!ߍ) Ǘ = new string[] {
    "ICE"
  };
  if (ઠ == થ.ણ) return Յ("", "ORE", Ъ.Щ, Ǘ) == 0;
  if (ઠ == થ.ઢ) return Յ("", "COMPONENT", Ъ.Щ, Ǘ) == 0;
  else return Յ("", "", Ъ.Щ, Ǘ) == 0;
}
void ǘ(bool Ǔ, bool Ǒ, float ǐ,
  float Ǐ) {
  if (ਵ == null || ਯ.Count == 0) return;
  Vector3 ǎ = new Vector3D();
  int Ǎ = 0;
  for (int c = 0; c < ਯ.Count; c++) {
    if (ਯ[c].WorldMatrix.Forward != ਣ.WorldMatrix.Forward) continue;
    Ǎ++;
    ǎ += ਯ[c].GetPosition();
  }
  ǎ = ǎ / Ǎ;
  Vector3 ǌ = ۏ(ਵ, ǎ);
  ਵ.Enabled = true;
  ਵ.ShowOnHUD = Ǔ;
  ਵ.
  LeftExtend = (float)((Ǒ ? 1 : ߦ) / 2 f * ઐ - ǌ.X);
  ਵ.RightExtend = (float)((Ǒ ? 1 : ߦ) / 2 f * ઐ + ǌ.X);
  ਵ.TopExtend = ((float)((Ǒ ? 1 : ߥ) / 2 f * એ + ǌ.Y));
  ਵ.
  BottomExtend = (float)((Ǒ ? 1 : ߥ) / 2 f * એ - ǌ.Y);
  ਵ.FrontExtend = (Ǔ ? ߤ : ǐ) - ǌ.Z;
  ਵ.BackExtend = Ǔ ? 0 : Ǐ + ਠ * 0.75 f + ǌ.Z;
  ਵ.DetectFloatingObjects = true;
  ਵ.
  DetectAsteroids = false;
  ਵ.DetectLargeShips = true;
  ਵ.DetectSmallShips = true;
  ਵ.DetectStations = true;
  ਵ.DetectOwner = true;
  ਵ.DetectSubgrids = false;
  ਵ
    .DetectPlayers = false;
  ਵ.DetectEnemy = true;
  ਵ.DetectFriendly = true;
  ਵ.DetectNeutral = true;
}
void R < ǈ > (List < ǈ > d, bool f) {
  for (int c =
    0; c < d.Count; c++) R((IMyTerminalBlock) d[c], f);
}
void ǋ(bool Ǌ) {
  for (int c = 0; c < ਫ.Count; c++) {
    ਫ[c].Stockpile = Ǌ;
  }
}
void ǉ < ǈ > (List <
  ǈ > d, bool Ǉ, float Ǆ, bool ǃ) {
  for (int c = 0; c < d.Count; c++) {
    IMyTextSurface Ƥ = null;
    if (d[c] is IMyTextSurface) Ƥ = (IMyTextSurface) d[
      c];
    if (Ƥ != null) {
      Ƥ.ContentType = ContentType.TEXT_AND_IMAGE;
      if (!Ǉ) continue;
      Ƥ.Font = "Debug";
      if (ǃ) continue;
      Ƥ.FontSize = Ǆ;
    }
  }
}
void
R(IMyTerminalBlock m, bool f) {
  if (m == null) return;
  String u = f ? "OnOff_On" : "OnOff_Off";
  var t = m.GetActionWithName(u);
  t.Apply(m);
}
bool s = true;
void r(bool f) {
  s = f;
  if (!ߟ) return;
  R(ਬ, f);
}
void p(ChargeMode o) {
  for (int c = 0; c < ਦ.Count; c++) ਦ[c].ChargeMode = o;
}
void n(List < IMyLandingGear > m, bool l) {
  for (int c = 0; c < m.Count; c++) {
    if (l) m[c].Lock();
    if (!l) m[c].Unlock();
  }
}
bool j = false;
void g(
  bool f) {
  if (j == f) return;
  List < IMyShipController > d = new List < IMyShipController > ();
  उ.GetBlocksOfType(d, ਢ);
  if (d.Count == 0) return;
  for (int c = 0; c < d.Count; c++) d[c].DampenersOverride = f;
  j = f;
}
IMyShipConnector b(MyShipConnectorStatus W) {
  for (int c = 0; c < ਲ.Count; c
    ++) {
    if (!ԡ(ਲ[c], true)) continue;
    if (ਲ[c].Status == W) return ਲ[c];
  }
  return null;
}
double v(Vector3D Ƣ, Vector3D ƣ, Vector3D ơ, ଥ ř) {
  if (ơ.Length() == 0 f) return 0;
  Vector3D Ơ = ܞ(Ƣ, ƣ, Vector3D.Normalize(ơ));
  double Ɵ = ɦ(-Ơ, ř);
  return Ɵ / ơ.Length();
}
int ƞ = 0;
ଥ Ɲ = null;
void Ɯ(bool w) {
  double ƚ = 0;
  double ƙ = 0.9 f;
  if (w) {
    Ƭ = -1;
    ƞ = 0;
    Ɲ = null;
    if (ధ != ఏ.ఎ && ا.ơ.Length() != 0) {
      ƚ = ƙ * v(ا.Ƣ, ا.ڢ * -1, ا.ơ, null);
      if (ƚ < Ƭ ||
        Ƭ == -1) Ƭ = ƚ;
    }
    if (ଷ.ଣ && ଷ.ơ.Length() != 0) {
      ƚ = ƙ * v(ଷ.Ƣ, ଷ.ڢ * -1, ଷ.ơ, null);
      if (ƚ < Ƭ || Ƭ == -1) Ƭ = ƚ;
    }
    return;
  }
  if (ƞ == -1) return;
  if (ƞ >= 0) {
    int Ƙ
      = 0;
    while (ƞ < ટ.Count) {
      if (Ƙ > 100) return;
      Ƙ++;
      ଥ ř = ટ[ƞ];
      if (ř.ơ.Length() != 0 f) {
        ƚ = ƙ * Math.Min(v(ř.Ƣ, ř.ڢ * -1, ř.ơ, ř), v(ř.Ƣ * -1, ř.ڢ * -1, ř.ơ, ř));
        if (ƚ < Ƭ || Ƭ == -1) Ƭ = ƚ;
      } else Ɲ = ř;
      ƞ++;
    }
    ƞ = -1;
  }
  bool ķ = true;
  double Ķ = 0;
  if (ટ.Count == 0 && Ƭ == -1) ķ = false;
  if (Ɲ != null) {
    for (int ĵ = 0; ĵ < ɇ.Count; ĵ++) {
      String ĳ = ɇ.Keys.ElementAt(ĵ);
      double[, ] á = ɇ.Values.ElementAt(ĵ);
      double Û = 0;
      if (!ɩ(Ɲ, ĳ, out Û)) {
        ķ = false;
        break;
      }
      for (int c = 0; c < á.GetLength(0); c++) {
        for (int z = 0; z < á.GetLength(1); z++) {
          double y = Math.Abs(á[c, z] * Û);
          if (y == 0) continue;
          ķ = true;
          if (
            Ķ == 0 || y < Ķ) Ķ = y;
        }
      }
    }
  }
  if (!ķ) {
    for (int c = 0; c < Ɉ.GetLength(0); c++) {
      for (int z = 0; z < Ɉ.GetLength(1); z++) {
        double y = Math.Abs(Ɉ[c, z]);
        if (y == 0) continue;
        if (Ķ == 0 || y < Ķ) Ķ = y;
      }
    }
  }
  if (Ķ > 0) {
    ƚ = ǹ(Ķ, Me.CubeGrid.GridSizeEnum == MyCubeSize.Small ? minAccelerationSmall :
      minAccelerationLarge);
    if (ƚ > 0)
      if (ƚ < Ƭ || Ƭ == -1) Ƭ = ƚ;
  }
}
void ɣ(bool ɢ, float ɠ, float ɝ, float ɐ, float ɏ) {
  for (int c = 0; c < अ.Count; c++) {
    IMyGyro Ɏ = अ[c];
    Ɏ.
    GyroOverride = ɢ;
    if (!ɢ) {
      try {
        Ɏ.GyroPower = ऄ[c];
      } catch {
        Ɏ.GyroPower = 99;
      }
    } else {
      Ɏ.GyroPower = ɠ;
    }
    if (!ɢ) continue;
    Vector3D Ƣ = ਸ਼.WorldMatrix.
    Forward;
    Vector3D ɍ = ਸ਼.WorldMatrix.Right;
    Vector3D ƣ = ਸ਼.WorldMatrix.Up;
    Vector3D Ɍ = Ɏ.WorldMatrix.Forward;
    Vector3D ɋ = Ɏ.WorldMatrix.Up;
    Vector3D Ɋ = Ɏ.WorldMatrix.Left * -1;
    if (Ɍ == Ƣ) Ɏ.SetValueFloat("Roll", ɏ);
    else if (Ɍ == (Ƣ * -1)) Ɏ.SetValueFloat("Roll", ɏ * -1);
    else
    if (ɋ == (Ƣ * -1)) Ɏ.SetValueFloat("Yaw", ɏ);
    else if (ɋ == Ƣ) Ɏ.SetValueFloat("Yaw", ɏ * -1);
    else if (Ɋ == Ƣ) Ɏ.SetValueFloat("Pitch", ɏ);
    else if (Ɋ == (Ƣ * -1)) Ɏ.SetValueFloat("Pitch", ɏ * -1);
    if (Ɋ == (ɍ * -1)) Ɏ.SetValueFloat("Pitch", ɝ);
    else if (Ɋ == ɍ) Ɏ.SetValueFloat("Pitch", ɝ * -1);
    else if (ɋ == ɍ) Ɏ.SetValueFloat("Yaw", ɝ);
    else if (ɋ == (ɍ * -1)) Ɏ.SetValueFloat("Yaw", ɝ * -1);
    else if (Ɍ == (ɍ * -1)) Ɏ.
    SetValueFloat("Roll", ɝ);
    else if (Ɍ == ɍ) Ɏ.SetValueFloat("Roll", ɝ * -1);
    if (ɋ == (ƣ * -1)) Ɏ.SetValueFloat("Yaw", ɐ);
    else if (ɋ == ƣ) Ɏ.SetValueFloat(
      "Yaw", ɐ * -1);
    else if (Ɋ == ƣ) Ɏ.SetValueFloat("Pitch", ɐ);
    else if (Ɋ == (ƣ * -1)) Ɏ.SetValueFloat("Pitch", ɐ * -1);
    else if (Ɍ == ƣ) Ɏ.
    SetValueFloat("Roll", ɐ);
    else if (Ɍ == (ƣ * -1)) Ɏ.SetValueFloat("Roll", ɐ * -1);
  }
}
double[, ] Ɉ = new double[3, 2];
Dictionary < String, double[, ] > ɇ = new
Dictionary < string, double[, ] > ();
void Ɇ(IMyTerminalBlock m) {
  if (m == null) return;
  Ɉ = new double[3, 2];
  ɇ = new Dictionary < string, double[, ] > ();
  for (int c = 0; c < ਰ.Count; c++) {
    IMyThrust ĵ = ਰ[c];
    if (!ĵ.IsFunctional) continue;
    Vector3D ǭ = ܐ(m, ĵ.WorldMatrix.Backward);
    double Ʉ =
      ĵ.MaxEffectiveThrust;
    if (Math.Round(ǭ.X, 2) != 0.0)
      if (ǭ.X >= 0) Ɉ[0, 0] += Ʉ;
      else Ɉ[0, 1] -= Ʉ;
    if (Math.Round(ǭ.Y, 2) != 0.0)
      if (ǭ.Y >= 0) Ɉ[1, 0] += Ʉ;
      else Ɉ[1, 1] -= Ʉ;
    if (Math.Round(ǭ.Z, 2) != 0.0)
      if (ǭ.Z >= 0) Ɉ[2, 0] += Ʉ;
      else Ɉ[2, 1] -= Ʉ;
    String ƶ = ɬ(ĵ);
    double[, ] ɤ = null;
    if (ɇ.ContainsKey(ƶ)) ɤ = ɇ[ƶ];
    else {
      ɤ = new double[3, 2];
      ɇ.Add(ƶ, ɤ);
    }
    double ɫ = ĵ.MaxThrust;
    if (Math.Round(ǭ.X, 2) != 0.0)
      if (ǭ.X >= 0) ɤ[0, 0] += ɫ;
      else ɤ[
        0, 1] -= ɫ;
    if (Math.Round(ǭ.Y, 2) != 0.0)
      if (ǭ.Y >= 0) ɤ[1, 0] += ɫ;
      else ɤ[1, 1] -= ɫ;
    if (Math.Round(ǭ.Z, 2) != 0.0)
      if (ǭ.Z >= 0) ɤ[2, 0] += ɫ;
      else ɤ[2, 1] -= ɫ;
  }
}
static String ɬ(IMyThrust ĵ) {
  return ĵ.BlockDefinition.SubtypeId;
}
Vector3D ɪ(Vector3D ɥ, double[, ] á) {
  return new
  Vector3D(ɥ.X >= 0 ? á[0, 0] : á[0, 1], ɥ.Y >= 0 ? á[1, 0] : á[1, 1], ɥ.Z >= 0 ? á[2, 0] : á[2, 1]);
}
bool ɩ(ଥ ř, String ɨ, out double Û) {
  Û = 0;
  int ɧ = ବ.IndexOf(
    ɨ);
  if (ɧ == -1 || ř.ଟ == null || ɧ >= ř.ଟ.Length) return false;
  Û = ř.ଟ[ɧ];
  if (Û == -1) return false;
  return true;
}
Vector3D Ɇ(Vector3D ɥ, ଥ ř) {
  if (ř != null) {
    Vector3D ƹ = new Vector3D();
    for (int c = 0; c < ɇ.Keys.Count; c++) {
      String ĳ = ɇ.Keys.ElementAt(c);
      double Û = 0;
      if (!ɩ(ř, ĳ,
          out Û)) {
        return ɪ(ɥ, Ɉ);
      }
      ƹ += ɪ(ɥ, ɇ.Values.ElementAt(c)) * Û;
    }
    return ƹ;
  }
  return ɪ(ɥ, Ɉ);
}
double ɦ(Vector3D ɥ, ଥ ř) {
  return ɦ(ɥ, new Vector3D(), ř);
}
double ɦ(Vector3D ɥ, Vector3D Ȁ, ଥ ř) {
  Vector3D ǲ = Ɇ(ɥ, ř);
  Vector3D Ǚ = ǲ + Ȁ * फ़;
  double Ǻ = (Ǚ / ɥ).AbsMin();
  return (double)(ɥ * Ǻ).Length();
}
static double ǹ(double ǵ, double m) {
  if (m == 0) return 0;
  return ǵ / m;
}
void Ǵ(Vector3D ǳ, bool f) {
  if (!f) {
    for (int c = 0; c < ਰ.Count; c++) ਰ[c].SetValueFloat("Override", 0.0 f);
    return;
  }
  Vector3D ǲ = Ɇ(ǳ, null);
  double Ǳ = ऊ == true && Math.Min(1, Math.Abs(ǹ(ǳ.X, ǲ.X))) > ޝ ? ޝ : Math.Min(1, Math.Abs(ǹ(ǳ.X, ǲ.X)));
  double ǰ = ऊ == true && Math.Min(1, Math.Abs(ǹ(ǳ.Y, ǲ.Y))) > ޝ ? ޝ : Math.Min(1, Math.Abs(
    ǹ(ǳ.Y, ǲ.Y)));
  double ǯ = ऊ == true && Math.Min(1, Math.Abs(ǹ(ǳ.Z, ǲ.Z))) > ޝ ? ޝ : Math.Min(1, Math.Abs(ǹ(ǳ.Z, ǲ.Z)));
  for (int c = 0; c < ਰ.Count; c++) {
    IMyThrust Ǯ = ਰ[c];
    Vector3D ǭ = ڑ(ܐ(ਸ਼, Ǯ.WorldMatrix.Backward), 1);
    if (ǭ.X != 0 && Math.Sign(ǭ.X) == Math.Sign(ǳ.X)) Ǯ.
    SetValueFloat("Override", (float)(Ǯ.MaxThrust * Ǭ(Ǳ)));
    else if (ǭ.Y != 0 && Math.Sign(ǭ.Y) == Math.Sign(ǳ.Y)) Ǯ.SetValueFloat("Override", (float)
      (Ǯ.MaxThrust * Ǭ(ǰ)));
    else if (ǭ.Z != 0 && Math.Sign(ǭ.Z) == Math.Sign(ǳ.Z)) Ǯ.SetValueFloat("Override", (float)(Ǯ.MaxThrust * Ǭ(ǯ)));
    else Ǯ.SetValueFloat("Override", 0.0 f);
  }
}
double Ǭ(double ǫ) {
  double Ǫ = ऊ == true ? ޝ : ǫ;
  return Ǫ;
}
double ǜ(Vector3D Ǜ, Vector3D ǚ, ଥ ř) {
  if (Ǜ.Length() == 0) return 0;
  double ƙ = 1;
  if (ǚ.Length() > 0) ƙ = Math.Min(1, ۿ(-ǚ, Ǜ) / 90) * 0.4 f + 0.6 f;
  double Ƀ = ɦ(Ǜ, ǚ, ř);
  if (Ƀ == 0)
    return 0.1 f;
  double ǵ = ǹ(Ƀ, फ़);
  double ĵ = (double) Math.Sqrt(ǹ(Ǜ.Length(), ǵ * 0.5 f));
  return ǵ * ĵ * ƙ * ޝ;
}
bool ɂ = false;
bool ɀ = false;
bool ȿ =
  false;
bool Ⱦ = false;
double Ȼ = 0;
double ȹ = 0;
Vector3D Ȥ = new Vector3D();
Vector3D Ȉ = new Vector3D();
Vector3D ȇ = new Vector3D(1, 1, 1);
double Ȇ = 1;
Vector3D ȅ = new Vector3D();
void Ȅ() {
  Vector3D ȃ = Ȉ - ࡏ;
  if (ȃ.Length() == 0) ȃ = new Vector3D(0, 0, -1);
  Vector3D Ȃ = ܐ(ਸ਼, ȃ);
  Vector3D ȁ = Vector3D.Normalize(Ȃ);
  Vector3D Ȁ = ܐ(ਸ਼, ਸ਼.GetNaturalGravity());
  double ǿ = ȹ > 0 ? Math.Max(0, 1 - ۿ(ȃ, Ȥ) / 5) : 0;
  double Ǿ = (double)
  Math.Min((Ȼ > 0 ? Ȼ : 1000 f), Math.Max(ǜ(-Ȃ, Ȁ, null), ȹ * ǿ));
  if (!ɂ) Ǿ = 0;
  if (ɀ) Ǿ = Math.Max(0, 1 - ڥ / ڤ) * Ǿ;
  if (generalSpeedLimit > 0) Ǿ = Math.Min(
    generalSpeedLimit, Ǿ);
  if (Ⱦ) Ǿ *= (double) Math.Min(1, ǹ(ȃ.Length(), wpReachedDist) / 2);
  Vector3D ǽ = ܐ(ਸ਼, ਸ਼.GetShipVelocities().LinearVelocity);
  double Ǽ = (double)(Math.Max(0, 15 - ۿ(-ȁ, -ǽ)) / 15) * 0.85 f + 0.15 f;
  Ȇ += Math.Sign(Ǽ - Ȇ) / 10 f;
  Vector3D Ր = ȁ * Ǿ * Ȇ - (ǽ);
  Vector3D б = Ɇ(Ր, null);
  if (ȿ &&
    ম > 0.1 f) {
    Ր.X *= ڬ(Ր.X, ref ȇ.X, 1 f, б.X, 20);
    Ր.Y *= ڬ(Ր.Y, ref ȇ.Y, 1 f, б.Y, 20);
    Ր.Z *= ڬ(Ր.Z, ref ȇ.Z, 1 f, б.Z, 20);
  } else ȇ = new Vector3D(1, 1, 1);
  ȅ = फ़ * Ր - Ȁ * फ़;
  Ǵ(ȅ, ȿ);
  ম = Vector3D.Distance(ࡏ, Ȉ);
}
double ڬ(double ǵ, ref double ڬ, double ګ, double б, double ڪ) {
  ǵ = Math.Sign(
    Math.Round(ǵ, 2));
  if (ǵ == Math.Sign(ڬ)) ڬ += Math.Sign(ڬ) * ګ;
  else ڬ = ǵ;
  if (ǵ == 0) ڬ = 1;
  double ƹ = Math.Abs(ڬ);
  if (ƹ < ڪ || б == 0) return ƹ;
  ڬ = Math
    .Min(ڪ, Math.Max(-ڪ, ڬ));
  ƹ = Math.Abs(б);
  return ƹ;
}
bool ک = false;
bool ڨ = false;
bool ڧ = false;
bool ڦ = false;
double ڥ = 0;
double ڤ = 2;
Vector3D ڣ;
Vector3D Ƣ;
Vector3D ڢ;
void ڡ() {
  double ɝ = 90;
  double ɏ = 90;
  double ɐ = 90;
  double ڠ = (double)(Me.CubeGrid.GridSizeEnum ==
    MyCubeSize.Small ? gyroSpeedSmall : gyroSpeedLarge) / 100 f;
  Vector3D ڟ;
  Vector3D ڞ;
  Vector3D ڭ;
  if (ڧ) {
    ڟ = Vector3D.Normalize(Ȉ - ࡏ);
    ڞ = ܐ(ਸ਼, ڟ);
    ڭ = ܐ(ਸ਼, ڢ);
    ɝ = ۿ(ڞ, new Vector3D(0, -1, 0)) - 90;
    ɏ = ۯ(ڭ, new Vector3D(-1, 0, 0), ڭ.Y);
    ɐ = ۯ(ڞ, new Vector3D(-1, 0, 0), ڞ.Z);
  } else {
    ڟ = Ƣ;
    ڭ = ܐ(ਸ਼, ڢ);
    ڞ
      = ܐ(ਸ਼, Ƣ);
    Vector3D ڮ = ܐ(ਸ਼, ڣ);
    ɝ = ۯ(ڭ, new Vector3D(0, 0, 1), ڭ.Y);
    ɏ = ۯ(ڭ, new Vector3D(-1, 0, 0), ڭ.Y);
    ɐ = ۯ(ڮ, new Vector3D(0, 0, 1), ڮ.X);
  }
  if (ڦ && ڇ()) {
    Vector3D ơ = ਸ਼.GetNaturalGravity();
    ڭ = ܐ(ਸ਼, ơ);
    ɝ = ۯ(ڭ, new Vector3D(0, 0, 1), ڭ.Y);
    ɏ = ۯ(ڭ, new Vector3D(-1, 0, 0), ڭ.Y);
  }
  if (!ے(-45, ɏ, 45)) {
    ɝ = 0;
    ɐ = 0;
  };
  if (!ے(-45, ɐ, 45)) ɝ = 0;
  ɣ(ڨ, 1, (float)((-ɝ) * ڠ), (float)((-ɐ) * ڠ), (float)((-ɏ) * ڠ));
  ڥ = Math.Max(Math.Abs(ɝ),
    Math.Max(Math.Abs(ɏ), Math.Abs(ɐ)));
  ک = ڥ <= ڤ;
}
void ڵ() {
  ڨ = false;
}
void ڴ(Vector3D ڢ, Vector3D Ƣ, Vector3D ڳ, double ڱ, bool ڰ) {
  ڲ(ڢ, ڱ,
    ڰ);
  ڤ = ڱ;
  ڧ = false;
  this.Ƣ = Ƣ;
  this.ڣ = ڳ;
}
void ڴ(Vector3D ڢ, Vector3D Ƣ, Vector3D ڳ, bool ڰ) {
  ڴ(ڢ, Ƣ, ڳ, 2 f, ڰ);
}
void ڲ(Vector3D ڢ, double ڱ, bool ڰ) {
  ڤ = ڱ;
  this.ڨ = true;
  this.ڦ = ڰ;
  ڧ = true;
  ک = false;
  this.ڢ = ڢ;
}
void گ() {
  ڌ(false, false, false, Ȉ, 0);
  ȿ = false;
}
void ڌ(Vector3D ڈ,
  double پ) {
  ڌ(true, false, false, ڈ, پ);
}
void ڌ(bool ڋ, bool ڊ, bool ډ, Vector3D ڈ, double پ) {
  ڌ(ڋ, ڊ, ډ, ڈ, ڈ - ࡏ, 0.0 f, پ);
}
void ڌ(bool ڋ, bool ڊ, bool ډ, Vector3D ڈ, Vector3D Ȥ, double ȹ, double پ) {
  ȿ = true;
  this.ɂ = ڋ;
  Ȉ = ڈ;
  this.Ȼ = پ;
  this.ȹ = ȹ;
  this.Ⱦ = ڊ;
  this.ɀ = ډ;
  this.Ȥ = Ȥ;
  ম =
    Vector3D.Distance(ڈ, ࡏ);
}
bool ڇ() {
  Vector3D چ;
  return ਸ਼.TryGetPlanetPosition(out چ);
}
Dictionary < String, double[] > څ = new Dictionary <
  string, double[] > ();
double ڄ;
void ڃ() {
  if (!घ) return;
  try {
    ڄ = Runtime.CurrentInstructionCount;
  } catch {}
}
void ڂ(String Х) {
  if (!घ) return;
  if (ڄ == 0) return;
  try {
    double ځ = (Runtime.CurrentInstructionCount - ڄ) / Runtime.MaxInstructionCount * 100;
    if (!څ.ContainsKey(Х)) څ.
    Add(Х, new double[] {
      ځ,
      ځ
    });
    else {
      څ[Х][0] = ځ;
      څ[Х][1] = Math.Max(ځ, څ[Х][1]);
    }
  } catch {}
}
string ڀ(double y) {
  return Math.Round(y, 2) + " ";
}
string ڀ(Vector3D y) {
  return "X" + ڀ(y.X) + "Y" + ڀ(y.Y) + "Z" + ڀ(y.Z);
}
Exception ٿ = null;
void ڍ() {
  String ƶ =
    "Error occurred! \nPlease copy this and paste it \nto in Steam.\n" + "Version: " + VERSION + "\n" + "PamPlus Version" + SenX_Version + "\n";
  ǉ(ਥ, setLCDFontAndSize, 0.9 f, false);
  ǉ(Ƥ, setLCDFontAndSize,
    0.9 f, true);
  for (int c = 0; c < ਥ.Count; c++) ਥ[c].WriteText(ƶ + ٿ.ToString());
  for (int c = 0; c < Ƥ.Count; c++) Ƥ[c].WriteText(ƶ + ٿ.ToString());
}
const String ڝ = "INSTRUCTIONS";
const String ڜ = "DEBUG";
String ڛ = "", ښ = "";
String ڙ = "";
void ژ() {
  String ڗ = "";
  String ږ = "";
  ڙ = ܣ(
    false);
  ڗ += ڙ;
  ږ += ڙ;
  ږ += ऱ();
  for (int c = 0; c < ਥ.Count; c++) ਥ[c].WriteText(ڗ);
  for (int c = 0; c < Ƥ.Count; c++) Ƥ[c].WriteText(ڗ);
  Echo(ږ);
  for (
    int c = 0; c < ਤ.Count; c++) {
    IMyTextPanel ڕ = ਤ[c];
    String ڔ = ڕ.CustomData.ToUpper();
    if (ڔ == ڜ) ڕ.WriteText(ڛ + "\n" + ښ);
    if (ڔ == ڝ) ڕ.
    WriteText(ړ());
  }
}
string ړ() {
  String ƶ = "";
  try {
    double ڒ = Runtime.MaxInstructionCount;
    ƶ += "Inst: " + Runtime.CurrentInstructionCount +
      " Time: " + Math.Round(Runtime.LastRunTimeMs, 3) + "\n";
    ƶ += "Inst. avg/max: " + (int)(ख़ * ڒ) + " / " + (int)(ग़ * ڒ) + "\n";
    ƶ += "Inst. avg/max: " +
      Math.Round(ख़ * 100 f, 1) + "% / " + Math.Round(ग़ * 100 f, 1) + "% \n";
    ƶ += "Time avg/max: " + Math.Round(ॐ, 2) + "ms / " + Math.Round(क़, 2) + "ms \n";
  } catch {}
  for (int c = 0; c < څ.Count; c++) {
    ƶ += "" + څ.Keys.ElementAt(c) + " = " + Math.Round(څ.Values.ElementAt(c)[0], 2) + " / " + Math.
    Round(څ.Values.ElementAt(c)[1], 2) + "%\n";
  }
  return ƶ;
}
Vector3D ڑ(Vector3D چ, int ڐ) {
  return new Vector3D(Math.Round(چ.X, ڐ), Math.Round(چ.Y, ڐ), Math.Round(چ.Z, ڐ));
}
Vector3D ڏ(Vector3D چ, double ڎ) {
  Vector3D ƹ = new Vector3D(Math.Sign(چ.X), Math.Sign(چ.Y), Math.Sign(چ.Z));
  ƹ.X = ƹ.X == 0.0 ? ڎ : ƹ.X;
  ƹ.Y = ƹ.Y == 0.0 ? ڎ : ƹ.Y;
  ƹ.Z = ƹ.Z == 0.0 ? ڎ : ƹ.Z;
  return ƹ;
}
double ۿ(Vector3D ۼ, Vector3D ۦ) {
  if (ۼ == ۦ) return
  0;
  double ڬ = (ۼ * ۦ).Sum;
  double ۻ = ۼ.Length();
  double ۺ = ۦ.Length();
  if (ۻ == 0 || ۺ == 0) return 0;
  double ƹ = (double)((180.0 f / Math.PI) *
    Math.Acos(ڬ / (ۻ * ۺ)));
  if (double.IsNaN(ƹ)) return 0;
  return ƹ;
}
double ۯ(Vector3D ۮ, Vector3D ۦ, double ۥ) {
  double ƹ = ۿ(ۮ, ۦ);
  if (ۥ > 0 f) ƹ *= -1;
  if (ƹ > -90 f) return ƹ - 90 f;
  else return 180 f - (-ƹ - 90 f);
}
double ە(double ۓ) {
  return (Math.PI / 180) * ۓ;
}
bool ے(double ɰ, double ǵ,
  double б) {
  return (ǵ >= ɰ && ǵ <= б);
}
Vector3D ۑ(IMyTerminalBlock m, Vector3D ې) {
  return Vector3D.Transform(ې, m.WorldMatrix);
}
Vector3D ۏ
  (IMyTerminalBlock m, Vector3D ێ) {
    return ܐ(m, ێ - m.GetPosition());
  }
Vector3D ܐ(IMyTerminalBlock m, Vector3D ܝ) {
  return Vector3D.
  TransformNormal(ܝ, MatrixD.Transpose(m.WorldMatrix));
}
Vector3D ܞ(Vector3D ܚ, Vector3D ܙ, Vector3D ܝ) {
  MatrixD ܜ = MatrixD.CreateFromDir(ܚ, ܙ);
  return Vector3D.TransformNormal(ܝ, MatrixD.Transpose(ܜ));
}
Vector3D ܛ(Vector3D ܚ, Vector3D ܙ, Vector3D Ǜ) {
  MatrixD ܜ = MatrixD.
  CreateFromDir(ܚ, ܙ);
  return Vector3D.Transform(Ǜ, ܜ);
}
String ܘ(Vector3D چ) {
  return "" + چ.X + "|" + چ.Y + "|" + چ.Z;
}
Vector3D ܗ(String ƶ) {
  String[] ܖ =
    ƶ.Split('|');
  return new Vector3D(double.Parse(ڻ(ܖ, 0)), double.Parse(ڻ(ܖ, 1)), double.Parse(ڻ(ܖ, 2)));
}
String ܕ(ଥ ř) {
  String ܔ =
    ":";
  String ƹ = ܘ(ř.ࡏ) + ܔ + ܘ(ř.Ƣ) + ܔ + ܘ(ř.ڢ) + ܔ + ܘ(ř.ڣ) + ܔ + ܘ(ř.ơ);
  for (int c = 0; c < ř.ଟ.Length; c++) {
    ƹ += ܔ;
    ƹ += Math.Round(ř.ଟ[c], 3);
  }
  return
  ƹ;
}
ଥ ܓ(String ܒ) {
  String[] ƶ = ܒ.Split(':');
  ଥ ƹ = new ଥ(ܗ(ڻ(ƶ, 0)), ܗ(ڻ(ƶ, 1)), ܗ(ڻ(ƶ, 2)), ܗ(ڻ(ƶ, 3)), ܗ(ڻ(ƶ, 4)));
  int c = 5;
  List < double >
    ƥ = new List < double > ();
  while (c < ƶ.Length) {
    String ھ = ڻ(ƶ, c);
    double y = 0;
    if (!double.TryParse(ھ, out y)) break;
    ƥ.Add(y);
    c++;
  }
  ƹ.ଟ = ƥ.
  ToArray();
  return ƹ;
}
void ڼ < ǈ > (ǈ ƶ, bool ڽ) {
  if (ڽ) Storage += "\n";
  Storage += ƶ;
}
void ڼ < ǈ > (ǈ ƶ) {
  ڼ(ƶ, true);
}
String ڻ(String[] ƶ, int c) {
  String ں = ƶ.ElementAtOrDefault(c);
  if (String.IsNullOrEmpty(ں)) return "";
  return ں;
}
bool ڹ = false;
void Save() {
  if (ڹ || ઠ == થ.ડ) {
    Storage =
      "";
    return;
  }
  Storage = DATAREV + ";";
  ڼ(ܘ(ࡐ), false);
  ڼ(ܘ(ଷ.Ƣ));
  ڼ(ܘ(ଷ.ڣ));
  ڼ(ܘ(ଷ.ڢ));
  ڼ(ܘ(ଷ.ơ));
  ڼ(ܘ(ଷ.ࡏ));
  ڼ(ܘ(ଷ.ଢ));
  ڼ(ଷ.ଣ);
  ڼ(ܘ(ا.ࡏ));
  ڼ(ܘ(ا.ơ));
  ڼ(ܘ(ا.Ƣ));
  ڼ(ܘ(ا.ڢ));
  ڼ(ܘ(ا.ڣ));
  ڼ(ܘ(ا.ଢ));
  ڼ(ا.ଣ);
  ڼ(ܘ(ఫ));
  ڼ(ܘ(న));
  ڼ(";");
  ڼ((int) ઠ, false);
  ڼ((int) ధ);
  ڼ((int) ஶ);
  ڼ(ޏ);
  ڼ(ޠ);
  ڼ(ޟ);
  ڼ(ޞ);
  ڼ(ޝ);
  ڼ(ߢ);
  ڼ(ߍ);
  if (ઠ == થ.ކ) {
    ڼ((int) ߴ.Բ);
    ڼ(ߴ.Ա);
    ڼ(ߴ.ԧ);
    ڼ(ߴ.Ԧ);
    ڼ(ߴ.ԥ);
    ڼ((int) ߪ.Բ);
    ڼ(ߪ.Ա);
    ڼ(ߪ.ԧ);
    ڼ(ߪ.Ԧ);
    ڼ(ߪ.ԥ);
  } else {
    ڼ((int) ߧ);
    ڼ((int) ߩ);
    ڼ((int) ߎ);
    ڼ((int) ߣ);
    ڼ((int) ద);
    ڼ(ߨ);
    ڼ(ߟ);
    ڼ(ߡ);
    ڼ(ߠ);
    ڼ(ߦ);
    ڼ(ߥ);
    ڼ(ߤ);
    ڼ(ޜ);
    ڼ(ޛ);
    ڼ(ޚ);
    ڼ(ޙ);
    ڼ(థ);
    ڼ(త);
    ڼ(ঽ);
    ڼ(হ);
    ڼ(ষ);
    ڼ(স);
    ڼ(শ);
    ڼ(ਙ);
  }
  ڼ(";");
  for (int c = 0; c < ବ.Count; c++) ڼ((c > 0 ? "|" : "") + ବ[c], false);
  ڼ(";");
  for (int c = 0; c < ટ.Count; c
    ++) ڼ(ܕ(ટ[c]), c > 0);
}
ఏ ڸ = ఏ.ఎ;
public enum ڷ {
  ڶ,
  ڿ,
  ۀ,
  ǁ
}
ڷ ۍ() {
  if (Storage == "") return ڷ.ۀ;
  String[] ی = Storage.Split(';');
  if (ڻ(ی, 0) !=
    DATAREV) {
    return ڷ.ǁ;
  }
  int c = 0;
  try {
    String[] ƶ = ڻ(ی, 1).Split('\n');
    ࡐ = ܗ(ڻ(ƶ, c++));
    ଷ.Ƣ = ܗ(ڻ(ƶ, c++));
    ଷ.ڣ = ܗ(ڻ(ƶ, c++));
    ଷ.ڢ = ܗ(ڻ(ƶ, c++));
    ଷ.ơ = ܗ(ڻ(ƶ, c++));
    ଷ.ࡏ = ܗ(ڻ(ƶ, c++));
    ଷ.ଢ = ܗ(ڻ(ƶ, c++));
    ଷ.ଣ = bool.Parse(ڻ(ƶ, c++));
    ا.ࡏ = ܗ(ڻ(ƶ, c++));
    ا.ơ = ܗ(ڻ(ƶ, c++));
    ا.Ƣ = ܗ(ڻ(ƶ, c++));
    ا.ڢ = ܗ(ڻ(ƶ, c++));
    ا.ڣ = ܗ(ڻ(ƶ, c++));
    ا.ଢ = ܗ(ڻ(ƶ, c++));
    ا.ଣ = bool.Parse(ڻ(ƶ, c++));
    ఫ = ܗ(ڻ(ƶ, c++));
    న = ܗ(ڻ(ƶ, c++));
    ƶ = ڻ(ی, 2).Split('\n');
    c =
      0;
    ઠ = (થ) int.Parse(ڻ(ƶ, c++));
    ధ = (ఏ) int.Parse(ڻ(ƶ, c++));
    ஶ = (ఏ) int.Parse(ڻ(ƶ, c++));
    ޏ = int.Parse(ڻ(ƶ, c++));
    ޠ = int.Parse(ڻ(ƶ, c++));
    ޟ = int.Parse(ڻ(ƶ, c++));
    ޞ = int.Parse(ڻ(ƶ, c++));
    ޝ = double.Parse(ڻ(ƶ, c++));
    ߢ = bool.Parse(ڻ(ƶ, c++));
    ߍ = bool.Parse(ڻ(ƶ, c++));
    if (ઠ ==
      થ.ކ) {
      ߴ.Բ = (Կ) int.Parse(ڻ(ƶ, c++));
      ߴ.Ա = double.Parse(ڻ(ƶ, c++));
      ߴ.ԧ = double.Parse(ڻ(ƶ, c++));
      ߴ.Ԧ = ڻ(ƶ, c++);
      ߴ.ԥ = ڻ(ƶ, c++);
      ߪ.Բ = (Կ)
      int.Parse(ڻ(ƶ, c++));
      ߪ.Ա = double.Parse(ڻ(ƶ, c++));
      ߪ.ԧ = double.Parse(ڻ(ƶ, c++));
      ߪ.Ԧ = ڻ(ƶ, c++);
      ߪ.ԥ = ڻ(ƶ, c++);
    } else {
      ߧ = (ߑ) int.Parse(ڻ(
        ƶ, c++));
      ߩ = (ߝ) int.Parse(ڻ(ƶ, c++));
      ߎ = (ࠄ) int.Parse(ڻ(ƶ, c++));
      ߣ = (ߕ) int.Parse(ڻ(ƶ, c++));
      ద = (ߑ) int.Parse(ڻ(ƶ, c++));
      ߨ = bool.Parse(
        ڻ(ƶ, c++));
      ߟ = bool.Parse(ڻ(ƶ, c++));
      ߡ = bool.Parse(ڻ(ƶ, c++));
      ߠ = bool.Parse(ڻ(ƶ, c++));
      ߦ = int.Parse(ڻ(ƶ, c++));
      ߥ = int.Parse(ڻ(ƶ, c++));
      ߤ = int.Parse(ڻ(ƶ, c++));
      ޜ = double.Parse(ڻ(ƶ, c++));
      ޛ = double.Parse(ڻ(ƶ, c++));
      ޚ = double.Parse(ڻ(ƶ, c++));
      ޙ = double.Parse(ڻ(ƶ, c++));
      థ = int.Parse(ڻ(ƶ, c++));
      త = int.Parse(ڻ(ƶ, c++));
      ঽ = int.Parse(ڻ(ƶ, c++));
      হ = int.Parse(ڻ(ƶ, c++));
      ষ = int.Parse(ڻ(ƶ, c++));
      স = int.
      Parse(ڻ(ƶ, c++));
      শ = int.Parse(ڻ(ƶ, c++));
      ਙ = double.Parse(ڻ(ƶ, c++));
    }
    ƶ = ڻ(ی, 3).Replace("\n", "").Split('|');
    ବ = ƶ.ToList();
    ƶ = ڻ(ی, 4).
    Split('\n');
    ટ.Clear();
    if (ƶ.Count() >= 1 && ƶ[0] != "")
      for (int z = 0; z < ƶ.Length; z++) ટ.Add(ܓ(ڻ(ƶ, z)));
  } catch {
    return ڷ.ڿ;
  }
  ڸ = ஶ;
  ఙ();
  return
  ڷ.ڶ;
}
String ۋ(String ʳ) {
  int c = ʳ.IndexOf("//");
  if (c != -1) ʳ = ʳ.Substring(0, c);
  String[] ƶ = ʳ.Split('=');
  if (ƶ.Length <= 1) return "";
  return ƶ[1].Trim();
}
String ۊ(String[] ƶ, String ۉ, ref bool ķ) {
  foreach(String u in ƶ) if (u.StartsWith(ۉ)) return u;
  ķ = false;
  return "";
}
bool ۈ = false;
String ۇ = "";
String ۆ() {
  if (ઠ != થ.ડ) return "" + Me.GetId();
  return ۅ;
}
const String ۅ = "#";
const Char ۄ = ';';
bool ۃ(
  ref String u, out string ۂ, Char ہ) {
  int c = u.IndexOf(ہ);
  ۂ = "";
  if (c < 0) return false;
  ۂ = u.Substring(0, c);
  u = u.Remove(0, c + 1);
  return
  true;
}
void ע(String շ) {
  if (!ۈ) return;
  String נ = "" + ۄ;
  String ן = ן = VERSION + נ;
  ן += ۇ + נ;
  ן += (int) ઠ + נ;
  ן += ڀ(ࡏ.X) + "" + נ;
  ן += ڀ(ࡏ.Y) + נ;
  ן += ڀ(ࡏ.Z) + נ;
  if (ઠ != થ.ކ) ן += ށ(ஶ) + (ஶ == ఏ.ఋ ? " " + Math.Round(ణ, 1) + "%" : "") + נ;
  else ן += ށ(ஶ) + נ;
  ן += ܭ + נ;
  ן += ތ + "" + נ;
  ן += ڙ + נ;
  ן += Ճ + נ;
  ן += Ղ + נ;
  for (int c = 0; c < Ձ.Count; c++) {
    if (Ձ[c].П != Ъ.Щ) continue;
    ן += Ձ[c].Х + "/" + Ձ[c].Ф + "/" + Ձ[c].ʵ + נ;
  }
}
public enum מ {
  ם,
  ל,
  כ,
  ך
}
String י(double ʵ) {
  if (ʵ >= 1000000) return Math.Round(ʵ / 1000000 f, ʵ / 1000000 f < 100 ? 1 : 0) + "M";
  if (ʵ >= 1000) return Math.Round(ʵ / 1000 f, ʵ / 1000 f < 100 ? 1 : 0) +
    "K";
  return "" + Math.Round(ʵ);
}
String ט(int ח) {
  if (ח >= 60 * 60) return Math.Round(ח / (60 f * 60 f), 1) + " h";
  if (ח >= 60) return Math.Round(ח /
    60 f, 1) + " min";
  return "" + ח + " s";
}
String ז(int ו, String u, int ה, ref int ד) {
  String[] ג = u.Split('\n');
  if (ה >= ד + ו - 1) ד++;
  ד = Math.Min(
    ג.Count() - 1 - ו, ד);
  if (ה < ד + 1) ד--;
  ד = Math.Max(0, ד);
  String ƹ = "";
  for (int c = 0; c < ו; c++) {
    int ב = c + ד;
    if (ב >= ג.Count()) break;
    ƹ += ג[ב] +
      "\n";
  }
  return ƹ;
}
String ס(int ɧ, int ب, String ʳ) {
  String[] ג = ʳ.Split('\n');
  int ا = ɧ * ب;
  int ئ = (ɧ + 1) * (ب);
  String ƹ = "";
  for (int c = ا; c < ئ; c++) {
    if (c >= ג.Count()) break;
    ƹ += ג[c] + "\n";
  }
  return ƹ;
}
class إ {
  private Action < ICollection < MyDefinitionId >> ؤ;
  private Action <
    ICollection < MyDefinitionId >> أ;
  private Action < ICollection < MyDefinitionId >> آ;
  private Func < IMyTerminalBlock, IDictionary < string, int > ,
    bool > ء;
  private Func < long, MyTuple < bool, int, int >> ؠ;
  private Action < IMyTerminalBlock, IDictionary < MyDetectedEntityInfo, float >> ײ;
  private Action < IMyTerminalBlock, ICollection < MyDetectedEntityInfo >> ױ;
  private Func < long, int, MyDetectedEntityInfo > װ;
  private Func <
    IMyTerminalBlock, long, int, bool > ת;
  private Func < IMyTerminalBlock, int, MyDetectedEntityInfo > ש;
  private Action < IMyTerminalBlock, long, int > ר;
  private Action < IMyTerminalBlock, bool, int > ק;
  private Action < IMyTerminalBlock, bool, bool, int > צ;
  private Func < IMyTerminalBlock, int,
    bool, bool, bool > ץ;
  private Func < IMyTerminalBlock, int, float > פ;
  private Func < IMyTerminalBlock, ICollection < string > , int, bool > ף;
  private Action < IMyTerminalBlock, ICollection < string > , int > א;
  private Action < IMyTerminalBlock, float > և;
  private Func < IMyTerminalBlock, long, int, bool > Օ;
  private Func < IMyTerminalBlock, long, int, bool > ճ;
  private Func < IMyTerminalBlock, long, int, Vector3D ? > ղ;
  private
  Func < IMyTerminalBlock, float > ձ;
  private Func < IMyTerminalBlock, float > հ;
  private Func < MyDefinitionId, float > կ;
  private Func < long,
    bool > ծ;
  private Func < IMyTerminalBlock, bool > խ;
  private Func < long, float > լ;
  private Func < IMyTerminalBlock, int, string > ի;
  private
  Action < IMyTerminalBlock, int, string > ժ;
  private Action < IMyTerminalBlock, int, Action < long, int, ulong, long, Vector3D, bool >> թ;
  private
  Action < IMyTerminalBlock, int, Action < long, int, ulong, long, Vector3D, bool >> ը;
  private Func < long, float > է;
  private Func <
    IMyTerminalBlock, long > զ;
  private Func < IMyTerminalBlock, int, Matrix > ե;
  private Func < IMyTerminalBlock, int, Matrix > դ;
  private Func <
    IMyTerminalBlock, long, bool, bool, bool > գ;
  private Func < IMyTerminalBlock, int, MyTuple < Vector3D, Vector3D >> բ;
  private Func < IMyTerminalBlock,
    MyTuple < bool, bool >> ա;
  public bool ՙ(IMyTerminalBlock Ֆ) {
    var մ = Ֆ.GetProperty("WcPbAPI")?.As < IReadOnlyDictionary < string,
      Delegate >>
      ().GetValue(Ֆ);
    if (մ == null) throw new Exception($"WcPbAPI failed to activate");
    return ն(մ);
  }
  public bool ն(
    IReadOnlyDictionary < string, Delegate > ք) {
    if (ք == null) return false;
    ֆ(ք, "GetCoreWeapons", ref ؤ);
    ֆ(ք, "GetCoreStaticLaunchers", ref أ);
    ֆ(ք,
      "GetCoreTurrets", ref آ);
    ֆ(ք, "GetBlockWeaponMap", ref ء);
    ֆ(ք, "GetProjectilesLockedOn", ref ؠ);
    ֆ(ք, "GetSortedThreats", ref ײ);
    ֆ(ք,
      "GetObstructions", ref ױ);
    ֆ(ք, "GetAiFocus", ref װ);
    ֆ(ք, "SetAiFocus", ref ת);
    ֆ(ք, "GetWeaponTarget", ref ש);
    ֆ(ք, "SetWeaponTarget", ref ר);
    ֆ(ք,
      "FireWeaponOnce", ref ק);
    ֆ(ք, "ToggleWeaponFire", ref צ);
    ֆ(ք, "IsWeaponReadyToFire", ref ץ);
    ֆ(ք, "GetMaxWeaponRange", ref פ);
    ֆ(ք,
      "GetTurretTargetTypes", ref ף);
    ֆ(ք, "SetTurretTargetTypes", ref א);
    ֆ(ք, "SetBlockTrackingRange", ref և);
    ֆ(ք, "IsTargetAligned", ref Օ);
    ֆ(ք,
      "CanShootTarget", ref ճ);
    ֆ(ք, "GetPredictedTargetPosition", ref ղ);
    ֆ(ք, "GetHeatLevel", ref ձ);
    ֆ(ք, "GetCurrentPower", ref հ);
    ֆ(ք, "GetMaxPower", ref կ);
    ֆ(ք, "HasGridAi", ref ծ);
    ֆ(ք, "HasCoreWeapon", ref խ);
    ֆ(ք, "GetOptimalDps", ref լ);
    ֆ(ք, "GetActiveAmmo", ref ի);
    ֆ(ք,
      "SetActiveAmmo", ref ժ);
    ֆ(ք, "MonitorProjectile", ref թ);
    ֆ(ք, "UnMonitorProjectile", ref ը);
    ֆ(ք, "GetConstructEffectiveDps", ref է);
    ֆ(ք,
      "GetPlayerController", ref զ);
    ֆ(ք, "GetWeaponAzimuthMatrix", ref ե);
    ֆ(ք, "GetWeaponElevationMatrix", ref դ);
    ֆ(ք, "IsTargetValid", ref գ);
    ֆ(ք,
      "GetWeaponScope", ref բ);
    ֆ(ք, "IsInRange", ref ա);
    return true;
  }
  private void ֆ < օ > (IReadOnlyDictionary < string, Delegate > ք, string փ, ref օ ւ)
  where օ: class {
    if (ք == null) {
      ւ = null;
      return;
    }
    Delegate ց;
    if (!ք.TryGetValue(փ, out ց)) throw new Exception(
      $ "{GetType().Name} :: Couldn't find {փ} delegate of type {typeof(օ)}");
    ւ = ց as օ;
    if (ւ == null) throw new Exception(
      $ "{GetType().Name} :: Delegate {փ} is not type {typeof(օ)}, instead it's: {ց.GetType()}");
  }
  public void ր(ICollection < MyDefinitionId > յ) => ؤ?.Invoke(յ);
  public void տ(ICollection < MyDefinitionId > յ) => أ?.Invoke(յ);
  public void վ(ICollection < MyDefinitionId > յ) => آ?.Invoke(յ);
  public bool ս(IMyTerminalBlock ռ, IDictionary < string, int > յ) => ء?.
  Invoke(ռ, յ) ?? false;
  public MyTuple < bool, int, int > ջ(long պ) => ؠ?.Invoke(պ) ?? new MyTuple < bool, int, int > ();
  public void չ(
    IMyTerminalBlock Ֆ, IDictionary < MyDetectedEntityInfo, float > յ) => ײ?.Invoke(Ֆ, յ);
  public void ո(IMyTerminalBlock Ֆ, ICollection <
    MyDetectedEntityInfo > յ) => ױ?.Invoke(Ֆ, յ);
  public MyDetectedEntityInfo ? ة(long ع, int ٳ = 0) => װ?.Invoke(ع, ٳ);
  public bool ٴ(IMyTerminalBlock Ֆ, long ٯ, int ٳ = 0) => ת?.Invoke(Ֆ, ٯ, ٳ) ?? false;
  public MyDetectedEntityInfo ? ٲ(IMyTerminalBlock خ, int ح = 0) => ש?.Invoke(خ, ح) ?? null;
  public void ٱ(IMyTerminalBlock خ, long ٯ, int ح = 0) => ר?.Invoke(خ, ٯ, ح);
  public void ٮ(IMyTerminalBlock خ, bool و = true, int ح = 0) => ק?.
  Invoke(خ, و, ح);
  public void ي(IMyTerminalBlock خ, bool ى, bool و, int ح = 0) => צ?.Invoke(خ, ى, و, ح);
  public bool ه(IMyTerminalBlock خ, int ح = 0, bool ټ = true, bool ٽ = false) => ץ?.Invoke(خ, ح, ټ, ٽ) ?? false;
  public float ٻ(IMyTerminalBlock خ, int ح) => פ?.Invoke(خ, ح) ?? 0 f;
  public bool ٺ(IMyTerminalBlock خ, IList < string > յ, int ح = 0) => ף?.Invoke(خ, յ, ح) ?? false;
  public void ٹ(IMyTerminalBlock خ, IList <
    string > յ, int ح = 0) => א?.Invoke(خ, յ, ح);
  public void ٸ(IMyTerminalBlock خ, float ٷ) => և?.Invoke(خ, ٷ);
  public bool ٶ(IMyTerminalBlock خ, long ت, int ح) => Օ?.Invoke(خ, ت, ح) ?? false;
  public bool ٵ(IMyTerminalBlock خ, long ت, int ح) => ճ?.Invoke(خ, ت, ح) ?? false;
  public
  Vector3D ? ن(IMyTerminalBlock خ, long ت, int ح) => ղ?.Invoke(خ, ت, ح) ?? null;
  public float ظ(IMyTerminalBlock خ) => ձ?.Invoke(خ) ?? 0 f;
  public
  float ط(IMyTerminalBlock خ) => հ?.Invoke(خ) ?? 0 f;
  public float ض(MyDefinitionId ص) => կ?.Invoke(ص) ?? 0 f;
  public bool ش(long ر) => ծ?.
  Invoke(ر) ?? false;
  public bool س(IMyTerminalBlock خ) => խ?.Invoke(خ) ?? false;
  public float ز(long ر) => լ?.Invoke(ر) ?? 0 f;
  public string
  ذ(IMyTerminalBlock خ, int ح) => ի?.Invoke(خ, ح) ?? null;
  public void د(IMyTerminalBlock خ, int ح, string ج) => ժ?.Invoke(خ, ح, ج);
  public void ث(IMyTerminalBlock خ, int ح, Action < long, int, ulong, long, Vector3D, bool > ل) => թ?.Invoke(خ, ح, ل);
  public void م(
    IMyTerminalBlock خ, int ح, Action < long, int, ulong, long, Vector3D, bool > ل) => ը?.Invoke(خ, ح, ل);
  public float ك(long ر) => է?.Invoke(ر) ?? 0 f;
  public
  long ق(IMyTerminalBlock خ) => զ?.Invoke(خ) ?? -1;
  public Matrix ف(IMyTerminalBlock خ, int ح) => ե?.Invoke(خ, ح) ?? Matrix.Zero;
  public
  Matrix ـ(IMyTerminalBlock خ, int ح) => դ?.Invoke(خ, ح) ?? Matrix.Zero;
  public bool ؿ(IMyTerminalBlock خ, long ؾ, bool ؽ, bool ؼ) => գ?.
  Invoke(خ, ؾ, ؽ, ؼ) ?? false;
  public MyTuple < Vector3D, Vector3D > ػ(IMyTerminalBlock خ, int ح) => բ?.Invoke(خ, ح) ?? new MyTuple < Vector3D,
    Vector3D > ();
  public MyTuple < bool, bool > Ң(IMyTerminalBlock غ) => ա?.Invoke(غ) ?? new MyTuple < bool, bool > ();
}
