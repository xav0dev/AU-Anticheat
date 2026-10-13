using System.Reflection;
using UnityEngine;
using AUAnticheat.anticheat;

int count=0;
void Check(bool result,string reason) { count++; if(!result) throw new Exception(reason); }
var observe=typeof(LobbyMovementGuard).GetNestedType("ObserveLobby",BindingFlags.NonPublic)!.GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static)!;
var snap=typeof(LobbyMovementGuard).GetNestedType("BeforeLobbySnap",BindingFlags.NonPublic)!.GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static)!;
var player=new PlayerControl(); player.NetTransform=new CustomNetworkTransform { myPlayer=player }; PlayerControl.AllPlayerControls.Add(player);
void Tick(float time,float x) { Time.realtimeSinceStartup=time; player.transform.position=new Vector2(x,0); observe.Invoke(null,null); }
bool Incoming(float x,byte call=21)
{
    var reader=new Hazel.MessageReader { Buffer=BitConverter.GetBytes((short)(x*100)).Concat(new byte[4]).ToArray() };
    bool accepted=(bool)snap.Invoke(null,new object[]{player.NetTransform,call,reader})!;
    Check(reader.Position==0,"snap inspection preserves reader"); return accepted;
}
LobbyMovementGuard.Reset();
Tick(0,-1);
for(int i=1;i<=40;i++) Tick(i*0.1f,-1);
int casts=Physics2D.Casts;
Tick(4.1f,0); // brush wardrobe
Check(Incoming(1),"wall contact invalidates stale checkpoint");
Tick(4.2f,1); Tick(4.4f,2); Tick(4.6f,-2); // curved/interpolated ordinary movement
Check(Physics2D.Casts==casts && LobbyMovementGuard.BlockedSnaps==0,"ordinary motion never classified by straight-line wall casts");
Check(AUAnticheat.AUAnticheatPlugin.notifications.Count==0,"walking produces no wall-crossing warning");
Check(!Incoming(2),"explicit fresh cross-wall teleport still locally blocked");
Check(LobbyMovementGuard.BlockedSnaps==1,"explicit snap block counted");
Check(Incoming(0),"endpoint touching geometry is ambiguous, not blocked");
Check(Incoming(-3),"same-side snap allowed");
Check(Incoming(2,99),"unrelated RPC untouched");
Time.realtimeSinceStartup=6;
Check(Incoming(2),"stale checkpoint cannot reject a new snap");
Tick(6,-2); Tick(7.1f,-2); Tick(7.3f,-2); Tick(8.4f,-2);
for(int i=1;i<=12;i++) Tick(8.4f+i*0.1f,-2);
Physics2D.ThrowCast=true;
Check(Incoming(2),"physics query failure is not malformed-packet evidence");
Check(LobbyMovementGuard.Enabled,"geometry failure does not disable unrelated protections");
Physics2D.ThrowCast=false;
Tick(20,-2);for(int i=1;i<=20;i++) Tick(20+i*.1f,-2);
Check(!Incoming(-10),"explicit teleport outside geometry envelope locally rejected even without wall intersection");
Time.realtimeSinceStartup=30;player.transform.position=new(-10,0);
int notices=AUAnticheat.AUAnticheatPlugin.notifications.Count;
SceneBoundaryMonitor.Tick();Time.realtimeSinceStartup=34;SceneBoundaryMonitor.Tick();
Check(AUAnticheat.AUAnticheatPlugin.notifications.Count==notices,"new player has boundary settling grace");
for(int i=0;i<=25;i++) { Time.realtimeSinceStartup=35+i*.1f;SceneBoundaryMonitor.Tick(); }
Check(AUAnticheat.AUAnticheatPlugin.notifications.Count==notices+1 && SenderEvidence.Summary().Contains("Target"),"sustained out-of-bounds position gives one target-labelled review alert");
SceneBoundaryMonitor.Tick();Check(AUAnticheat.AUAnticheatPlugin.notifications.Count==notices+1,"boundary alerts throttled");
player.Data.Disconnected=true;Time.realtimeSinceStartup+=20;SceneBoundaryMonitor.Tick();
Check(AUAnticheat.AUAnticheatPlugin.notifications.Count==notices+1,"disconnected players excluded");player.Data.Disconnected=false;
LobbyBehaviour.Instance.Colliders=Array.Empty<Collider2D>();Time.realtimeSinceStartup+=2;
Check(!SceneBoundaryMonitor.OutsideLobby(new(-10,0)),"missing collision geometry cannot block valid traffic");
SceneBoundaryMonitor.Tick();Check(AUAnticheat.AUAnticheatPlugin.notifications.Count==notices+1,"missing geometry cannot accuse player");
LobbyBehaviour.Instance=null;ShipStatus.Instance=new();MovementContext.Stable=false;Time.realtimeSinceStartup+=10;
for(int i=0;i<80;i++) { Time.realtimeSinceStartup+=.1f;SceneBoundaryMonitor.Tick(); }
Check(AUAnticheat.AUAnticheatPlugin.notifications.Count==notices+1,"vent/meeting/transition instability excluded in game");
Console.WriteLine($"PASS: {count} production lobby-guard assertions with simulated geometry; not live Unity physics.");
