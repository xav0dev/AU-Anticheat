using System.Reflection;
using AUAnticheat.anticheat;
using UnityEngine;
using InnerNet;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
int assertions=0;
void Check(bool result,string name){assertions++;if(!result)throw new Exception(name);}
MethodInfo Hook(Type t,string nested,string method)=>t.GetNestedType(nested,BindingFlags.NonPublic)!.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!;
string saved="";
FriendCodeExceptions.Initialize("old#1234;BAD INPUT;OLD#1234",s=>saved=s);
Check(FriendCodeExceptions.Snapshot().Length==1,"loaded codes are normalized/deduplicated");
Check(FriendCodeExceptions.Add(" Test#1234 ")&&saved.Contains("test#1234"),"persist normalized exception");
Check(!FriendCodeExceptions.Add("test#1234")&&!FriendCodeExceptions.Add("<b>x#1234"),"duplicate/markup rejected");
Check(FriendCodeExceptions.Remove("TEST#1234")&&!saved.Contains("test#1234"),"remove exact exception");
for(int n=0;n<200;n++)FriendCodeExceptions.Add($"player#{n}");
Check(FriendCodeExceptions.Snapshot().Length==128,"exception storage bounded");
FriendCodeExceptions.Initialize("",null);
var client=AmongUsClient.Instance;
PlayerControl Add(int id,uint level,string code)
{
    var p=new PlayerControl {OwnerId=id,NetId=(uint)id,Data=new(){PlayerLevel=level,FriendCode=code,PlayerName=$"Player{id}"}};
    PlayerControl.AllPlayerControls.Add(p);client.Clients[id]=new(){Id=id,Character=p,PlayerLevel=level,FriendCode=code};return p;
}
var host=Add(1,0,"host#1111");PlayerControl.LocalPlayer=host;
var low=Add(2,1,"low#2222");
var levelTick=Hook(typeof(LobbyLevelPolicy),"ObserveLobby","Postfix");
void Scan(){Time.realtimeSinceStartup+=0.2f;levelTick.Invoke(null,null);}
LobbyLevelPolicy.Enabled=true; LobbyLevelPolicy.MinimumLevel=10;
client.Clients[2].IsBeingCreated=true; Scan();Check(client.Removals.Count==0,"loading record waits");
client.Clients[2].IsBeingCreated=false;client.Clients[2].PlayerLevel=2;Scan();Check(client.Removals.Count==0,"conflicting level waits");
client.Clients[2].PlayerLevel=1;low.Data.FriendCode="";Scan();Check(client.Removals.Count==0,"unknown friend code waits for exceptions");
low.Data.FriendCode="low#2222";Scan();Check(client.Removals.Count==0,"enabling rule gives lobby time to settle");
Time.realtimeSinceStartup+=2;Scan();
Check(client.Removals.SequenceEqual(new[]{(2,true)}),"ready sample after settling requests one ban, host exempt");
Scan();Check(client.Removals.Count==1,"no repeated ban request");
var exempt=Add(3,1,"friend#3333");FriendCodeExceptions.Add("friend#3333");Scan();Check(client.Removals.Count==1,"friend-code exception bypasses only admission");
FriendCodeExceptions.Remove("friend#3333");Scan();Check(client.Removals.Count==1,"second removal waits for departure of first");
low.Data.Disconnected=true;Scan();Check(client.Removals.Count==1,"departure starts cooldown");
Time.realtimeSinceStartup+=2;Scan();Check(client.Removals.Last()==(3,true),"removing exception re-enables paced admission");
low.Data.Disconnected=false;
client.AmHost=false;var nonhost=Add(4,1,"guest#4444");Scan();Check(client.Removals.Count==2,"nonhost never removes players");client.AmHost=true;
GameManager.Instance.GameHasStarted=true;Scan();Check(client.Removals.Count==2,"no admission bans during round");GameManager.Instance.GameHasStarted=false;

Time.realtimeSinceStartup=1000;
var offer=Hook(typeof(LobbyExtension),"OnOffer","Postfix");var lobby=LobbyBehaviour.Instance;
LobbyExtension.Tick();Check(lobby.Requests==0,"no timer request without game offer");
offer.Invoke(null,new object[]{lobby,60,true,1,10});
LobbyExtension.Tick();Check(lobby.Requests==1,"requests as soon as extension is offered");
Time.realtimeSinceStartup=1001;LobbyExtension.Tick();LobbyExtension.Tick();Check(lobby.Requests==1,"no repeated request for same offer");
offer.Invoke(null,new object[]{lobby,20,true,1,11});LobbyExtension.Tick();Check(lobby.Requests==1,"changed offer cannot create request flood");
Time.realtimeSinceStartup=1100;offer.Invoke(null,new object[]{lobby,20,false,1,12});LobbyExtension.Tick();Check(lobby.Requests==1,"unavailable extension not requested");
offer.Invoke(null,new object[]{lobby,20,true,2,13});LobbyExtension.Tick();Check(lobby.Requests==1,"wrong host not requested");
offer.Invoke(null,new object[]{lobby,20,true,1,14});client.GameId=43;LobbyExtension.Tick();Check(lobby.Requests==1,"old lobby offer cannot affect new game");client.GameId=42;
Hook(typeof(LobbyExtension),"OnExtended","Postfix").Invoke(null,new object[]{lobby});Check(!LobbyExtension.Status.Contains("game confirmed"),"late confirmation for stale game state ignored");
offer.Invoke(null,new object[]{lobby,60,true,1,15});LobbyExtension.Tick();
Time.realtimeSinceStartup+=11;LobbyExtension.Tick();Check(LobbyExtension.Status.Contains("No extension confirmation"),"request without acknowledgement warns instead of promising extension");
Hook(typeof(LobbyExtension),"OnExtended","Postfix").Invoke(null,new object[]{lobby});Check(LobbyExtension.Status.Contains("game confirmed"),"valid extension confirmation is separate from request");

var round=new RoundLedger();round.Begin(10);round.Observe(1,"<b>Name\n", "Phantom",true);round.Observe(2,"Crew", "Crewmate",false);
round.Kill(1,2);round.Kill(1,2);round.Kill(1,1);Check(round.Lines().Count==0,"no report or role leak during live game");
round.End(135,3);Check(round.Lines()[0].Contains("Impostors won")&&round.Lines()[0].Contains("2m 5s"),"correct winner/duration");
Check(round.Lines()[1].Contains("1 kill(s)")&&!round.Lines()[1].Contains('<'),"kill deduplication and safe chat text");
for(int reason=0;reason<=8;reason++)Check(RoundLedger.Winner(reason)==(new[]{0,1,5,7}.Contains(reason)?"Crewmates":"Impostors"),"native outcome mapping");
round.Begin(100);Check(round.Lines().Count==0,"next round discards previous report");

host.Data.RoleType=RoleTypes.Shapeshifter;client.GameState=InnerNetClient.GameStates.Started;GameManager.Instance.GameHasStarted=true;
host.Data.RoleWhenAlive=RoleTypes.Crewmate; // A stale alive-role field must not override a living role.
LobbyChat.Clear();PlayerControl.Sent.Clear();RoundReports.Begin();
var killPrefix=Hook(typeof(RoundReports),"OnKillResult","Prefix");var killPostfix=Hook(typeof(RoundReports),"OnKillResult","Postfix");
object[] killArguments={low,MurderResultFlags.Succeeded,false};killPrefix.Invoke(null,killArguments);killPostfix.Invoke(null,new object[]{host,low,killArguments[2]});killPostfix.Invoke(null,new object[]{host,low,killArguments[2]});
RoundReports.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==0,"production report never sends during live gameplay");
var end=Hook(typeof(RoundReports),"OnEnd","Prefix");Time.realtimeSinceStartup+=125;end.Invoke(null,new object[]{new EndGameResult{GameOverReason=GameOverReason.CrewmatesByTask}});
RoundReports.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==0,"end event alone does not leak report before waiting lobby");
client.GameState=InnerNetClient.GameStates.Joined;GameManager.Instance.GameHasStarted=false;
client.Clients[1].IsBeingCreated=true;RoundReports.Tick();Time.realtimeSinceStartup+=10;RoundReports.Tick();LobbyChat.Tick();
Check(PlayerControl.Sent.Count==0,"return waits for host spawn to finish");client.Clients[1].IsBeingCreated=false;
RoundReports.Tick();Time.realtimeSinceStartup+=2;RoundReports.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==0,"two seconds is too early for result");
var chat=HudManager.Instance.Chat;HudManager.Instance.Chat=null;RoundReports.Tick();Time.realtimeSinceStartup+=10;
HudManager.Instance.Chat=chat;RoundReports.Tick();Time.realtimeSinceStartup+=2;RoundReports.Tick();LobbyChat.Tick();
Check(PlayerControl.Sent.Count==0,"HUD interruption restarts continuous readiness delay");
LobbyBehaviour.Instance.Id++;RoundReports.Tick();Time.realtimeSinceStartup+=2;RoundReports.Tick();LobbyChat.Tick();
Check(PlayerControl.Sent.Count==0,"new lobby instance restarts delay");
Time.realtimeSinceStartup+=3;RoundReports.Tick();chat.timeSinceLastMessage=0;LobbyChat.Tick();
Check(PlayerControl.Sent.Count==0,"ready results wait for the shared manual chat cooldown");
Time.realtimeSinceStartup+=3;LobbyChat.Tick();Check(PlayerControl.Sent.Count==0,"native cooldown also has a quarter-second safety margin");
Time.realtimeSinceStartup+=0.25f;LobbyChat.Tick();Check(PlayerControl.Sent.Count==1&&chat.timeSinceLastMessage==0,"automatic result consumes normal chat cooldown");
host.Data.IsDead=true;Time.realtimeSinceStartup+=5;RoundReports.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==1,"stale dead host cannot send queued results");
host.Data.IsDead=false;RoundReports.Tick();Time.realtimeSinceStartup+=3;RoundReports.Tick();LobbyChat.Tick();
Check(PlayerControl.Sent.Count==1 && PlayerControl.Sent[0].Contains("Crewmates won")&&PlayerControl.Sent[0].Contains("2:05")&&PlayerControl.Sent[0].Contains("Player1(S):1k")&&PlayerControl.Sent[0].Length<=100,"all production stats in one bounded message with role and deduplicated kills");
Check(AUAnticheat.AUAnticheatPlugin.notifications.Messages.Any(m=>m.Title=="Match results"&&m.Body.Contains("Shapeshifter")&&m.Body.Contains("1 kill(s)")),"full role and kill details retained in local Alerts");
RoundReports.Tick();Time.realtimeSinceStartup+=3.25f;LobbyChat.Tick();Check(PlayerControl.Sent.Count==1,"summary sent once");
LobbyChat.QueueNotice("old room");client.GameId=77;LobbyChat.Tick();Check(PlayerControl.Sent.Count==1,"chat cleared on room change");
LobbyChat.QueueNotice(new string('x',300));LobbyChat.Tick();Check(PlayerControl.Sent.Last().Length<=100,"public messages bounded");

var join=Hook(typeof(ActivityTracker),"OnJoin","Postfix");join.Invoke(null,new object[]{client.Clients[2]});
ActivityTracker.Observe(2,2,true);Check(ActivityTracker.Summary().Contains("1 messages"),"activity begins on join");ActivityTracker.Forget(2);Check(ActivityTracker.Count==0,"leaving removes session activity");
Check(MeetingResultRules.UniqueVoters(new byte[]{0,1,2})&&!MeetingResultRules.UniqueVoters(new byte[]{0,0}),"duplicate result voter rejected");
Check(MeetingResultRules.UniqueVoters(Array.Empty<byte>())&&!MeetingResultRules.UniqueVoters(new byte[]{255}),"empty result tolerated, sentinel voter rejected");
var resultGuard=Hook(typeof(MeetingProtection),"BeforeResults","Prefix");
var legal=new Il2CppStructArray<MeetingHud.VoterState>(new[]{new MeetingHud.VoterState{VoterId=0,VotedForId=253},new MeetingHud.VoterState{VoterId=1,VotedForId=255}});
Check((bool)resultGuard.Invoke(null,new object[]{legal})!,"skip/dead target sentinels retained");
var dup=new Il2CppStructArray<MeetingHud.VoterState>(new[]{new MeetingHud.VoterState{VoterId=1},new MeetingHud.VoterState{VoterId=1}});
Check(!(bool)resultGuard.Invoke(null,new object[]{dup})!,"production duplicate results blocked without attribution");
Check(PublicText.Simple("Protection","Rejected packet before processing: malformed vote-kick").Contains("nobody was automatically banned"),"clear non-accusatory notification");
Check(PublicText.Simple("Anti-cheat","Detected several unexpected disconnects").Contains("connection problem"),"disconnect alerts distinguish possible attacks from outages");
LobbyChat.QueueNotice("discard after disconnect");
Hook(typeof(RoundReports),"ClearLobbyServices","Postfix").Invoke(null,null);
int sentBeforeDisconnect=PlayerControl.Sent.Count;
Time.realtimeSinceStartup+=10;RoundReports.Tick();LobbyChat.Tick();
Check(PlayerControl.Sent.Count==sentBeforeDisconnect&&ActivityTracker.Count==0,"disconnect clears queued chat and reports even with reused lobby IDs");
LobbyChat.QueueNotice("cancelled notice");LobbyChat.ShareAlerts=false;LobbyChat.Tick();LobbyChat.ShareAlerts=true;
Check(PlayerControl.Sent.Count==sentBeforeDisconnect,"turning off shared alerts discards queued notices");
LobbyChat.QueueReport(new[]{"cancelled report"});RoundReports.Enabled=false;LobbyChat.Tick();RoundReports.Enabled=true;
Check(PlayerControl.Sent.Count==sentBeforeDisconnect,"turning off match reports discards queued reports");
// A full lobby must never produce a batch of admission bans.
client.GameId++;client.AmHost=true;client.GameState=InnerNetClient.GameStates.Joined;
GameManager.Instance.GameHasStarted=false;MeetingHud.Instance=null;
client.Clients.Clear();PlayerControl.AllPlayerControls.Clear();client.Removals.Clear();
host=Add(1,0,"host#1111");PlayerControl.LocalPlayer=host;
for(int id=2;id<=15;id++) Add(id,1,$"guest#{id}");
LobbyLevelPolicy.Enabled=true;LobbyLevelPolicy.MinimumLevel=10;Scan();Time.realtimeSinceStartup+=2;Scan();
Check(client.Removals.Count==1,"15-player enable sends only one ban");
Time.realtimeSinceStartup+=3;Scan();Check(client.Removals.Count==1,"unacknowledged ban blocks next removal");
LobbyLevelPolicy.Enabled=false;Scan();LobbyLevelPolicy.Enabled=true;Scan();Time.realtimeSinceStartup+=3;Scan();
Check(client.Removals.Count==1,"toggle does not bypass pending removal");
FriendCodeExceptions.Add("guest#3");
client.Clients[2].Character.Data.Disconnected=true;Scan();Time.realtimeSinceStartup+=2;Scan();
Check(client.Removals.Count==2&&client.Removals[1].Id==4,"next paced send rechecks new exceptions");
Time.realtimeSinceStartup+=16;Scan();Check(!LobbyLevelPolicy.Enabled&&client.Removals.Count==2,"uncompleted ban pauses policy without retries");
client.Clients[2].Character.Data.Disconnected=false;

// Disk persistence, continuous-full timer, roster changes and normal chat pacing.
string rulesDirectory=Path.Combine(AppContext.BaseDirectory,"au-rules-tests-"+Guid.NewGuid().ToString("N"));
try
{
 bool savedEnabled=false;int savedDelay=0;
 LobbyRules.Initialize(rulesDirectory,false,10,(on,seconds)=>{savedEnabled=on;savedDelay=seconds;});
 Check(File.Exists(LobbyRules.FilePath),"initialization creates persistent rules file");
 File.WriteAllText(LobbyRules.FilePath,"Respect other players.\nNo cheating.\n");LobbyRules.Reload();
 LobbyRules.Enabled=true;LobbyRules.DelaySeconds=5;
 Check(savedEnabled&&savedDelay==5,"rules preferences saved");
 LobbyChat.Clear();PlayerControl.Sent.Clear();LobbyRules.Tick();Time.realtimeSinceStartup+=4;LobbyRules.Tick();LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==0,"no rules before chosen delay");
 Time.realtimeSinceStartup+=1;LobbyRules.Tick();LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==1&&PlayerControl.Sent[0]=="Respect other players. / No cheating.","all rules are sent in order in exactly one unlabelled message");
 LobbyChat.Tick();Check(PlayerControl.Sent.Count==1,"rules do not burst in one frame");
 Time.realtimeSinceStartup+=3;LobbyRules.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==1,"rules observe safety margin too");
 Time.realtimeSinceStartup+=0.25f;LobbyRules.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==1,"no second rules message after cooldown");
 Time.realtimeSinceStartup+=20;LobbyRules.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==1,"no repetition while full roster unchanged");
 client.Clients[15].Character.Data.Disconnected=true;LobbyRules.Tick();
 Time.realtimeSinceStartup+=20;LobbyRules.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==1,"14-player lobby cannot trigger rules");
 client.Clients[15].Character.Data.Disconnected=false;LobbyRules.Tick();Time.realtimeSinceStartup+=4;
 client.Clients[15].Character.NetId++;LobbyRules.Tick();Time.realtimeSinceStartup+=1;LobbyRules.Tick();LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==1,"replacement at 15/15 restarts timer");
 Time.realtimeSinceStartup+=4;LobbyRules.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==2,"refilled replacement roster posts once after a fresh full delay");
 LobbyRules.Enabled=false;LobbyRules.Tick();LobbyRules.Enabled=true;LobbyRules.Tick();Time.realtimeSinceStartup+=10;LobbyRules.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==2,"toggle cannot repeat sent rules");
 LobbyRules.Reload();LobbyRules.Tick();Time.realtimeSinceStartup+=10;LobbyRules.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==2,"reload does not resend while full roster unchanged");
 client.Clients[15].Character.Data.Disconnected=true;LobbyRules.Tick();
 client.Clients[15].Character.Data.Disconnected=false;LobbyRules.Tick();Time.realtimeSinceStartup+=4;LobbyRules.Tick();LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==2,"same player returning waits new fill delay");
 Time.realtimeSinceStartup+=1;LobbyRules.Tick();chat.timeSinceLastMessage=0;LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==2,"refill message respects manual chat cooldown");
 Time.realtimeSinceStartup+=3.25f;LobbyChat.Tick();LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==3,"same roster after a real gap posts exactly once");
 Check(!LobbyChat.QueueRules(new[]{"Respect other players.","No cheating."}),"already-sent full cycle cannot be queued again");
 client.Clients[15].Character.NetId++;LobbyRules.Tick();Time.realtimeSinceStartup+=5;LobbyRules.Tick();chat.timeSinceLastMessage=0;LobbyChat.Tick();
 client.Clients[15].Character.Data.Disconnected=true;LobbyRules.Tick();Time.realtimeSinceStartup+=10;LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==3,"departure cancels refill post queued behind cooldown");
 client.Clients[15].Character.Data.Disconnected=false;LobbyRules.Tick();Time.realtimeSinceStartup+=4;LobbyRules.Tick();LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==3,"cancelled pending fill does not shorten next fill delay");
 Time.realtimeSinceStartup+=1;LobbyRules.Tick();LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==4,"next eligible fill sends once after cancellation");
 GameManager.Instance.GameHasStarted=true;Time.realtimeSinceStartup+=3;LobbyChat.Tick();Check(PlayerControl.Sent.Count==4,"no extra rules messages after game start");
 GameManager.Instance.GameHasStarted=false;LobbyRules.Reset();client.AmHost=false;LobbyRules.Tick();Time.realtimeSinceStartup+=10;LobbyRules.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==4,"nonhost never posts auto-rules");client.AmHost=true;
 LobbyRules.Initialize(rulesDirectory,savedEnabled,savedDelay,null);Check(LobbyRules.Preview.Contains("Respect other players"),"initialization preserves existing file");
 Check(LobbyRules.SaveText("Saved in menu.\nSecond rule."),"in-menu rules save succeeds");LobbyRules.Reload();Check(LobbyRules.Preview.Contains("Saved in menu."),"rules editor content survives disk reload");
 Check(!LobbyRules.SaveText(new string('a',101))&&File.ReadAllText(LobbyRules.FilePath).Contains("Saved in menu."),"invalid edit cannot overwrite saved rules");
 File.WriteAllText(LobbyRules.FilePath,new string('a',101));LobbyRules.Reload();Check(LobbyRules.Preview=="","long rule rejected instead of silently truncated");
 File.WriteAllText(LobbyRules.FilePath,string.Join("\n",Enumerable.Repeat("rule",9)));LobbyRules.Reload();Check(LobbyRules.Preview=="","too many rules rejected");
 File.WriteAllText(LobbyRules.FilePath,new string('x',5000));LobbyRules.Reload();Check(LobbyRules.Preview=="","oversized file rejected");
 Check(LobbyRules.SaveText("Small lobby rules."),"restore usable rules");
 for(int capacity=4;capacity<=15;capacity++)
 {
  GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers=capacity;
  foreach(var p in PlayerControl.AllPlayerControls) p.Data.Disconnected=p.OwnerId>capacity;
  LobbyRules.NewRound();LobbyChat.Clear();PlayerControl.Sent.Clear();LobbyRules.Tick();Time.realtimeSinceStartup+=5;LobbyRules.Tick();LobbyChat.Tick();
  Check(PlayerControl.Sent.Count==1,$"full {capacity}-player lobby posts rules");
 }
 GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers=0;LobbyRules.Reset();LobbyChat.Clear();PlayerControl.Sent.Clear();LobbyRules.Tick();Time.realtimeSinceStartup+=5;LobbyRules.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==0,"unknown lobby capacity never triggers rules");
 GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers=15;
 // Combined limits apply to the whole message; never send a truncated first rule.
 int bodyBudget=LobbyChat.MaximumPublicMessageLength-LobbyChat.CombinedMessage(new[]{"x"}).Length+1;
 string exactRules=new string('a',bodyBudget-4)+"\nb";
 Check(LobbyRules.SaveText(exactRules),"exact combined limit can be saved");
 bool savedFullBody=LobbyRules.SaveText(new string('a',bodyBudget));
 Check(savedFullBody&&bodyBudget==100&&LobbyRules.DraftMessageInfo(new string('a',bodyBudget)).Contains("100/100"),"single body permits all 100 characters with no label");
 Check(LobbyRules.SaveText(exactRules),"restore exact combined rules");
 LobbyRules.NewRound();LobbyChat.Clear();PlayerControl.Sent.Clear();LobbyRules.Tick();Time.realtimeSinceStartup+=5;LobbyRules.Tick();LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==1&&PlayerControl.Sent[0].Length==100&&PlayerControl.Sent[0].EndsWith(" / b"),"exact full-message boundary sends every rule once");
 string tooLongRules=exactRules+"b";Check(!LobbyRules.SaveText(tooLongRules)&&File.ReadAllText(LobbyRules.FilePath)==exactRules,"overlong combined edit preserves previous saved rules");
 Check(LobbyRules.DraftMessageInfo(tooLongRules).Contains("101/100")&&LobbyRules.DraftMessageInfo(tooLongRules).Contains("Too long"),"draft counter includes separators");
 File.WriteAllText(LobbyRules.FilePath,tooLongRules);LobbyRules.Reload(); // Older files are not destroyed.
 LobbyChat.Clear();PlayerControl.Sent.Clear();LobbyRules.Tick();Time.realtimeSinceStartup+=5;LobbyRules.Tick();LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==0&&LobbyRules.Status.Contains("Too long")&&LobbyRules.Preview==tooLongRules,"oversized combined rules produce a clear warning, no truncation or split");
 Check(!LobbyChat.QueueRules(tooLongRules.Split('\n')),"queue independently rejects oversized combined rules");
 Check(LobbyChat.CombinedMessage(new[]{"<b>No cheats</b>","Finish tasks"}).Contains(" / Finish tasks"),"rule separators survive sanitizing");
 Check(LobbyRules.SaveText(string.Join("\n",Enumerable.Range(1,8).Select(i=>"Rule "+i))),"eight short rules can be saved");
 LobbyRules.NewRound();LobbyChat.Clear();PlayerControl.Sent.Clear();LobbyRules.Tick();Time.realtimeSinceStartup+=5;LobbyRules.Tick();chat.timeSinceLastMessage=0;LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==0,"combined rules wait when user just sent free or quick chat");
 Time.realtimeSinceStartup+=3;LobbyChat.Tick();Check(PlayerControl.Sent.Count==0,"combined rules respect native cooldown and margin");
 Time.realtimeSinceStartup+=0.25f;LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==1&&Enumerable.Range(1,8).All(i=>PlayerControl.Sent[0].Contains("Rule "+i))&&chat.timeSinceLastMessage==0,"all eight short rules fit one chat bubble and consume shared cooldown");
 Time.realtimeSinceStartup+=30;LobbyRules.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==1,"eight saved rules never become eight sends");
 LobbyRules.DelaySeconds=0;Check(LobbyRules.DelaySeconds==1,"delay lower bound");LobbyRules.DelaySeconds=999;Check(LobbyRules.DelaySeconds==120,"delay upper bound");
}
finally { LobbyRules.Enabled=false;Directory.Delete(rulesDirectory,true); }
client.GameId++;LobbyLevelPolicy.Reset();client.Removals.Clear();LobbyChat.Clear();PlayerControl.Sent.Clear();
client.Clients.Clear();PlayerControl.AllPlayerControls.Clear();host=Add(1,0,"host#1111");PlayerControl.LocalPlayer=host;low=Add(2,0,"low#2222");
LobbyLevelPolicy.Enabled=true;Scan();Time.realtimeSinceStartup+=3;Scan();
Check(client.Removals.Count==0,"matching default zero records cannot ban a still-initializing player as level one");
low.Data.PlayerLevel=client.Clients[2].PlayerLevel=1;
LobbyLevelPolicy.Enabled=true;client.BanAllowed=false;Scan();Time.realtimeSinceStartup+=3;Scan();Check(client.Removals.Count==0&&LobbyLevelPolicy.Enabled,"rule stays configurable when vanilla ban permission unavailable");
client.BanAllowed=true;client.ClientId=99;Scan();Check(client.Removals.Count==0,"unsettled local host ID cannot ban");client.ClientId=1;
client.Clients[2].Id=3;Scan();Check(client.Removals.Count==0,"mismatched target client record cannot be banned");client.Clients[2].Id=2;
Scan();Check(client.Removals.SequenceEqual(new[]{(2,true)}),"existing eligible player receives normal ban after permissions settle");
LobbyChat.Tick();Check(PlayerControl.Sent.Count==0,"entry-level bans send no public chat");
LobbyChat.ProtectionNotice();LobbyChat.Tick();Check(PlayerControl.Sent.Count==0,"unknown protection events send no public accusations");
LobbyChat.EvidenceNotice("Player","detected","invalid role action");LobbyChat.Tick();Check(PlayerControl.Sent.Single()=="[AU-Anticheat] Player got detected by anticheat for invalid role action","evidence-based announcement format");
string loadoutDirectory=Path.Combine(AppContext.BaseDirectory,"au-loadouts-tests-"+Guid.NewGuid().ToString("N"));
try
{
 var store=new LoadoutStore(loadoutDirectory);
 Check(!store.Save("../escape","build",new byte[]{1}) && !store.Save("", "build",new byte[]{1}),"loadout names cannot become paths or be empty");
 Check(store.Save("Normal Lobby","build",new byte[]{1,2,3}),"named loadout saves atomically");
 Check(!store.Save("normal lobby","build",new byte[]{4}),"case-insensitive overwrite requires separate deletion");
 store=new LoadoutStore(loadoutDirectory);
 Check(store.Find("NORMAL LOBBY").Payload=="AQID" && store.Names.Length==1,"loadout data survives reload");
 Check(!store.Save("Large","build",new byte[4097]),"oversized loadout rejected");
 for(int i=1;i<20;i++) Check(store.Save("Loadout "+i,"build",new byte[]{1}),"bounded valid loadout stored");
 Check(!store.Save("Overflow","build",new byte[]{1}),"loadout count capped at twenty");
 Check(store.Delete("Normal Lobby") && new LoadoutStore(loadoutDirectory).Find("Normal Lobby")==null,"delete persists");
 LobbyLoadouts.Initialize(loadoutDirectory);
 client.AmHost=true;client.ClientId=client.HostId;GameManager.Instance.GameHasStarted=false;
 client.GameState=InnerNetClient.GameStates.Joined;MeetingHud.Instance=null;
 GameStartManager.Instance.startState=GameStartManager.StartingStates.NotStarting;
 GameOptionsManager.Instance.CurrentGameOptions=new(){KillCooldown=42};
 Check(LobbyLoadouts.Save("Live settings"),"production loadout saves current host settings");
 GameOptionsManager.Instance.CurrentGameOptions.KillCooldown=10;
 client.AmHost=false;Check(!LobbyLoadouts.Apply("Live settings"),"nonhost apply denied");client.AmHost=true;
 GameStartManager.Instance.startState=GameStartManager.StartingStates.Countdown;
 Check(!LobbyLoadouts.Apply("Live settings"),"countdown apply denied");GameStartManager.Instance.startState=GameStartManager.StartingStates.NotStarting;
 GameOptionsManager.Instance.CurrentGameOptions.MapId=1;
 Check(!LobbyLoadouts.Apply("Live settings"),"map mismatch never silently applied");GameOptionsManager.Instance.CurrentGameOptions.MapId=0;
 GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers=4;
 Check(!LobbyLoadouts.Apply("Live settings"),"capacity mismatch never evicts players");GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers=15;
 Application.version="new-build";Check(!LobbyLoadouts.Apply("Live settings"),"incompatible game build rejected");Application.version="test-build";
 Time.realtimeSinceStartup+=10;
 Check(LobbyLoadouts.Apply("Live settings") && GameOptionsManager.Instance.CurrentGameOptions.KillCooldown==42 && GameManager.Instance.LogicOptions.Syncs==1,"production apply syncs parsed options through normal game method");
 Check(!LobbyLoadouts.Apply("Live settings") && GameManager.Instance.LogicOptions.Syncs==1,"repeated apply paced");
 string file=Path.Combine(loadoutDirectory,"lobby-loadouts.json");File.WriteAllText(file,"broken");
 store=new LoadoutStore(loadoutDirectory);
 Check(!store.Save("Recovery","build",new byte[]{1}) && File.ReadAllText(file)=="broken","corrupt loadout file never silently overwritten");
}
finally { Directory.Delete(loadoutDirectory,true); }
// All automatic chat shares the host's actual cooldown, never a private bypass timer.
LobbyChat.Clear();PlayerControl.Sent.Clear();Time.realtimeSinceStartup+=100;
client.ClientId=client.HostId=host.OwnerId;client.AmHost=true;client.GameState=InnerNetClient.GameStates.Joined;
client.Clients[client.ClientId]=new(){Id=client.ClientId,Character=host};PlayerControl.LocalPlayer=host;
host.Data.IsDead=false;host.Data.Disconnected=false;MeetingHud.Instance=null;GameManager.Instance.GameHasStarted=false;
GameStartManager.Instance.startState=GameStartManager.StartingStates.NotStarting;
chat=HudManager.Instance.Chat;chat.timeSinceLastMessage=0;
LobbyChat.EvidenceNotice("Target","detected","invalid action");
Time.realtimeSinceStartup+=3;LobbyChat.Tick();Check(PlayerControl.Sent.Count==0,"evidence alert cannot jump native cooldown");
Time.realtimeSinceStartup+=0.25f;LobbyChat.Tick();Check(PlayerControl.Sent.Count==1&&chat.timeSinceLastMessage==0,"evidence alert consumes shared cooldown");
LobbyChat.Clear();LobbyChat.QueueNotice("after clearing");LobbyChat.Tick();Check(PlayerControl.Sent.Count==1,"clearing queue cannot bypass cooldown");
Time.realtimeSinceStartup+=4;
var observedChat=Hook(typeof(LobbyChat),"ObserveLocalChat","Postfix");
chat.timeSinceLastMessage=0; // Native manual/quick-chat sending consumes its own timer.
observedChat.Invoke(null,new object[]{host,true});LobbyChat.Tick();Check(PlayerControl.Sent.Count==1,"successful manual chat postpones queued alert");
Time.realtimeSinceStartup+=2;observedChat.Invoke(null,new object[]{low,true});
Time.realtimeSinceStartup+=1.25f;LobbyChat.Tick();Check(PlayerControl.Sent.Count==2,"other player's chat does not postpone the host queue");
Time.realtimeSinceStartup+=4;chat.timeSinceLastMessage=10;
observedChat.Invoke(null,new object[]{host,false});Check(chat.timeSinceLastMessage==10,"rejected manual send does not consume cooldown");
chat.timeSinceLastMessage=-2;observedChat.Invoke(null,new object[]{host,true});Check(chat.timeSinceLastMessage==-2,"observing manual chat never shortens a native cooldown penalty");
Time.realtimeSinceStartup+=4;
ChatController.MAX_CHAT_SEND_RATE=6;chat.timeSinceLastMessage=3.25f;LobbyChat.QueueNotice("longer native cooldown");
LobbyChat.Tick();Check(PlayerControl.Sent.Count==2,"native cooldown longer than three seconds is respected");
Time.realtimeSinceStartup+=3;LobbyChat.Tick();Check(PlayerControl.Sent.Count==3,"longer native cooldown plus margin permits one send");
ChatController.MAX_CHAT_SEND_RATE=3;Time.realtimeSinceStartup+=7;
AmongUs.Data.DataManager.Settings.Multiplayer.ChatMode=QuickChatModes.QuickChatOnly;LobbyChat.QueueNotice("quick-chat restricted");
LobbyChat.Tick();Check(PlayerControl.Sent.Count==3,"free text automation never overrides quick-chat-only restriction");
Time.realtimeSinceStartup+=21;LobbyChat.Tick();AmongUs.Data.DataManager.Settings.Multiplayer.ChatMode=QuickChatModes.FreeChatOrQuickChat;
LobbyChat.Tick();Check(PlayerControl.Sent.Count==3,"expired notices do not arrive in a late burst");
LobbyChat.QueueNotice("waiting for HUD");HudManager.Instance.Chat=null;LobbyChat.Tick();Check(PlayerControl.Sent.Count==3,"all automatic chat waits for HUD");HudManager.Instance.Chat=chat;
client.Clients[client.ClientId].Character=low;LobbyChat.Tick();Check(PlayerControl.Sent.Count==3,"mismatched local player binding cannot send");client.Clients[client.ClientId].Character=host;
GameStartManager.Instance.startState=GameStartManager.StartingStates.Countdown;LobbyChat.Tick();Check(PlayerControl.Sent.Count==3,"no automatic chat during countdown");
GameStartManager.Instance.startState=GameStartManager.StartingStates.NotStarting;
client.AmHost=false;LobbyChat.Tick();client.AmHost=true;LobbyChat.Tick();Check(PlayerControl.Sent.Count==3,"host authority loss cancels pending notices");
LobbyChat.QueueNotice("native refusal");PlayerControl.RejectChat=true;chat.timeSinceLastMessage=10;LobbyChat.Tick();
Check(PlayerControl.Sent.Count==3&&chat.timeSinceLastMessage==10,"native rejected automatic send is not displayed or treated as successful");
PlayerControl.RejectChat=false;Time.realtimeSinceStartup+=4;LobbyChat.Tick();Check(PlayerControl.Sent.Count==3,"rejected automatic message is not retried");
LobbyChat.QueueNotice("invalid timer");chat.timeSinceLastMessage=float.NaN;LobbyChat.Tick();Check(PlayerControl.Sent.Count==3,"invalid native timer fails closed");chat.timeSinceLastMessage=10;
LobbyChat.Clear();LobbyChat.QueueNotice("first");LobbyChat.QueueNotice("second");LobbyChat.Tick();LobbyChat.Tick();
Check(PlayerControl.Sent.Count==4,"one automatic line maximum per eligible frame");
Time.realtimeSinceStartup+=3;LobbyChat.Tick();Check(PlayerControl.Sent.Count==4,"consecutive automatic notices share margin");
Time.realtimeSinceStartup+=0.25f;LobbyChat.Tick();Check(PlayerControl.Sent.Count==5,"second alert sent only after shared cooldown");
Time.realtimeSinceStartup+=4;LobbyChat.QueueNotice("meeting alert");MeetingHud.Instance=new();client.GameState=InnerNetClient.GameStates.Started;
LobbyChat.Tick();Check(PlayerControl.Sent.Count==5,"stale meeting HUD after game end cannot send notices");
GameManager.Instance.GameHasStarted=true;LobbyChat.Tick();Check(PlayerControl.Sent.Count==6,"living host can send paced evidence notice in real meeting");
Time.realtimeSinceStartup+=4;LobbyChat.QueueNotice("dead host");host.Data.IsDead=true;LobbyChat.Tick();Check(PlayerControl.Sent.Count==6,"dead host never sends automated public notice");
host.Data.IsDead=false;MeetingHud.Instance=null;GameManager.Instance.GameHasStarted=false;client.GameState=InnerNetClient.GameStates.Joined;LobbyChat.Clear();
// A completed report is still rechecked at the moment each line would send.
host.Data.RoleType=RoleTypes.Impostor;
RoundReports.Begin();Time.realtimeSinceStartup+=10;end.Invoke(null,new object[]{new EndGameResult{GameOverReason=GameOverReason.CrewmatesByTask}});
RoundReports.Tick();Time.realtimeSinceStartup+=5;RoundReports.Tick();
GameStartManager.Instance.startState=GameStartManager.StartingStates.Countdown;LobbyChat.Tick();Check(PlayerControl.Sent.Count==6,"queued report rechecks readiness at send time");RoundReports.Tick();
GameStartManager.Instance.startState=GameStartManager.StartingStates.NotStarting;RoundReports.Tick();Time.realtimeSinceStartup+=2;RoundReports.Tick();LobbyChat.Tick();
Check(PlayerControl.Sent.Count==6,"cancelled countdown restarts return grace period");
Time.realtimeSinceStartup+=3;RoundReports.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==7,"queued report resumes after continuous readiness");
RoundReports.Begin();Time.realtimeSinceStartup+=4;RoundReports.Tick();LobbyChat.Tick();Check(PlayerControl.Sent.Count==7,"new round cancels unsent report lines");
// Standard one-to-three-impostor matches always have room for every role/kill count.
for(int length=1;length<=22;length++) for(int count=1;count<=3;count++)
{
 var compact=new RoundLedger();compact.Begin(0);
 Check(compact.ChatSummary(100)==null,"compact summary never leaks a running match");
 for(uint i=1;i<=count;i++)
 {
  compact.Observe(i,new string((char)('A'+i-1),length),new[]{"Impostor","Shapeshifter","Phantom"}[i-1],true);
  for(uint victim=0;victim<9;victim++)compact.Kill(i,100+i*10+victim);
 }
 compact.End(86400,3);string summary=compact.ChatSummary(100);
 Check(summary!=null&&LobbyChat.CombinedMessage(new[]{summary})==summary&&summary.Length<=100,"compact full result fits 100 without a label even at longest duration");
 Check(summary.Contains("Impostors won 1440:00"),"compact result retains winner and full duration");
 for(int i=0;i<count;i++)Check(summary.Contains($"({new[]{"I","S","P"}[i]}):9k"),"each impostor retains role and exact observed kill count");
}
var crowded=new RoundLedger();crowded.Begin(0);for(uint i=1;i<=15;i++)crowded.Observe(i,"LongPlayerName","Impostor",true);crowded.End(10,1);
Check(crowded.ChatSummary(100)==null,"abnormal roster cannot silently drop impostors to fit");
Check(!LobbyChat.QueueReport(new[]{new string('x',101)}),"report queue rejects oversized messages independently");
// Regression: a game build that renders RpcSendChat itself gets no extra bubble.
LobbyChat.Clear();Time.realtimeSinceStartup+=10;chat.timeSinceLastMessage=20;
int renderedBefore=chat.Rendered;
PlayerControl.RenderChatDuringSend=true;LobbyChat.QueueNotice("render once");LobbyChat.Tick();
Check(chat.Rendered==renderedBefore+1,"native rendering does not get a duplicate fallback bubble");
PlayerControl.RenderChatDuringSend=false;Time.realtimeSinceStartup+=4;LobbyChat.QueueNotice("fallback once");LobbyChat.Tick();
Check(chat.Rendered==renderedBefore+2,"missing native rendering gets exactly one fallback bubble");

// Regression: retain roles even when roleAssigned is delayed, on duplicate
// StartGame callbacks, and after the impostor leaves before Play Again.
Hook(typeof(RoundReports),"ClearLobbyServices","Postfix").Invoke(null,null);
client.Clients.Clear();PlayerControl.AllPlayerControls.Clear();
host=Add(1,30,"host#1111");host.Data.RoleType=RoleTypes.Crewmate;PlayerControl.LocalPlayer=host;
low=Add(2,40,"left#2222");low.Data.RoleType=RoleTypes.Shapeshifter;low.roleAssigned=false;
RoundReports.Begin();Time.realtimeSinceStartup+=5;
low.Data.RoleType=RoleTypes.Crewmate;RoundReports.Begin();RoundReports.Tick();
Hook(typeof(RoundReports),"PreserveLeavingRole","Prefix").Invoke(null,new object[]{client.Clients[2]});
PlayerControl.AllPlayerControls.Remove(low);client.Clients.Remove(2);
Time.realtimeSinceStartup+=5;end.Invoke(null,new object[]{new EndGameResult{GameOverReason=GameOverReason.ImpostorsByKill}});
RoundReports.Tick();Time.realtimeSinceStartup+=5;RoundReports.Tick();LobbyChat.Tick();
Check(PlayerControl.Sent.Last().Contains("Player2(S):0k")&&PlayerControl.Sent.Last().Contains("0:10"),"departed impostor and original start time survive duplicate begin/default crew snapshot");
int reportCount=PlayerControl.Sent.Count;end.Invoke(null,new object[]{new EndGameResult{GameOverReason=GameOverReason.ImpostorsByKill}});
RoundReports.Tick();Time.realtimeSinceStartup+=4;LobbyChat.Tick();
Check(PlayerControl.Sent.Count==reportCount,"duplicate end callback cannot repost results");

// Regression: timed level-1 entry policy, never a cheat accusation.
var appliedLevel=Hook(typeof(LobbyLevelPolicy),"ObserveAppliedLevel","Postfix");
void LevelUpdate(PlayerControl p,uint raw){p.SetLevel(raw);appliedLevel.Invoke(null,new object[]{p,raw});}
client.GameId++;LobbyLevelPolicy.Reset();LobbyLevelPolicy.Enabled=true;LobbyLevelPolicy.MinimumLevel=10;client.Removals.Clear();
low=Add(2,0,"levelone#2222");LevelUpdate(low,0);Scan();Time.realtimeSinceStartup+=2;Scan();
Time.realtimeSinceStartup+=4.7f;Scan();Check(client.Removals.Count==0,"level one is not banned before five stable seconds");
Scan();Check(client.Removals.SequenceEqual(new[]{(2,true)}),"explicit level-one update gets one ban after five stable seconds");
client.GameId++;LobbyLevelPolicy.Reset();LevelUpdate(low,0);client.Removals.Clear();FriendCodeExceptions.Add("levelone#2222");Scan();Time.realtimeSinceStartup+=2;Scan();Time.realtimeSinceStartup+=6;Scan();
Check(client.Removals.Count==0,"level-one friend-code exception remains exempt after timeout");
FriendCodeExceptions.Remove("levelone#2222");client.Clients[2].PlayerLevel=20;Scan();Time.realtimeSinceStartup+=10;Scan();
Check(client.Removals.Count==0,"conflicting high peer level never becomes level-one ban");
client.Clients[2].PlayerLevel=0;LevelUpdate(low,0);Scan();Time.realtimeSinceStartup+=40;Scan();Check(client.Removals.Count==0,"earlier higher level prevents subsequent zero glitch being called level one");
low.NetId++;Scan();Time.realtimeSinceStartup+=4;Scan();Check(client.Removals.Count==0,"replacement player cannot inherit old level-one timeout");
LevelUpdate(low,0);Scan();Time.realtimeSinceStartup+=5;Scan();Check(client.Removals.Count==1,"replacement needs its own explicit level update and stable interval");

client.GameId++;LobbyLevelPolicy.Reset();client.Removals.Clear();LobbyLevelPolicy.Enabled=true;
low.NetId++;low.Data.PlayerLevel=client.Clients[2].PlayerLevel=0;
Scan();Time.realtimeSinceStartup+=30;Scan();Check(client.Removals.Count==0,"zero loading values never become real level one merely by waiting");
low.Data.IsIncomplete=true;LevelUpdate(low,0);Time.realtimeSinceStartup+=10;Scan();Check(client.Removals.Count==0,"explicit update does not override incomplete data");
low.Data.IsIncomplete=false;Scan();Time.realtimeSinceStartup+=4;Scan();Check(client.Removals.Count==0,"complete data starts its own five-second level-one interval");
Time.realtimeSinceStartup+=1;Scan();Check(client.Removals.Count==1,"confirmed complete level-one data is still filtered");
client.GameId++;LobbyLevelPolicy.Reset();client.Removals.Clear();
low.Data.PlayerLevel=7;client.Clients[2].PlayerLevel=80;Scan();Time.realtimeSinceStartup+=3;Scan();
Check(client.Removals.SequenceEqual(new[]{(2,true)}),"live low level is filtered despite stale higher join snapshot");
client.GameId++;LobbyLevelPolicy.Reset();client.Removals.Clear();
low.Data.PlayerLevel=80;client.Clients[2].PlayerLevel=7;Scan();Time.realtimeSinceStartup+=3;Scan();
Check(client.Removals.Count==0,"live high level wins over stale low join snapshot");
low.Data.PlayerLevel=0;client.Clients[2].PlayerLevel=7;Scan();
Check(client.Removals.Count==0,"live high-to-zero glitch cannot fall back to a known conflicting low join snapshot");
low.NetId++;Scan();Check(client.Removals.SequenceEqual(new[]{(2,true)}),"nonzero join level is usable on a fresh object without conflicting live history");
client.GameId++;LobbyLevelPolicy.Reset();client.Removals.Clear();
low.Data.PlayerLevel=7;client.Clients[2].PlayerLevel=0;low.Data.IsIncomplete=true;Scan();Time.realtimeSinceStartup+=3;Scan();
Check(client.Removals.Count==0,"loading player is not filtered even with a nonzero snapshot");
low.Data.IsIncomplete=false;Scan();Check(client.Removals.Count==1,"loaded low level works even when join level remains zero");
client.GameId++;LobbyLevelPolicy.Reset();client.Removals.Clear();
low.Data.PlayerLevel=client.Clients[2].PlayerLevel=uint.MaxValue;Scan();Time.realtimeSinceStartup+=10;Scan();
Check(client.Removals.Count==0,"unknown sentinel never becomes a level");
low.Data.PlayerLevel=7;client.Clients[2].PlayerLevel=uint.MaxValue;Scan();Check(client.Removals.Count==1,"valid live level is independent of unknown peer sentinel");

// A fresh, short extension offer must not be trapped behind a 30-second gate.
client.GameId++;LobbyExtension.Reset();LobbyExtension.Tick();int requests=lobby.Requests;
offer.Invoke(null,new object[]{lobby,20,true,client.HostId,100});LobbyExtension.Tick();
Time.realtimeSinceStartup+=1;offer.Invoke(null,new object[]{lobby,8,true,client.HostId,101});LobbyExtension.Tick();
Check(lobby.Requests==requests+1,"new extension offer still respects request pacing");
Time.realtimeSinceStartup+=5;LobbyExtension.Tick();Check(lobby.Requests==requests+2,"short fresh extension offer accepted before it expires");
Hook(typeof(RoundReports),"ClearLobbyServices","Postfix").Invoke(null,null);
host.Data.RoleType=RoleTypes.Crewmate;low.Data.RoleType=RoleTypes.Crewmate;RoundReports.Begin();
Hook(typeof(RoundReports),"CaptureAssignedRole","Postfix").Invoke(null,new object[]{low,RoleTypes.Phantom});
low.Data.RoleType=RoleTypes.ImpostorGhost;low.Data.RoleWhenAlive=null;RoundReports.Tick();
end.Invoke(null,new object[]{new EndGameResult{GameOverReason=GameOverReason.ImpostorsByKill}});
var savedLedger=(RoundLedger)typeof(RoundReports).GetField("ledger",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
Check(savedLedger.ChatSummary(100).Contains("Player2(P):0k"),"actual assignment captured before missing ghost-alive-role data");
var incomplete=new RoundLedger();incomplete.Begin(0);incomplete.Observe(1,"Crew","Crewmate",false);incomplete.End(4,1);
Check(incomplete.ChatSummary(100)==null&&incomplete.Lines().Last().Contains("No impostor role data"),"never publish fabricated or unknown impostor summary");
// Moderation is deliberately separate from cheating evidence and gameplay RPCs.
Check(RoundReports.ReturnDelay==5,"BanMod-style five-second return delay retained");
LobbyChat.Clear();Time.realtimeSinceStartup+=30;chat.timeSinceLastMessage=30;
int networkBefore=PlayerControl.Sent.Count;int bubbles=chat.Rendered;
LobbyChat.QueueNotice("delayed echo regression");LobbyChat.Tick();
string echoText=PlayerControl.Sent.Last();Time.realtimeSinceStartup+=1;
chat.AddChat(host,echoText);chat.AddChat(host,echoText);
Check(PlayerControl.Sent.Count==networkBefore+1&&chat.Rendered==bubbles+1,"delayed native echo is suppressed after fallback; one network send");
chat.AddChat(low,echoText);Check(chat.Rendered==bubbles+2,"other players with identical text remain visible");
Hook(typeof(LobbyChat),"ObserveLocalChat","Prefix").Invoke(null,new object[]{host,echoText});
chat.AddChat(host,echoText);Check(chat.Rendered==bubbles+3,"deliberate manual repeat is not hidden");
foreach(string ad in new[]{"join my lobby ABCDEF","join room, code: ABCDEF","discord.gg/test","d\u200biscord.gg/test","ｄｉｓｃｏｒｄ.gg/test","entra en mi sala"})
 Check(ChatContentRules.IsAd(ad),"recognized local ad: "+ad);
foreach(string normal in new[]{"red vented","please start","what code should I join","I saw blue enter medbay","finish tasks","ABCDEF",new string('x',1601)})
 Check(!ChatContentRules.IsAd(normal),"ordinary chat not an ad: "+normal.Substring(0,Math.Min(50,normal.Length)));
Check(ChatContentRules.MatchesLiteral("bad phrase here",new[]{"bad phrase"})&&!ChatContentRules.MatchesLiteral("classmate",new[]{"ass",""}),"literal whole-word filters, empty filter ignored");
string moderationDirectory=Path.Combine(AppContext.BaseDirectory,"au-moderation-tests-"+Guid.NewGuid().ToString("N"));
try
{
 ModerationStore.Initialize(moderationDirectory);
 Check(ModerationStore.Set(" Test#1234 ","watch",true)&&ModerationStore.Contains("test#1234","watch")&&!ModerationStore.Contains("test#1234","ban"),"watchlist is separate from bans");
 Check(!ModerationStore.Set("bad id","ban",true)&&!ModerationStore.Set("test#1234","other",true),"invalid list entries refused");
 ModerationStore.Reload();Check(ModerationStore.Contains("test#1234","watch"),"local list survives reload");
 string listFile=Path.Combine(moderationDirectory,"moderation-list.json"),good=File.ReadAllText(listFile);
 File.WriteAllText(listFile,"broken");ModerationStore.Reload();
 Check(ModerationStore.Contains("test#1234","watch")&&!ModerationStore.Set("new#1234","ban",true)&&File.ReadAllText(listFile)=="broken","corrupt list retains prior memory and cannot be overwritten");
 File.WriteAllText(listFile,good);ModerationStore.Reload();
 Check(ModerationStore.Set("test#1234","watch",false)&&!ModerationStore.Contains("test#1234","watch"),"list removal persists");
 client.GameId++;LobbyLevelPolicy.Reset();client.Removals.Clear();client.Clients.Clear();PlayerControl.AllPlayerControls.Clear();
 host=Add(1,40,"host#1111");PlayerControl.LocalPlayer=host;low=Add(2,40,"guest#2222");
 client.AmHost=true;client.ClientId=client.HostId=1;client.GameState=InnerNetClient.GameStates.Joined;
 GameManager.Instance.GameHasStarted=false;MeetingHud.Instance=null;GameStartManager.Instance.startState=GameStartManager.StartingStates.NotStarting;
 ChatModeration.Spam=false;ChatModeration.Ads=true;ChatModeration.WordFilter=ChatModeration.NameFilter=false;
 Check(!ChatModeration.Allow(low,"join my lobby ABCDEF")&&ChatModeration.Allow(host,"join my lobby ABCDEF")&&client.Removals.Count==0,"ads hide locally without banning and skip own chat");
 Check(ModerationStore.Set(low.Data.FriendCode,"block",true)&&!ChatModeration.Allow(low,"hello"),"local block list hides bound player's chat");
 client.Clients[2].FriendCode="mismatch#1234";Check(ChatModeration.Allow(low,"hello"),"conflicting identity cannot inherit block list");client.Clients[2].FriendCode=low.Data.FriendCode;
 ModerationStore.Set(low.Data.FriendCode,"block",false);ChatModeration.WordFilter=true;ModerationStore.Words=new[]{"bad phrase"};
 Check(!ChatModeration.Allow(low,"bad phrase")&&ChatModeration.Allow(low,"good phrase"),"word filter local-only literal matching");
 ChatModeration.WordFilter=false;ChatModeration.NameFilter=true;ModerationStore.Names=new[]{low.Data.PlayerName};
 Check(!ChatModeration.Allow(low,"hello"),"name filter hides chat without changing player name or removing player");ChatModeration.NameFilter=false;
 ChatModeration.Spam=true;client.GameId++;
 for(int i=0;i<5;i++)Check(ChatModeration.Allow(low,"normal "+i),"initial chat burst allowed within threshold");
 Check(!ChatModeration.Allow(low,"sixth")&&client.Removals.Count==0,"chat flood locally hidden without punishment");
 Time.realtimeSinceStartup+=10;Check(ChatModeration.Allow(low,"later"),"spam window expires");
 low.NetId++;Check(ChatModeration.Allow(low,"new player object"),"replacement object starts fresh spam state");
 ModerationStore.EnforceBans=true;LobbyLevelPolicy.Enabled=false;
 Check(ModerationStore.Set(low.Data.FriendCode,"ban",true),"persistent ban saved");
 ModerationStore.Set(host.Data.FriendCode,"ban",true);
 client.AmHost=false;Scan();Time.realtimeSinceStartup+=3;Scan();Check(client.Removals.Count==0,"persistent ban never enforced as nonhost");
 client.AmHost=true;client.Clients[2].FriendCode="mismatch#1234";Scan();Time.realtimeSinceStartup+=3;Scan();Check(client.Removals.Count==0,"ban list requires bound identity and excludes host");
 client.Clients[2].FriendCode=low.Data.FriendCode;FriendCodeExceptions.Add(low.Data.FriendCode);Scan();
 Check(client.Removals.SequenceEqual(new[]{(2,true)}),"persistent ban works with minimum-level disabled; level exception does not override explicit ban");
 Time.realtimeSinceStartup+=16;Scan();Check(!ModerationStore.EnforceBans&&client.Removals.Count==1,"unacknowledged persistent ban pauses admission without retries");
 int removalCount=client.Removals.Count;
 var kickGuard=Hook(typeof(RemovalRequestGuard),"BeforeKickOrBan","Prefix");var lateGuard=Hook(typeof(RemovalRequestGuard),"BeforeLateRejection","Prefix");
 bool Allowed(MethodInfo guard,int target)=>(bool)guard.Invoke(null,new object[]{client,target})!;
 Check(Allowed(kickGuard,2)&&!Allowed(kickGuard,1),"ordinary host moderation allowed; self kick/ban denied");
 client.AmHost=false;Check(!Allowed(kickGuard,2)&&!Allowed(lateGuard,2),"nonhost local removal requests denied");client.AmHost=true;
 client.ClientId=99;Check(!Allowed(kickGuard,2)&&!Allowed(lateGuard,2),"unsettled host authority denied");client.ClientId=1;
 client.Clients[2].IsReady=true;Check(!Allowed(lateGuard,2),"late rejection of ready client denied");
 client.Clients[2].IsReady=false;Check(Allowed(lateGuard,2),"legitimate not-ready startup rejection retained");
 RemovalRequestGuard.Enabled=false;Check(Allowed(kickGuard,1)&&Allowed(lateGuard,1),"removal guard can be disabled");RemovalRequestGuard.Enabled=true;
 ChatModeration.AfkAlerts=true;ChatModeration.AfkSeconds=60;LobbyIdleMonitor.Tick();
 int afkBefore=AUAnticheat.AUAnticheatPlugin.notifications.Messages.Count(m=>m.Title=="Possible AFK");
 Time.realtimeSinceStartup+=59;LobbyIdleMonitor.Tick();Check(AUAnticheat.AUAnticheatPlugin.notifications.Messages.Count(m=>m.Title=="Possible AFK")==afkBefore,"AFK timer waits full interval");
 low.Position=new(1,1);LobbyIdleMonitor.Tick();Time.realtimeSinceStartup+=59;LobbyIdleMonitor.Tick();
 Check(AUAnticheat.AUAnticheatPlugin.notifications.Messages.Count(m=>m.Title=="Possible AFK")==afkBefore,"movement resets AFK timer");
 LobbyIdleMonitor.Touch(low);Time.realtimeSinceStartup+=59;LobbyIdleMonitor.Tick();Check(AUAnticheat.AUAnticheatPlugin.notifications.Messages.Count(m=>m.Title=="Possible AFK")==afkBefore,"chat resets AFK timer");
 Time.realtimeSinceStartup+=1;LobbyIdleMonitor.Tick();LobbyIdleMonitor.Tick();
 Check(AUAnticheat.AUAnticheatPlugin.notifications.Messages.Count(m=>m.Title=="Possible AFK")==afkBefore+1&&client.Removals.Count==removalCount,"one AFK reminder, no kick");
 ChatModeration.AfkAlerts=false;LobbyIdleMonitor.Tick();
 LobbyChat.Clear();Time.realtimeSinceStartup+=30;chat.timeSinceLastMessage=30;
 LobbyRules.Initialize(moderationDirectory,false,1,(_,_)=>{});LobbyRules.SaveText("Be kind");LobbyRules.Enabled=true;
 Add(3,20,"third#3333");Add(4,20,"fourth#4444");
 GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers=4;LobbyRules.Tick();Time.realtimeSinceStartup+=2;LobbyRules.Tick();
 int beforeRules=PlayerControl.Sent.Count;
 Check(LobbyChat.QueueRules(new[]{"Be kind"})&&LobbyChat.QueueRules(new[]{"Be kind"}),"duplicate pending rules coalesced");
 LobbyChat.Tick();Time.realtimeSinceStartup+=4;LobbyChat.Tick();
 Check(PlayerControl.Sent.Count==beforeRules+1,"full timer plus repeated queue produces exactly one rules network send");
}
finally { LobbyRules.Enabled=false;Directory.Delete(moderationDirectory,true); }
Console.WriteLine($"PASS: {assertions} production lobby/report/chat/meeting assertions with managed doubles, not native gameplay.");
