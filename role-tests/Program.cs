using System.Reflection;
using AUAnticheat.anticheat;
using AmongUs.GameOptions;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
int count=0;
void Check(bool v,string message){count++;if(!v)throw new Exception(message);}
MethodInfo Hook(Type t,string name)=>t.GetNestedType(name,BindingFlags.NonPublic)!.GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static)!;
var decode=Hook(typeof(InfluencerProtection),"BeforeInfluencerDecode");
var images=Hook(typeof(InfluencerProtection),"BeforeInfluencerImages");
var role=Hook(typeof(GhostRoleTransitionMonitor),"OnGhostRoleAssigned");
var feed=new SocialMediumFeedSystem();
bool Display(params byte[] list)=>(bool)images.Invoke(null,new object[]{feed,new Il2CppStructArray<byte>(list)})!;
InfluencerProtection.Reset();
Check(Display(0)&&Display(0,1)&&Display(0,1,2),"all legal message sizes reach feed");
Check(!Display()&&!Display(0,1,2,0),"invalid image counts blocked before display");
Check(!Display(3)&&!Display(255),"out of catalog indices blocked");
Check(Display(2,2),"repeated image selection not invented as a cheat");
feed.PossibleSpriteList=null!;
Check(!Display(0),"uninitialized catalog protected without player accusation");
feed.PossibleSpriteList=new(){new(),new(),new()};
InfluencerProtection.Reset();
for(int n=0;n<64;n++)Check(Display(0),"normal burst accepted");
Check(!Display(0),"feed flood stopped before rendering");
feed.Id++;
Check(Display(0),"recreated feed does not inherit previous limit");
InfluencerProtection.Reset();
Check(Display(0),"lobby reset clears resource limit");
var reader=new Hazel.MessageReader {Buffer=new byte[]{99,99,2,0,1},Offset=1,Position=1,Length=4};
Check((bool)decode.Invoke(null,new object[]{reader})!,"native entry permits legal nonzero-offset buffer");
Check(reader.Position==1,"native entry preserves cursor");
reader.Buffer[2]=255;
Check(!(bool)decode.Invoke(null,new object[]{reader})!,"native entry blocks invalid length");
Check(reader.Position==1,"rejected message preserves cursor");

var player=new PlayerControl();
foreach(var ghost in new[]{RoleTypes.SpiritGuide,RoleTypes.GuardianAngel,RoleTypes.CrewmateGhost,RoleTypes.ImpostorGhost})
{
    Anticheat.Reset(); player.Data.RoleType=RoleTypes.Crewmate; player.Data.IsDead=false;
    role.Invoke(null,new object[]{player,ghost});
    Check(Anticheat.GhostGrace&&Anticheat.MoveGrace,"incoming ghost role grants grace before death replication");
    Check(!MovementContext.StableLivingPlayer(player),"grace covers old living role snapshot");
    Anticheat.Reset(); player.Data.RoleType=ghost;
    Check(!MovementContext.StableLivingPlayer(player),"ghost role excluded even before IsDead arrives");
}
player.Data.RoleType=RoleTypes.Crewmate; Anticheat.Reset();
Check(MovementContext.StableLivingPlayer(player),"stable live player inspected");
void ContextCase(Action set,Action clear,string reason){set();Check(!MovementContext.StableLivingPlayer(player),reason);clear();}
ContextCase(()=>player.Data.IsDead=true,()=>player.Data.IsDead=false,"death excluded");
ContextCase(()=>player.roleAssigned=false,()=>player.roleAssigned=true,"uninitialized role excluded");
ContextCase(()=>player.inVent=true,()=>player.inVent=false,"engineer/vent movement excluded");
ContextCase(()=>player.onLadder=true,()=>player.onLadder=false,"ladder excluded");
ContextCase(()=>player.inMovingPlat=true,()=>player.inMovingPlat=false,"platform excluded");
ContextCase(()=>player.MyPhysics.DoingCustomAnimation=true,()=>player.MyPhysics.DoingCustomAnimation=false,"custom animation excluded");
ContextCase(()=>MeetingHud.Instance=new(),()=>MeetingHud.Instance=null,"meeting excluded");
ContextCase(()=>Anticheat.MeetingGrace=true,()=>Anticheat.MeetingGrace=false,"post-meeting grace excluded");
ContextCase(()=>Anticheat.RoundGrace=true,()=>Anticheat.RoundGrace=false,"round grace excluded");
ContextCase(()=>LobbyBehaviour.Instance=new(),()=>LobbyBehaviour.Instance=null,"lobby excluded");
ContextCase(()=>GameManager.Instance.GameHasStarted=false,()=>GameManager.Instance.GameHasStarted=true,"round ended excluded");
var snap=new AUAnticheat.anticheat.rpc.SnapTo();
bool Jump(){Anticheat.MoveGrace=false;return snap.Validate(player,new Hazel.MessageReader {Buffer=new byte[]{20,0,0,0,1,0},Length=6});}
Check(!Jump(),"unexpected large snap rejected by local authority before applying");
player.Data.RoleType=RoleTypes.SpiritGuide;
Check(Jump(),"Influencer snap not rejected as living teleport");
player.Data.RoleType=RoleTypes.Crewmate; player.inVent=true;
Check(Jump(),"vent snap allowed");player.inVent=false;
AmongUsClient.Instance.AmHost=false;
Check(Jump(),"observer does not override authoritative remote state");AmongUsClient.Instance.AmHost=true;
AUAnticheat.Utilities.ServerAuthority=true;
Check(Jump(),"official server correction allowed");
Console.WriteLine($"PASS: {count} production role/teleport adapter assertions with managed doubles; not live native gameplay.");
