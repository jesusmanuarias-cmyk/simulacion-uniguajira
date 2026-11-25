using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace CampusMonday {
 [RequireComponent(typeof(CharacterController))]
 public class CampusPlayer : MonoBehaviour {
   public FullCampus campus; public RoomDefinition[] campusRooms; public MondaySimulation simulation; public Transform visual; public Animator animator;
   public float walkSpeed=1.8f,runSpeed=5.2f,jumpHeight=1.05f; public bool controlled;
   public string animationState="Idle"; public bool IsSeated=>seated; public bool IsGrounded=>body&&body.isGrounded;
   CharacterController body; Camera cam; Vector3 velocity,smoothVelocity,spawn,standPosition;
   float yaw=180,pitch=18,cameraDistance=4.1f,vertical,seatTime; bool seated,standingUp;
   Vector3 seatStart,seatTarget; Quaternion seatRotation; RoomDefinition selectedRoom; int selectedSeat=-1;
   string prompt=""; int previousFloor;
   Vector3 lastSafeGround;
   float airborneTime;
   bool hasCampusSpawn;
   const int CampusCollisionMask = ~(1 << 2);

   internal bool PrepareCampusControl()
   {
     if (!hasCampusSpawn)
     {
       if (!campus.FindPlayerSpawn(out spawn)) return false;
       hasCampusSpawn = true;
       ResetPlayer();
     }
     campus.PreparePlayerCollisions(transform.position);
     return true;
   }

   void MoveToGround(Vector3 point)
   {
     if (campus) campus.PreparePlayerCollisions(point);
     body.enabled = false;
     transform.position = point;
     body.enabled = true;
     vertical = 0;
     airborneTime = 0;
     velocity = smoothVelocity = Vector3.zero;
     body.Move(Vector3.down * .09f);
   }

   void CheckCampusGround()
   {
     if (!campus) { if (transform.position.y < -8) ResetPlayer(); return; }
     if (body.isGrounded)
     {
       airborneTime = 0;
       // Keep a checkpoint well inside the supporting surface.
       if (Physics.SphereCast(transform.position + Vector3.up * .35f, body.radius*.64f, Vector3.down,
           out var floor, .35f, CampusCollisionMask, QueryTriggerInteraction.Ignore) && floor.normal.y > .7f)
         lastSafeGround = transform.position + Vector3.up * .04f;
       return;
     }

     airborneTime += Time.deltaTime;
     if (vertical < 0 && airborneTime > .55f &&
         (transform.position.y < lastSafeGround.y - 5 ||
          !Physics.Raycast(transform.position + Vector3.up * .15f, Vector3.down, 7,
             CampusCollisionMask, QueryTriggerInteraction.Ignore)))
       MoveToGround(lastSafeGround);
   }
   void Awake(){body=GetComponent<CharacterController>();cam=Camera.main;spawn=transform.position;}
   public void SetControlled(bool enabled){controlled=enabled;velocity=Vector3.zero;
     if(enabled){if(simulation){previousFloor=simulation.floor;simulation.floor=0;}yaw=transform.eulerAngles.y;pitch=18;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
     else {Cursor.lockState=CursorLockMode.None;Cursor.visible=true;if(simulation)simulation.floor=previousFloor;}
   }
   void OnDisable(){if(controlled){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
   public void ResetPlayer()
   {
     seated = standingUp = false;
     lastSafeGround = spawn;
     MoveToGround(spawn);
     Play("Locomotion");
   }
   void Play(string state){if(animator&&animationState!=state){animator.CrossFadeInFixedTime(state,.16f);animationState=state;}}
   bool SeatOccupied(RoomDefinition room,int seat){if(campus)return campus.Occupied(room);return simulation.engine!=null&&simulation.engine.people.Any(p=>p.room==room.id&&p.seat==seat&&p.role==Role.Estudiante&&p.arrival<=simulation.clock&&simulation.clock<p.depart);}
   public bool TrySit(){
     if(seated||!body.isGrounded)return false;float best=1.9f;selectedRoom=null;selectedSeat=-1;
     foreach(var room in (campus?campusRooms:simulation.rooms))for(int i=0;i<room.seats.Length;i++){
       float d=Vector3.Distance(transform.position,room.seats[i]);if(d>=best||SeatOccupied(room,i))continue;
       if(!NavMesh.SamplePosition(transform.position,out var from,.6f,NavMesh.AllAreas))continue;
       var path=new NavMeshPath();if(!NavMesh.CalculatePath(from.position,campus?campus.SeatApproach(room):room.seats[i],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete||PersonPlan.Length(path.corners)>2.8f)continue;
       selectedRoom=room;selectedSeat=i;best=d;
     }
     if(selectedRoom==null){prompt="Acércate a un asiento libre para sentarte.";return false;}
     BeginSit(selectedRoom,selectedSeat);return true;
   }
   public void BeginSit(RoomDefinition room,int seat){selectedRoom=room;selectedSeat=seat;standPosition=transform.position;seatStart=transform.position;seatTarget=room.seats[seat];
     Vector3 facing=room.board-seatTarget;facing.y=0;seatRotation=Quaternion.LookRotation(facing);seated=true;standingUp=false;seatTime=0;body.enabled=false;velocity=Vector3.zero;Play("Seated");}
   public void Stand(){if(!seated||standingUp)return;standingUp=true;seatTime=0;seatStart=transform.position;Play("Locomotion");}
   void Update(){
     if(!controlled)return;if(campus)campus.PreparePlayerCollisions(transform.position);var k=Keyboard.current;var m=Mouse.current;if(k==null)return;
     if(k.escapeKey.wasPressedThisFrame){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
     if(m!=null&&m.leftButton.wasPressedThisFrame&&Cursor.lockState!=CursorLockMode.Locked){if(!simulation||!simulation.PointerOverInterface){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}}
     if(m!=null&&Cursor.lockState==CursorLockMode.Locked){var delta=m.delta.ReadValue();yaw+=delta.x*.12f;pitch=Mathf.Clamp(pitch-delta.y*.1f,-18,65);cameraDistance=Mathf.Clamp(cameraDistance-m.scroll.ReadValue().y*.003f,2,6);}
     if(k.rKey.wasPressedThisFrame)ResetPlayer();
     if(seated){seatTime+=Time.deltaTime;float t=Mathf.SmoothStep(0,1,seatTime/.6f);
       if(standingUp){transform.position=Vector3.Lerp(seatStart,standPosition,t);if(t>=1){seated=false;standingUp=false;body.enabled=true;vertical=-2;Play("Locomotion");}}
       else {transform.position=Vector3.Lerp(seatStart,seatTarget,t);transform.rotation=Quaternion.Slerp(transform.rotation,seatRotation,Time.deltaTime*12);if(seatTime>=.65f)Play("Seated");
         if(k.eKey.wasPressedThisFrame||SeatOccupied(selectedRoom,selectedSeat))Stand();}
       return;
     }
     Vector2 input=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));input=Vector2.ClampMagnitude(input,1);
     Vector3 desired=Quaternion.Euler(0,yaw,0)*new Vector3(input.x,0,input.y);bool run=k.leftShiftKey.isPressed||k.rightShiftKey.isPressed;
     velocity=Vector3.SmoothDamp(velocity,desired*(run?runSpeed:walkSpeed),ref smoothVelocity,.13f);
     if(desired.sqrMagnitude>.01f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(desired),1-Mathf.Exp(-12*Time.deltaTime));
     bool grounded=body.isGrounded;if(grounded&&vertical<0)vertical=-2;
     if(grounded&&k.spaceKey.wasPressedThisFrame){vertical=Mathf.Sqrt(jumpHeight*2*19.6f);Play("Jump");}
     vertical-=19.6f*Time.deltaTime;var flags=body.Move((velocity+Vector3.up*vertical)*Time.deltaTime);if((flags&CollisionFlags.Above)!=0&&vertical>0)vertical=0;
     animator.SetFloat("Speed",new Vector3(body.velocity.x,0,body.velocity.z).magnitude,.1f,Time.deltaTime);
     if(!body.isGrounded&&vertical>-.8f)Play("Jump");else if(body.isGrounded)Play("Locomotion");
     if(k.eKey.wasPressedThisFrame)TrySit();
     CheckCampusGround();
   }
   void LateUpdate(){if(!controlled||!cam)return;var pivot=transform.position+Vector3.up*(seated?1.15f:1.5f);
     var rotation=Quaternion.Euler(pitch,yaw,0);var desired=pivot+rotation*new Vector3(.35f,0,-cameraDistance);
     var delta=desired-pivot;float distance=delta.magnitude;
     if(Physics.SphereCast(pivot,.15f,delta.normalized,out var hit,distance,~(1<<2),QueryTriggerInteraction.Ignore))desired=pivot+delta.normalized*Mathf.Max(.15f,hit.distance-.1f);
     cam.transform.position=Vector3.Lerp(cam.transform.position,desired,1-Mathf.Exp(-18*Time.deltaTime));cam.transform.rotation=Quaternion.LookRotation(pivot+Vector3.up*.12f-cam.transform.position);
   }
   public string InteractionPrompt=>seated?"Sentado en aula "+selectedRoom.id:prompt;
 }

 // Used to author reusable native clips from the supplied generic Mixamo rig.
 public class PlayerPoseSampler {
   readonly Transform root,rig;readonly Vector3 rigRest;readonly Transform[] bones;readonly Quaternion[] rest;readonly Dictionary<string,Transform> named;
   public PlayerPoseSampler(Transform visual){root=visual;rig=visual.GetChild(0);rigRest=rig.localPosition;bones=visual.GetComponentsInChildren<Transform>();rest=bones.Select(t=>t.localRotation).ToArray();
     named=new Dictionary<string,Transform>();foreach(var t in bones)named[t.name.ToLowerInvariant().Split(':').Last()]=t;}
   void Aim(string a,string b,Vector3 dir){if(!named.TryGetValue(a,out var from)||!named.TryGetValue(b,out var to))return;from.rotation=Quaternion.FromToRotation(to.position-from.position,root.TransformDirection(dir))*from.rotation;}
   public void Sample(string state,float time,float duration){
     for(int i=0;i<bones.Length;i++)bones[i].localRotation=rest[i];rig.localPosition=rigRest;
     float blend=state=="SitDown"?Mathf.SmoothStep(0,1,time/duration):state=="StandUp"?1-Mathf.SmoothStep(0,1,time/duration):state=="Seated"?1:0;
     bool moving=state=="Walk"||state=="Run";bool running=state=="Run";float cycle=time/duration*Mathf.PI*2;float breathe=Mathf.Sin(time/duration*Mathf.PI*2);
     rig.localPosition+=Vector3.down*(.43f*blend)+Vector3.up*(moving?Mathf.Abs(Mathf.Sin(cycle))* (running?.05f:.018f):breathe*.004f);
     if(named.TryGetValue("spine2",out var spine))spine.localRotation*=Quaternion.Euler(moving?(running?9:3):breathe*.6f,0,0);
     foreach(var side in new[]{"left","right"}){float sign=side=="left"?1:-1;float swing=moving?Mathf.Sin(cycle)*sign*(running?.95f:.48f):0;
       Vector3 thigh=Vector3.Lerp(Vector3.down+Vector3.forward*swing,Vector3.forward,blend);
       Vector3 calf=Vector3.down-Vector3.forward*Mathf.Max(0,-swing)*(running?1.3f:.55f);
       if(state=="Jump"){thigh=Vector3.down+Vector3.forward*.55f;calf=Vector3.down-Vector3.forward*.75f;}
       Aim(side+"upleg",side+"leg",thigh);Aim(side+"leg",side+"foot",calf);
       Aim(side+"arm",side+"forearm",Vector3.down+Vector3.forward*Mathf.Lerp(-swing*.65f,.25f,blend));
       Aim(side+"forearm",side+"hand",Vector3.Lerp(Vector3.down+Vector3.forward*(running?.65f:.12f),Vector3.forward,blend));
     }
   }
 }
}





