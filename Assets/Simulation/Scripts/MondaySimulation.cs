using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace CampusMonday {
 public class MondaySimulation:MonoBehaviour {
   public Transform building; public GameObject[] characters; public RoomDefinition[] rooms; public CampusPlayer player; public RuntimeAnimatorController npcAnimations;
   public Vector3 mainEntrance,rearEntrance,office; public Vector3[] officeSeats,officeDoors; public NavMeshData navigation;
   public ModelSettings settings=new ModelSettings(); public float minutesPerSecond=.5f; public double clock=388;
   [NonSerialized] public MondayEngine engine; public bool paused; public int floor=1; public string focusRoom="1102";
   readonly Dictionary<int,PersonView> views=new Dictionary<int,PersonView>(); readonly Stack<PersonView>[] pool={new Stack<PersonView>(),new Stack<PersonView>(),new Stack<PersonView>()};
   Camera cam; Vector3 pivot; float yaw=155,pitch=55,distance=48; int lastFloor=-1; string message=""; MondayDashboard dashboard;
   NavMeshDataInstance nav;
   bool draggingCamera; Vector3 cameraDragAnchor;
   public void Initialize(){ if(navigation!=null&&!nav.valid)nav=NavMesh.AddNavMeshData(navigation);Restart(); }
   void Start(){cam=Camera.main;pivot=rooms[0].board+new Vector3(3,0,-10);Initialize();Focus(focusRoom,false);dashboard=gameObject.AddComponent<MondayDashboard>();dashboard.Initialize(this);}
   void OnDestroy(){if(nav.valid)nav.Remove();}
   readonly Dictionary<(Vector3,Vector3),Vector3[]> paths=new Dictionary<(Vector3,Vector3),Vector3[]>();
   public Vector3[] Route(Vector3 a,Vector3 b){
     if(paths.TryGetValue((a,b),out var cached))return cached;
     if(!NavMesh.SamplePosition(a,out var ah,1.6f,NavMesh.AllAreas)||!NavMesh.SamplePosition(b,out var bh,1.6f,NavMesh.AllAreas))return null;
     var p=new NavMeshPath();if(!NavMesh.CalculatePath(ah.position,bh.position,NavMesh.AllAreas,p)||p.status!=NavMeshPathStatus.PathComplete)return null;
     var corners=p.corners;var list=new List<Vector3>{NpcGroundSurface.Project(corners[0])};
     for(int i=1;i<corners.Length;i++){
       // Height must follow treads and the landing, not a slope spanning the corridor.
       int steps=Mathf.Abs(corners[i].y-corners[i-1].y)>.1f?Mathf.CeilToInt(Vector3.Distance(corners[i-1],corners[i])/.12f):1;
       for(int j=1;j<=steps;j++){
         var point=NpcGroundSurface.Project(Vector3.Lerp(corners[i-1],corners[i],j/(float)steps));
         if(list.Count>1){var previous=list[list.Count-1]-list[list.Count-2];var next=point-list[list.Count-1];if(previous.sqrMagnitude>.000001f&&next.sqrMagnitude>.000001f&&Vector3.Angle(previous,next)<.1f)list.RemoveAt(list.Count-1);}
         list.Add(point);
       }
     }
     if(list.Count==1)list.Add(NpcGroundSurface.Project(bh.position));var result=list.ToArray();paths[(a,b)]=result;return result;
   }
   public void Restart(){foreach(var p in views.Values)Destroy(p.gameObject);views.Clear();foreach(var p in pool){foreach(var v in p)Destroy(v.gameObject);p.Clear();}
     settings.mainShare=settings.alternative?.6f:.9f; engine=new MondayEngine(JsonUtility.FromJson<MondayData>(Resources.Load<TextAsset>("monday").text),rooms,mainEntrance,rearEntrance,office,settings,Route,officeSeats,officeDoors);clock=388;message="Escenario cargado · semilla "+settings.seed;
   }
   void Update(){if(engine==null)return;var keyboard=Keyboard.current;
     if(player&&keyboard!=null&&keyboard.tabKey.wasPressedThisFrame)player.SetControlled(!player.controlled);
     if(!paused)clock=Math.Min(1380,clock+Time.deltaTime*minutesPerSecond);
     foreach(var p in engine.people){var state=p.State(clock);bool visible=state!=Phase.Fuera&&state!=Phase.Finalizado;
       if(visible&&!views.ContainsKey(p.id)){int r=(int)p.role;var v=pool[r].Count>0?pool[r].Pop():CreateView(r);views[p.id]=v;v.gameObject.SetActive(true);}
       if(!visible&&views.TryGetValue(p.id,out var old)){views.Remove(p.id);old.gameObject.SetActive(false);pool[(int)p.role].Push(old);}
       if(visible){var v=views[p.id];Vector3 pos=p.Position(clock,out var facing);bool seated=state==Phase.Sentado||state==Phase.Trabajando;
         if(state==Phase.Sentado||state==Phase.EnTablero){var room=rooms.First(r=>r.id==p.room);facing=room.board-pos;}if(state==Phase.Trabajando)facing=p.seat%2==0?Vector3.left:Vector3.right;
         if(state==Phase.Esperando&&clock<p.admitted){int rank=engine.people.Count(o=>o.room==p.room&&o.doorArrival<p.doorArrival&&o.admitted>clock);pos+=Vector3.back*Math.Min(rank,40)*.42f;}
         pos=NpcGroundSurface.Project(pos);
         v.transform.position=pos;facing.y=0;if(facing.sqrMagnitude>.001f)v.transform.rotation=Quaternion.LookRotation(facing);
         v.Pose(seated,state==Phase.Llegando||state==Phase.Entrando||state==Phase.Saliendo,(float)(clock*60*p.speed+p.id));
         foreach(var rr in v.renderers)rr.enabled=floor==0||Mathf.Abs(pos.y-FloorHeight(floor))<2;
       }
     }
     if(lastFloor!=floor){foreach(var r in building.GetComponentsInChildren<Renderer>(true))r.enabled=floor==0||r.bounds.min.y<FloorHeight(floor)+2.5f;lastFloor=floor;}
     var mouse=Mouse.current;var kb=Keyboard.current;
     if(kb!=null&&(((!player||!player.controlled)&&(!dashboard||!dashboard.HasKeyboardFocus)&&kb.spaceKey.wasPressedThisFrame)||kb.pKey.wasPressedThisFrame))paused=!paused;
     if(player&&player.controlled){draggingCamera=false;return;}
     if(mouse!=null){
       bool overInterface=PointerOverInterface;
       if(mouse.leftButton.wasPressedThisFrame)draggingCamera=!overInterface&&CameraPlanePoint(mouse.position.ReadValue(),out cameraDragAnchor);
       if(!mouse.leftButton.isPressed||overInterface)draggingCamera=false;
       if(draggingCamera&&CameraPlanePoint(mouse.position.ReadValue(),out var point)){
         // Drag the world on a horizontal plane; preserve camera height and orientation.
         var offset=cameraDragAnchor-point;offset.y=0;pivot+=offset;
       }
       else if(!overInterface&&mouse.rightButton.isPressed){var d=mouse.delta.ReadValue();yaw+=d.x*.25f;pitch=Mathf.Clamp(pitch-d.y*.2f,10,88);}
       if(!overInterface&&!draggingCamera)distance=Mathf.Clamp(distance-mouse.scroll.ReadValue().y*.025f,5,110);
     }
     if(cam){cam.transform.position=pivot+Quaternion.Euler(pitch,yaw,0)*new Vector3(0,0,-distance);cam.transform.LookAt(pivot);}
   }
   bool CameraPlanePoint(Vector2 screenPoint,out Vector3 point){
     point=default;if(!cam)return false;var ray=cam.ScreenPointToRay(screenPoint);
     if(!new Plane(Vector3.up,pivot).Raycast(ray,out float enter))return false;
     point=ray.GetPoint(enter);return true;
   }
   void OnApplicationFocus(bool focused){if(!focused)draggingCamera=false;}
   float FloorHeight(int f)=>rooms.First(r=>r.floor==f).seats[0].y;
   public PersonView CreateView(int role){var root=new GameObject(((Role)role).ToString());var model=Instantiate(characters[role],root.transform);model.name="Personaje original";
     var actorAnimator=model.GetComponent<Animator>();if(!actorAnimator)actorAnimator=model.AddComponent<Animator>();actorAnimator.enabled=true;actorAnimator.runtimeAnimatorController=npcAnimations;actorAnimator.applyRootMotion=false;actorAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
     var rs=model.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);float scale=1.7f/b.size.y;model.transform.localScale*=scale;
     b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);model.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
     var v=root.AddComponent<PersonView>();v.Setup(model.transform);return v;
   }
   public void Focus(string id,bool close){focusRoom=id;var r=rooms.First(x=>x.id==id);floor=r.floor;pivot=(r.seats[0]+r.seats[r.seats.Length-1])*.5f+Vector3.up*.5f;distance=close?12:42;}
   public void Seek(double t){clock=Math.Max(388,Math.Min(1380,t));}
   public void Export(){var data=JsonUtility.FromJson<MondayData>(Resources.Load<TextAsset>("monday").text);string folder=Path.Combine(Application.dataPath,"../Results/Interactive");engine.Export(folder,(settings.alternative?"alternativo":"base")+"_"+settings.seed,data);message="CSV exportados a Results/Interactive";}
   public bool PointerOverInterface=>dashboard&&dashboard.PointerOverControls;
   public string StatusMessage=>message;
   public void ViewOffices(){floor=2;pivot=(officeSeats[0]+officeSeats[officeSeats.Length-1])*.5f;distance=25;}
   public void Capture(){ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"../Documentation/preview.png"));message="Captura guardada en Documentation/preview.png";}
 }
 public class PersonView:MonoBehaviour {
   public Renderer[] renderers;Animator animator;string state="";Dictionary<string,float> durations;
   Transform model;Vector3 basePosition;FootProbe[] footProbes;
   class SkinPoint {public string[] names;public Vector3[] points;public Vector4 weights;}
   class FootProbe {public Transform[] bones;public SkinPoint skin;public Vector3 Position(){var p=Vector3.zero;for(int i=0;i<4;i++)if(skin.weights[i]>0)p+=bones[i].TransformPoint(skin.points[i])*skin.weights[i];return p;}}
   class SoleProfile {public float baseline;public SkinPoint[] points;}
   static readonly Dictionary<Avatar,SoleProfile> soleProfiles=new Dictionary<Avatar,SoleProfile>();
   public float GroundLift {get;private set;}
   public string MotionState=>state;
   public Vector3 VisualOffset=>model.localPosition-basePosition;
   public float ProbeClearance(){float clearance=10;foreach(var probe in footProbes){var sole=probe.Position();var p=sole;p.y=transform.position.y;if(NpcGroundSurface.TryHeight(p,out float ground))clearance=Mathf.Min(clearance,sole.y-ground);}return clearance;}
   public void Setup(Transform model){this.model=model;basePosition=model.localPosition;renderers=model.GetComponentsInChildren<Renderer>();animator=model.GetComponent<Animator>();if(!animator||!animator.avatar||!animator.avatar.isHuman)throw new InvalidOperationException("El NPC requiere un Avatar humanoide válido");animator.speed=0;durations=animator.runtimeAnimatorController.animationClips.GroupBy(c=>c.name).ToDictionary(g=>g.Key,g=>g.First().length);
     animator.Rebind();animator.Update(0);
     if(!soleProfiles.TryGetValue(animator.avatar,out var profile)){
       var feet=new[]{animator.GetBoneTransform(HumanBodyBones.LeftFoot),animator.GetBoneTransform(HumanBodyBones.RightFoot)};var toes=new[]{animator.GetBoneTransform(HumanBodyBones.LeftToes),animator.GetBoneTransform(HumanBodyBones.RightToes)};
       float meshBottom=float.PositiveInfinity;var baked=new Mesh();var candidates=new List<(int side,SkinPoint skin,Vector3 world)>();
       foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>()){renderer.BakeMesh(baked);var visible=baked.vertices;var original=renderer.sharedMesh.vertices;var weights=renderer.sharedMesh.boneWeights;var bind=renderer.sharedMesh.bindposes;var skinBones=renderer.bones;
       for(int vertexIndex=0;vertexIndex<visible.Length;vertexIndex++){var world=renderer.transform.TransformPoint(visible[vertexIndex]);meshBottom=Mathf.Min(meshBottom,world.y);
         if(world.y>Mathf.Max(feet[0].position.y,feet[1].position.y)+.15f)continue;
         int side=Vector3.SqrMagnitude(world-feet[0].position)<Vector3.SqrMagnitude(world-feet[1].position)?0:1;var weight=weights[vertexIndex];var indexes=new[]{weight.boneIndex0,weight.boneIndex1,weight.boneIndex2,weight.boneIndex3};var data=new SkinPoint{weights=new Vector4(weight.weight0,weight.weight1,weight.weight2,weight.weight3),names=new string[4],points=new Vector3[4]};for(int i=0;i<4;i++)if(data.weights[i]>0){data.names[i]=skinBones[indexes[i]].name;data.points[i]=bind[indexes[i]].MultiplyPoint3x4(original[vertexIndex]);}candidates.Add((side,data,world));
       }}
       if(Application.isPlaying)UnityEngine.Object.Destroy(baked);else UnityEngine.Object.DestroyImmediate(baked);
       if(float.IsPositiveInfinity(meshBottom))meshBottom=transform.position.y;
       var points=new List<SkinPoint>();for(int side=0;side<2;side++){var shoe=candidates.Where(p=>p.side==side).ToArray();if(shoe.Length==0)continue;
         points.Add(shoe.OrderBy(p=>p.world.y).First().skin);
         for(int i=0;i<8;i++){float angle=i*Mathf.PI/4;var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var p=shoe.OrderByDescending(p=>Vector3.Dot(p.world,direction)).First();points.Add(p.skin);}
         // Include lower shoe edges: horizontal extrema alone can select the upper toe.
         foreach(float slope in new[]{.5f,1f,2f})for(int i=0;i<8;i++){float angle=i*Mathf.PI/4;var direction=new Vector3(Mathf.Cos(angle),-slope,Mathf.Sin(angle));var p=shoe.OrderByDescending(p=>Vector3.Dot(p.world,direction)).First();if(!points.Contains(p.skin))points.Add(p.skin);}
       }
       profile=new SoleProfile{baseline=meshBottom-transform.position.y,points=points.ToArray()};soleProfiles[animator.avatar]=profile;
     }
     var bones=model.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);footProbes=profile.points.Select(p=>new FootProbe{skin=p,bones=p.names.Select(n=>n==null?null:bones[n]).ToArray()}).ToArray();model.localPosition=basePosition-Vector3.up*profile.baseline;basePosition=model.localPosition;
   }
   // Small skin keeps the curved toe between sampled sole points clear of tread edges.
   const float SoleClearance=.035f;
   float FootPenetration(){float penetration=0;foreach(var probe in footProbes){var sole=probe.Position();var point=sole;point.y=transform.position.y;if(NpcGroundSurface.TryHeight(point,out float ground,.045f))penetration=Mathf.Max(penetration,ground+SoleClearance-sole.y);}return penetration;}
   public void Pose(bool seated,bool walk,float phase){
     model.localPosition=basePosition;
     string next=seated?"Seated":walk?"Walk":"Idle";float duration=durations[next];
     float normalized=Mathf.Repeat(phase/duration,1);
     if(next!=state){animator.Play(next,0,normalized);state=next;}
     animator.SetFloat("MotionTime",normalized);animator.Update(0);
     GroundLift=seated?0:Mathf.Max(0,FootPenetration());
     model.localPosition=basePosition+Vector3.up*GroundLift;
   }
 }
}
// Refresh compilation

