using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CampusMonday {
public partial class FullCampus : MonoBehaviour {
    [Serializable] public class Seat { public string building,name,role,room; public Vector3 position,forward; }
    [Serializable] public class Inventory { public Seat[] seats; public Vector3 entrance; public int chunks,objects,triangles; }
    const float CampusAgentRadius=.14f;
    const float CampusAgentHeight=1.58f;
    const float CampusAgentClimb=.34f;
    const float PlayerRadius=.18f;
    const float PlayerHeight=1.68f;
    const float PlayerStep=.32f;
    const float PlayerFootOffset=.015f;
    class Trip {
        public Seat seat;
        public Vector3[] actualRoute, tour;
        public Vector3 origin, exit, destination;
        public bool toured, relocated, relocationEligible;
        public Vector3 beforeRelocation;
        public Vector3[] route; public string routeStatus; public Vector3 partialEnd;
        public int phase,corner;
        public Vector3 visualOffset; public float entered,sat,left,ended,meters,stalled; public int retries;
        public NavMeshAgent agent;
        public GameObject visual;
        public Animator animator;
    }
    public Shader campusShader;
    public GameObject[] characters;
    public RuntimeAnimatorController animations,playerAnimations;
    CampusPlayer player;
    readonly Dictionary<RoomDefinition,Trip> playerPlaces=new Dictionary<RoomDefinition,Trip>();
    readonly List<MeshFilter> solidTiles=new List<MeshFilter>();
    readonly HashSet<MeshFilter> playerCollisionTiles = new HashSet<MeshFilter>();
    public Vector3 SeatApproach(RoomDefinition room){var path=playerPlaces[room].route;return path[path.Length-1];}
    public bool Occupied(RoomDefinition room)=>playerPlaces.TryGetValue(room,out var trip)&&trip.phase==2;
    void CreatePlayer(){
        var person=new GameObject("Jugador");person.layer=2;person.transform.position=entrance+Vector3.up*.03f;
        var rig=Instantiate(characters[0],person.transform);rig.transform.localPosition=Vector3.zero;
        var rs=rig.GetComponentsInChildren<Renderer>();var box=rs[0].bounds;foreach(var r in rs)box.Encapsulate(r.bounds);
        rig.transform.localScale*=PlayerHeight/box.size.y;box=rs[0].bounds;foreach(var r in rs)box.Encapsulate(r.bounds);
        rig.transform.position-=new Vector3(box.center.x-person.transform.position.x,box.min.y-person.transform.position.y,box.center.z-person.transform.position.z);
        var body=person.AddComponent<CharacterController>();
        body.radius=PlayerRadius;body.height=PlayerHeight;
        body.center=Vector3.up*(PlayerHeight*.5f+PlayerFootOffset);
        body.skinWidth=.025f;body.stepOffset=PlayerStep;
        player=person.AddComponent<CampusPlayer>();player.campus=this;player.visual=rig.transform;player.animator=rig.GetComponent<Animator>();
        player.animator.runtimeAnimatorController=playerAnimations;player.animator.applyRootMotion=false;
        foreach(var t in trips.Where(x=>x.route!=null)){
            var seat=t.seat;var room=new RoomDefinition{id=seat.room,seats=new[]{seat.position},board=seat.position+seat.forward};
            playerPlaces.Add(room,t);
        }
        player.campusRooms=playerPlaces.Keys.ToArray();
    }
    internal void PreparePlayerCollisions(Vector3 position)
    {
        foreach (var tile in solidTiles)
        {
            // Both the visible high-detail mesh and its collider use campus world coordinates.
            if (playerCollisionTiles.Contains(tile) || tile.sharedMesh.bounds.SqrDistance(position) > 144f)
                continue;
            var collider = tile.gameObject.AddComponent<MeshCollider>();
            if (tile.sharedMesh.GetIndexCount(0) > 6_291_456)
                collider.cookingOptions &= ~MeshColliderCookingOptions.UseFastMidphase;
            collider.sharedMesh = tile.sharedMesh;
            playerCollisionTiles.Add(tile);
        }
    }

    internal bool FindPlayerSpawn(out Vector3 position)
    {
        PreparePlayerCollisions(entrance);
        Physics.SyncTransforms();
        const int mask = ~(1 << 2);
        for (float radius = 0; radius <= 6; radius += .5f)
        {
            int samples = radius == 0 ? 1 : 12;
            for (int i = 0; i < samples; i++)
            {
                var point = entrance + Quaternion.Euler(0, i * (360f / samples), 0) * Vector3.forward * radius;
                if (!NavMesh.SamplePosition(point, out var navPoint, .75f, NavMesh.AllAreas) ||
                    Mathf.Abs(navPoint.position.y - entrance.y) > 1.25f) continue;
                if (!Physics.Raycast(navPoint.position + Vector3.up * .55f, Vector3.down, out var floor, 1.15f,
                    mask, QueryTriggerInteraction.Ignore) || floor.normal.y < .7f) continue;
                var feet = floor.point + Vector3.up * .035f;
                if (Physics.CheckCapsule(feet + Vector3.up * (PlayerFootOffset+PlayerRadius),
                    feet + Vector3.up * (PlayerFootOffset+PlayerHeight-PlayerRadius), PlayerRadius,
                    mask, QueryTriggerInteraction.Ignore)) continue;
                position = feet;
                return true;
            }
        }

        position = entrance;
        return false;
    }

    public NavMeshData baked;
    public Inventory inventory;
    float frameSeconds;int frames; public readonly List<NavMeshBuildSource> sources=new List<NavMeshBuildSource>();
    readonly List<Trip> trips=new List<Trip>();
    NavMeshDataInstance nav;
    [Serializable] class ThresholdLinks { public long geometryBytes; public Threshold[] links; }
    [Serializable] class Threshold { public Vector3 start,end; }
    readonly List<NavMeshLinkInstance> thresholdLinks = new List<NavMeshLinkInstance>();
    public void LoadThresholdLinks()
    {
        if(thresholdLinks.Count>0)return;
        string file=Path.Combine(Folder,"threshold_links.json");
        if(!File.Exists(file))return;
        var data=JsonUtility.FromJson<ThresholdLinks>(File.ReadAllText(file));
        if(data?.links==null||data.geometryBytes!=new FileInfo(Path.Combine(Folder,"campus.gz")).Length)return;
        foreach(var link in data.links)
        {
            var added=NavMesh.AddLink(new NavMeshLinkData{
                startPosition=link.start,endPosition=link.end,bidirectional=true,
                agentTypeID=0,area=0,costModifier=-1,width=0},Vector3.zero,Quaternion.identity);
            if(NavMesh.IsLinkValid(added))thresholdLinks.Add(added);
        }
    }
    Vector3 entrance;
    public Bounds bounds;
    string summary,message;
    int next,finished,eligibleStalls;
    bool running;
    Trip[] spawnOrder;
    readonly Dictionary<string, Vector3> buildingOrigins = new Dictionary<string, Vector3>();
    readonly Dictionary<Vector3, Vector3> exteriorExits = new Dictionary<Vector3, Vector3>();
    readonly Dictionary<Vector3, Vector3[]> exteriorTours = new Dictionary<Vector3, Vector3[]>();
    bool paused;
    float rate=1;
    Material mapMaterial;
    MondayDashboard dashboard;
    public bool PlayerControlled=>player&&player.controlled;
    public bool PointerOverControls=>dashboard&&dashboard.PointerOverControls;
    public bool Running=>running;
    public bool Paused=>paused;
    public bool Started=>elapsed>0||running;
    public string Status=>message;
    public string Clock=>TimeSpan.FromSeconds(6*3600+28*60+elapsed).ToString(@"hh\:mm\:ss");
    public string PlayerPrompt=>player?player.InteractionPrompt:"";
    public int Floor { get; private set; }
    public int MaxFloor=>FloorCuts.Max(block=>block.Cuts.Length);
    public int Capacity(string role)=>capacity.TryGetValue(role,out int count)?count:0;
    Dictionary<string,int> capacity;
    public void ToggleRun(){
        if(checkingLocalAccess)return;
        if(running){paused=!paused;return;}
        if(Started)Restart();
        else ClearLoadPeople();
        spawnOrder=trips.GroupBy(t=>t.seat.building)
            .SelectMany(group=>group.Select((trip,index)=>(trip,index)))
            .OrderBy(item=>item.index).Select(item=>item.trip).ToArray();
        running=true;
    }
    public void SetRate(float value){rate=value;}
    public void SetFloor(int floor){Floor=floor;ApplyFloorCuts();}
    public void SetPlayer(bool enabled)
    {
        if (!player || player.controlled == enabled) return;
        if (enabled && !player.PrepareCampusControl())
        {
            message = "No se encontró piso libre para el jugador en la portería.";
            return;
        }
        player.SetControlled(enabled);
        SetFloor(Floor);
    }
    public void Restart(){
        if(checkingLocalAccess)return;
        ClearLoadPeople();
        for(int i=0;i<trips.Count;i++){
            var trip=trips[i];
            if(trip.agent){trip.agent.enabled=false;Destroy(trip.agent.gameObject);}
            if(trip.visual)Destroy(trip.visual);
            var reset=new Trip{seat=trip.seat,route=trip.route,routeStatus=trip.routeStatus,partialEnd=trip.partialEnd};trips[i]=reset;
        }
        foreach(var room in playerPlaces.Keys.ToArray()){
            var trip=playerPlaces[room];
            playerPlaces[room]=trips.First(t=>ReferenceEquals(t.seat,trip.seat));
        }
        running=false;paused=false;next=0;finished=0;eligibleStalls=0;frames=0;frameSeconds=0;elapsed=0;
        message=routeStatus;
    }
    string routeStatus;
    public void OpenResults(){
        string dir=Path.Combine(Application.persistentDataPath,"Campus");
        Directory.CreateDirectory(dir);Application.OpenURL(new Uri(dir).AbsoluteUri);
    }
    float elapsed,orbit=35,zoom=85;
    Vector3 focus;
    static string Folder=>Path.Combine(Application.streamingAssetsPath,"Campus");
    void Start(){LoadMap();if(!baked)baked=Bake();if(baked)nav=NavMesh.AddNavMeshData(baked);LoadThresholdLinks();Audit();CreatePlayer();focus=entrance;Camera.main.farClipPlane=2200;MoveCamera();dashboard=gameObject.AddComponent<MondayDashboard>();dashboard.Initialize(this);}
    public void LoadMap(bool preview=false, bool navigationOnly=false){
        inventory=JsonUtility.FromJson<Inventory>(File.ReadAllText(Path.Combine(Folder,"capacity.json")));
        mapMaterial=new Material(campusShader);var material=mapMaterial;
        using(var reader=new BinaryReader(new GZipStream(File.OpenRead(Path.Combine(Folder,"campus.gz")),CompressionMode.Decompress))){
            int count=reader.ReadInt32();
            bool hasBounds=false;
            for(int i=0;i<count;i++){
                var group=new GameObject("Sector "+i);group.transform.SetParent(transform);
                var levels=new LOD[2];
                for(int level=0;level<2;level++){
                int vertices=reader.ReadInt32(),indices=reader.ReadInt32();bool solid=reader.ReadBoolean();
                if((preview&&level==0&&!solid)||(navigationOnly&&!solid)){
                    SkipGeometry(reader,vertices*16L+indices*4L);
                    continue;
                }
                var points=MemoryMarshal.Cast<byte,Vector3>(reader.ReadBytes(vertices*12)).ToArray();
                var colors=MemoryMarshal.Cast<byte,Color32>(reader.ReadBytes(vertices*4)).ToArray();
                var triangles=MemoryMarshal.Cast<byte,int>(reader.ReadBytes(indices*4)).ToArray();
                var mesh=new Mesh{indexFormat=vertices>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.vertices=points;mesh.colors32=colors;mesh.triangles=triangles;mesh.RecalculateNormals();
                var tile=new GameObject("Sector "+i);tile.transform.SetParent(group.transform);
                tile.AddComponent<MeshFilter>().sharedMesh=mesh;tile.AddComponent<MeshRenderer>().sharedMaterial=material;
                if(!hasBounds){bounds=mesh.bounds;hasBounds=true;}else bounds.Encapsulate(mesh.bounds);
                levels[level]=new LOD(level==0?.06f:.00001f,new[]{tile.GetComponent<Renderer>()});if(solid&&!preview)solidTiles.Add(tile.GetComponent<MeshFilter>());if(solid&&!preview)sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Mesh,sourceObject=mesh,transform=Matrix4x4.identity});
                }
                if(navigationOnly)continue;
                var lod=group.AddComponent<LODGroup>();lod.SetLODs(levels[0].renderers==null?new[]{new LOD(.00001f,levels[1].renderers)}:levels);lod.RecalculateBounds();
            }
        }
    }
    static void SkipGeometry(BinaryReader reader,long bytes){
        var buffer=new byte[65536];
        while(bytes>0){
            int read=reader.Read(buffer,0,(int)Math.Min(bytes,buffer.Length));
            if(read==0)throw new EndOfStreamException();
            bytes-=read;
        }
    }
    public NavMeshData Bake(){
        var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=CampusAgentRadius;settings.agentHeight=CampusAgentHeight;settings.agentClimb=CampusAgentClimb;settings.agentSlope=45;settings.overrideVoxelSize=true;settings.voxelSize=.075f;settings.overrideTileSize=true;settings.tileSize=192;
        return UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
    }
    public string Audit(){
        entrance=inventory.entrance;
        bool gate=NavMesh.SamplePosition(entrance,out var start,8,NavMesh.AllAreas);if(gate)entrance=start.position;
        trips.Clear();
        var path=new NavMeshPath();
        var reverse=new NavMeshPath();
        foreach(var seat in inventory.seats){
            var trip=new Trip{seat=seat,routeStatus=gate?"sin aproximación en el piso":"portería fuera de NavMesh"};
            trips.Add(trip);
            if(!gate)continue;
            var facing=Vector3.ProjectOnPlane(seat.forward,Vector3.up).normalized;
            if(facing.sqrMagnitude<.01f)facing=Vector3.forward;
            var right=Vector3.Cross(Vector3.up,facing);
            float closest=float.PositiveInfinity;
            var approaches=new HashSet<Vector3>();
            foreach(float reach in new[]{.48f,.72f})
            foreach(var side in new[]{right,-right,facing,-facing,(right+facing).normalized,(right-facing).normalized,(-right+facing).normalized,(-right-facing).normalized}){
                if(!NavMesh.SamplePosition(seat.position+side*reach,out var hit,.57f,NavMesh.AllAreas)||
                    Mathf.Abs(hit.position.y-seat.position.y)>.55f||
                    Vector3.ProjectOnPlane(hit.position-seat.position,Vector3.up).magnitude>1.05f||!approaches.Add(hit.position))continue;
                trip.routeStatus="con aproximación, sin conexión con portería";
                if(!NavMesh.CalculatePath(entrance,hit.position,NavMesh.AllAreas,path))continue;
                if(path.status!=NavMeshPathStatus.PathComplete){
                    var corners=path.corners;
                    if(corners.Length==0)continue;
                    var end=corners[corners.Length-1];
                    var remaining=(end-hit.position).sqrMagnitude;
                    if(remaining<closest){closest=remaining;trip.partialEnd=end;}
                    continue;
                }
                if(!NavMesh.CalculatePath(hit.position,entrance,NavMesh.AllAreas,reverse)||reverse.status!=NavMeshPathStatus.PathComplete)continue;
                trip.route=path.corners;trip.routeStatus="ruta completa";goto NextSeat;
            }
            NextSeat: continue;
        }
        capacity=inventory.seats.GroupBy(s=>s.role).ToDictionary(g=>g.Key,g=>g.Count());summary=string.Join("   ",inventory.seats.GroupBy(s=>s.role).Select(g=>g.Key+": "+g.Count()));
        routeStatus=message=trips.Count(t=>t.route!=null)+" de "+trips.Count+" puestos con ruta completa desde portería.";
        if(Application.isEditor){
            Directory.CreateDirectory("Documentation");
            using var writer=new StreamWriter("Documentation/campus_navigation_audit.csv",false,new UTF8Encoding(true));
            writer.WriteLine("edificio,zona,puesto,diagnostico,x,y,z,corte_x,corte_y,corte_z");
            foreach(var t in trips){
                var p=t.seat.position;var end=t.partialEnd;
                writer.WriteLine(FormattableString.Invariant($"{Q(t.seat.building)},{Q(t.seat.room)},{Q(t.seat.name)},{Q(t.routeStatus)},{p.x:F3},{p.y:F3},{p.z:F3},{end.x:F3},{end.y:F3},{end.z:F3}"));
            }
        }
        Debug.Log("Campus: "+summary+" | "+message+" Acceso "+entrance);return summary+"\n"+message;
    }
    static bool CompletePath(Vector3 from, Vector3 to, NavMeshPath path) =>
        NavMesh.CalculatePath(from,to,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;

    void Spawn(Trip t)
    {
        if(!buildingOrigins.TryGetValue(t.seat.building,out var origin))
        {
            var seats=inventory.seats.Where(seat=>seat.building==t.seat.building);
            if(!PickLoadOrigin(BuildingOrigins(t.seat.building),seats,out origin))origin=entrance;
            buildingOrigins[t.seat.building]=origin;
        }
        if(!NavMesh.SamplePosition(origin,out var start,.45f,NavMesh.AllAreas))
        {
            FinishTrip(t,false);
            t.routeStatus="sin aparición navegable";
            return;
        }

        var local=PlanLocalTrip(t.seat,start.position);
        if(local==null && t.route!=null)
        {
            t.actualRoute=t.route;
            start.position=entrance;
        }
        else t.actualRoute=local?.route;

        t.origin=start.position;
        if(!exteriorExits.TryGetValue(t.origin,out t.exit))
        {
            t.exit=t.origin==entrance||CompletePath(t.origin,entrance,new NavMeshPath())?entrance:t.origin;
            exteriorExits[t.origin]=t.exit;
        }
        if(t.actualRoute!=null)
        {
            Spawn(t,t.origin,t.actualRoute[t.actualRoute.Length-1]);
            return;
        }
        Spawn(t,t.origin,t.origin);
        BeginTour(t);
    }

    void BeginTour(Trip t)
    {
        var from=t.agent.transform.position;
        t.toured=true;
        t.retries=0;
        t.stalled=0;
        if(!exteriorTours.TryGetValue(t.origin,out var tour))
        {
            var points=new List<Vector3>();
            var path=new NavMeshPath();
            var candidates=AccessPoints.SelectMany(entry=>entry.Points).Concat(LibraryOrigins)
                .OrderBy(point=>(point-t.origin).sqrMagnitude);
            var previous=t.origin;
            foreach(var candidate in candidates)
            {
                if(points.Count==3)break;
                if(!NavMesh.SamplePosition(candidate,out var nextPoint,.45f,NavMesh.AllAreas) ||
                   (nextPoint.position-previous).sqrMagnitude<225 ||
                   !CompletePath(previous,nextPoint.position,path) ||
                   !CompletePath(nextPoint.position,t.exit,path))continue;
                previous=nextPoint.position;
                points.Add(previous);
            }
            // A disconnected exterior still allows a short circuit on its own walkable ground.
            for(int i=0;points.Count<3&&i<12;i++)
            {
                var target=t.origin+Quaternion.Euler(0,i*30,0)*Vector3.forward*(i<6?8:4);
                if(!NavMesh.SamplePosition(target,out var nearby,1f,NavMesh.AllAreas) ||
                   (nearby.position-previous).sqrMagnitude<9 ||
                   !CompletePath(previous,nearby.position,path) ||
                   !CompletePath(nearby.position,t.exit,path))continue;
                previous=nearby.position;
                points.Add(previous);
            }
            exteriorTours[t.origin]=tour=points.ToArray();
        }
        t.tour=tour;
        t.corner=0;
        t.phase=6;
        if(t.tour.Length==0 || !CompletePath(from,t.tour[0],new NavMeshPath()))BeginExit(t);
        else SetTripDestination(t,t.tour[0]);
    }

    void BeginExit(Trip t)
    {
        var path=new NavMeshPath();
        var from=t.agent.transform.position;
        if(!CompletePath(from,t.exit,path) && !CompletePath(from,t.origin,path))
        {
            if(!t.relocated || !t.agent.Warp(t.beforeRelocation) ||
               (!CompletePath(t.beforeRelocation,t.exit,path)&&!CompletePath(t.beforeRelocation,t.origin,path)))
            {
                FinishTrip(t,false);
                return;
            }
            from=t.beforeRelocation;
        }
        t.phase=3;
        t.retries=0;
        t.stalled=0;
        var corners=path.corners;
        SetTripDestination(t,corners.Length>0?corners[corners.Length-1]:from);
    }

    static void SetTripDestination(Trip t,Vector3 target)
    {
        t.destination=target;
        t.agent.SetDestination(target);
    }

    void FinishTrip(Trip t,bool completed)
    {
        t.phase=completed?4:5;
        t.ended=elapsed;
        finished++;
        if(t.agent)Destroy(t.agent.gameObject);
        if(t.visual)Destroy(t.visual);
    }

    void Spawn(Trip t,Vector3 origin) => Spawn(t,origin,t.route[t.route.Length-1]);
    void Spawn(Trip t,Vector3 origin,Vector3 destination){
        var obj=new GameObject(t.seat.role);obj.transform.position=origin;
        t.agent=obj.AddComponent<NavMeshAgent>();t.agent.radius=CampusAgentRadius;t.agent.height=CampusAgentHeight;t.agent.speed=1.45f;t.agent.avoidancePriority=20+next%60;
        SetTripDestination(t,destination);t.phase=1;t.entered=elapsed;
    }
    void Visual(Trip t,Vector3 p,Vector3? testVelocity=null){
        bool near=(p.y<=VisibleHeight(p))&&(Camera.main.transform.position-p).sqrMagnitude<12100;
        if(!near){if(t.visual) t.visual.SetActive(false);return;}
        if(!t.visual){
            int k=t.seat.role=="Profesor"?1:t.seat.role=="Administrativo"?2:0;
            t.visual=Instantiate(characters[k],p,Quaternion.identity);
            var renderers=t.visual.GetComponentsInChildren<Renderer>();var box=renderers[0].bounds;foreach(var r in renderers)box.Encapsulate(r.bounds);
            t.visual.transform.localScale*=1.7f/box.size.y;
            box=renderers[0].bounds;foreach(var r in renderers)box.Encapsulate(r.bounds);
            t.visualOffset=p-new Vector3(box.center.x,box.min.y,box.center.z);
            t.animator=t.visual.GetComponent<Animator>();t.animator.runtimeAnimatorController=animations;t.animator.applyRootMotion=false;
        }
        t.visual.SetActive(true);t.visual.transform.position=p+t.visual.transform.rotation*t.visualOffset;
        var velocity=testVelocity??t.agent.velocity;
        var direction=t.phase==2?t.seat.forward:velocity;
        direction.y=0;if(direction.sqrMagnitude>.001f)t.visual.transform.rotation=Quaternion.LookRotation(direction);
        string state=t.phase==2?(t.seat.role=="Profesor"&&elapsed-t.sat<60?"Idle":"Seated"):velocity.sqrMagnitude>.05f?"Walk":"Idle";
        if(!t.animator.GetCurrentAnimatorStateInfo(0).IsName(state))t.animator.Play(state);
        t.animator.SetFloat("MotionTime",Mathf.Repeat(testVelocity.HasValue?Time.time:elapsed,1));
    }
    void Update(){
        var keys=Keyboard.current;var mouse=Mouse.current;
        if(keys!=null&&keys.pKey.wasPressedThisFrame&&running)paused=!paused;
        if(keys!=null&&keys.tabKey.wasPressedThisFrame)SetPlayer(!PlayerControlled);
        if(!player.controlled&&mouse!=null){
            if(mouse.leftButton.isPressed&&!PointerOverControls){var d=mouse.delta.ReadValue();var yaw=Quaternion.Euler(0,orbit,0);focus+=yaw*new Vector3(-d.x,0,-d.y)*zoom*.0014f;}
            zoom=Mathf.Clamp(zoom-mouse.scroll.ReadValue().y*.08f,5,1000);MoveCamera();
        }
        if(!running)return;
                if(paused){
            foreach(var trip in trips)if(trip.agent&&trip.agent.enabled)trip.agent.isStopped=true;
            return;
        }
        frames++;frameSeconds+=Time.unscaledDeltaTime;elapsed+=Time.deltaTime*rate;
        while(next<spawnOrder.Length&&next<=elapsed*2){Spawn(spawnOrder[next]);next++;}
        foreach(var t in trips){
            if(t.phase<1||(t.phase>3&&t.phase!=6))continue;
            if(t.agent.enabled){t.agent.isStopped=false;t.agent.speed=1.45f*rate;t.agent.acceleration=8*rate;}
            if(t.phase==2){
                bool teaching=t.seat.role=="Profesor"&&elapsed-t.sat<60;Visual(t,teaching?t.agent.transform.position:t.seat.position);
                if(elapsed-t.sat<120)continue;
                t.left=elapsed;t.agent.enabled=true;BeginExit(t);
            }else{
                if(t.agent.velocity.sqrMagnitude<.001f&&!t.agent.pathPending&&t.agent.remainingDistance>.12f)t.stalled+=Time.deltaTime;else t.stalled=0;
                if(t.stalled>20||(!t.agent.pathPending&&t.agent.pathStatus!=NavMeshPathStatus.PathComplete)){
                    t.stalled=0;
                    if(++t.retries<=3)SetTripDestination(t,t.destination);
                    else if(t.phase==1){if(!TryRelocateNearSeat(t))BeginTour(t);}
                    else if(t.phase==6)BeginExit(t);
                    else FinishTrip(t,false);
                    continue;
                }
                Visual(t,t.agent.transform.position);t.meters+=t.agent.velocity.magnitude*Time.deltaTime;
                if(t.agent.pathPending||t.agent.remainingDistance>.12f||float.IsInfinity(t.agent.remainingDistance))continue;
                if(t.phase==1){t.phase=2;t.sat=elapsed;t.agent.enabled=false;}
                else if(t.phase==6){
                    t.retries=0;
                    if(++t.corner<t.tour.Length)SetTripDestination(t,t.tour[t.corner]);
                    else BeginExit(t);
                }else FinishTrip(t,true);
            }
        }
        if(finished==trips.Count){running=false;Export();}
    }
    void MoveCamera(){var camera=Camera.main;if(!camera)return;camera.transform.rotation=Quaternion.Euler(55,orbit,0);camera.transform.position=focus-camera.transform.forward*zoom;}
    public void RemoveThresholdLinks()
    {
        foreach(var link in thresholdLinks)
            if(NavMesh.IsLinkValid(link))NavMesh.RemoveLink(link);
        thresholdLinks.Clear();
    }
    void OnDestroy(){RemoveThresholdLinks();if(nav.valid)nav.Remove();}
    static string Q(string s)=>"\""+s.Replace("\"","\"\"")+"\"";
    public void Export(){
        string dir=Path.Combine(Application.persistentDataPath,"Campus",DateTime.Now.ToString("yyyyMMdd_HHmmssfff"));Directory.CreateDirectory(dir);
        using(var w=new StreamWriter(Path.Combine(dir,"agentes.csv"),false,new UTF8Encoding(true))){
            w.WriteLine("id,tipo,edificio,salon,puesto,estado,entrada_s,sentado_s,salida_puesto_s,fin_s,recorrido_m,traslado_por_atasco");
            int id=0;foreach(var t in trips)w.WriteLine(FormattableString.Invariant($"{++id},{Q(t.seat.role)},{Q(t.seat.building)},{Q(t.seat.room)},{Q(t.seat.name)},{(t.phase==4&&t.toured?"recorrió el campus y salió":new[]{"pendiente","ingresando","sentado","saliendo","finalizado","ruta interrumpida","recorriendo campus"}[t.phase])},{t.entered:F2},{t.sat:F2},{t.left:F2},{t.ended:F2},{t.meters:F2},{(t.relocated?"sí":"no")}"));
        }
        using(var w=new StreamWriter(Path.Combine(dir,"ocupacion.csv"),false,new UTF8Encoding(true))){
            w.WriteLine("salon,capacidad,ocupacion_actual,ocupacion_maxima");
            foreach(var g in trips.GroupBy(t=>t.seat.room)){
                var events=g.Where(t=>t.sat>0).Select(t=>(t.sat,1)).Concat(g.Where(t=>t.left>0).Select(t=>(t.left,-1))).OrderBy(e=>e.Item1).ThenBy(e=>e.Item2);
                int current=0,peak=0;foreach(var e in events){current+=e.Item2;peak=Math.Max(peak,current);}
                w.WriteLine($"{Q(g.Key)},{g.Count()},{current},{peak}");
            }
        }
        using(var w=new StreamWriter(Path.Combine(dir,"rutas_planificadas.csv"),false,new UTF8Encoding(true))){
            w.WriteLine("id,punto,x_m,y_m,z_m");
            for(int i=0;i<trips.Count;i++){
                var route=trips[i].actualRoute??trips[i].route;if(route==null)continue;
                for(int j=0;j<route.Length;j++){var p=route[j];w.WriteLine(FormattableString.Invariant($"{i+1},{j},{p.x:F2},{p.y:F2},{p.z:F2}"));}
            }
        }
        File.WriteAllText(Path.Combine(dir,"rendimiento.csv"),"frames,segundos_reales,fps_medio\n"+FormattableString.Invariant($"{frames},{frameSeconds:F2},{(frameSeconds>0?frames/frameSeconds:0):F2}\n"));
        message="CSV guardados en "+dir;
    }
}
}























