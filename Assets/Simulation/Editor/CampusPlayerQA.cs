using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using CampusMonday;

// Integration checks through a disposable Input System keyboard, never OS input.
[InitializeOnLoad]
public static class CampusPlayerQA {
 static Keyboard keyboard;static CampusPlayer player;static CharacterController body;static double began;static int stage=-1;static Vector3 start;static float jumpBase,maxJump;static bool sawJump;
 [Serializable] class Evidence {public string status;public float walkingMeters,runningMeters,jumpMeters,blockedTravel;public bool grounded,seated,stood;public string[] clips;}
 static Evidence evidence; static bool fullCampus;
 static CampusPlayerQA(){EditorApplication.update+=Tick;}
 static void Begin(){player=UnityEngine.Object.FindFirstObjectByType<CampusPlayer>();if(!player)throw new Exception("Falta jugador");fullCampus=player.campus; if(fullCampus) { player.campus.SetPlayer(true); Require(player.controlled,"El modo tercera persona no se activó"); } else { player.simulation.paused=true;player.simulation.Seek(388);player.ResetPlayer();player.SetControlled(true); } body=player.GetComponent<CharacterController>();
   keyboard=InputSystem.AddDevice<Keyboard>("Campus QA keyboard");keyboard.MakeCurrent();evidence=new Evidence{clips=AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Simulation/Player"}).Select(AssetDatabase.GUIDToAssetPath).Select(Path.GetFileNameWithoutExtension).ToArray()};Next(0);
 }
 static void Next(int value){stage=value;began=EditorApplication.timeSinceStartup;start=player.transform.position;}
 static void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
 static void Require(bool result,string description){if(!result)throw new Exception(description);}
 static void Finish(string status){evidence.status=status;File.WriteAllText("Results/player_qa.json",JsonUtility.ToJson(evidence,true));if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}if(player){player.ResetPlayer();if(fullCampus)player.campus.SetPlayer(false);else player.SetControlled(false);}stage=-1;}
 static void Tick(){
   if(!EditorApplication.isPlaying){if(File.Exists("Library/campus-player-qa")&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)EditorApplication.isPlaying=true;if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}stage=-1;return;}
   try{
          if(stage<0){
       const string campusRequest = "Library/campus-player-qa";
       string request = File.Exists(campusRequest) ? campusRequest : "Tools/player_qa.request";
       if(!File.Exists(request))return;
       if(!UnityEngine.Object.FindAnyObjectByType<CampusPlayer>())return;
       try{File.Delete(request);}catch(IOException){return;}Begin();
     }
     double elapsed=EditorApplication.timeSinceStartup-began;
     if(stage==0){Keys();if(elapsed>1){evidence.grounded=body.isGrounded;Require(evidence.grounded,"El jugador no apoya en el suelo de entrada");Next(1);}}
     else if(stage==1){Keys(Key.W);if(elapsed>1.1){evidence.walkingMeters=Vector3.Distance(start,player.transform.position);Require(evidence.walkingMeters>.6f,"W no desplaza al jugador");Next(2);}}
     else if(stage==2){Keys(Key.W,Key.LeftShift);if(elapsed>1.1){evidence.runningMeters=Vector3.Distance(start,player.transform.position);Require(evidence.runningMeters>evidence.walkingMeters*1.4f,"Shift no aumenta la velocidad");jumpBase=player.transform.position.y;maxJump=0;sawJump=false;Next(3);}}
     else if(stage==3){Keys(elapsed<.15?new[]{Key.Space}:Array.Empty<Key>());maxJump=Mathf.Max(maxJump,player.transform.position.y-jumpBase);sawJump|=player.animator.GetCurrentAnimatorStateInfo(0).IsName("Jump");if(elapsed>1.6){evidence.jumpMeters=maxJump;Require(maxJump>.4f&&sawJump,"El salto físico o su animación no se activó");Next(4);}}
     else if(stage==4 && fullCampus){
       Keys();
       var origin = player.transform.position;
       body.enabled=false;
       player.transform.position=origin + Vector3.right*1200;
       body.enabled=true;
       Next(7);
     }
     else if(stage==7){
       Keys();
       if(elapsed>1.5){
         Require(body.isGrounded && Vector3.Distance(start,player.transform.position)>1000,
           "El jugador no volvió al suelo tras caer fuera del mapa");
         Finish("OK: tercera persona del campus y recuperación");
       }
     }
     else if(stage==4){Keys();var room=player.simulation.rooms[0];Vector3 facing=room.board-room.seats[31];facing.y=0;facing.Normalize();
       body.enabled=false;player.transform.position=room.seats[31]-facing*.55f;body.enabled=true;body.Move(Vector3.down*.15f);
       Require(player.TrySit(),"No se pudo interactuar con una silla libre cercana");Next(5);
     }
     else if(stage==5){Keys();if(elapsed>1){evidence.seated=player.IsSeated&&player.animator.GetCurrentAnimatorStateInfo(0).IsName("Seated");Require(evidence.seated,"Falta estado sentado");player.Stand();Next(6);}}
     else if(stage==6){Keys();if(elapsed>1){evidence.stood=!player.IsSeated&&body.enabled;Require(evidence.stood,"No se completó levantarse");var from=player.transform.position;body.Move(Vector3.right*100);evidence.blockedTravel=Vector3.Distance(from,player.transform.position);Require(evidence.blockedTravel<90,"El personaje atraviesa las paredes");Finish("OK");}}
   }catch(Exception ex){Debug.LogException(ex);if(evidence==null)evidence=new Evidence();Finish("ERROR: "+ex.Message);}
 }
}

