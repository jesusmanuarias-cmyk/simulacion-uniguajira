using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using CampusMonday;

public static class MondayBuilder {
 const string Root="Assets/Simulation/";
 [MenuItem("Simulación/Lunes/1. Construir escena y navegación")]
 public static void Build(){
   Directory.CreateDirectory(Root+"Generated");Directory.CreateDirectory("Results");
   NavMesh.RemoveAllNavMeshData();var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"Building.fbx");if(!importer.isReadable){importer.isReadable=true;importer.importCameras=false;importer.importLights=false;importer.SaveAndReimport();}
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var building=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Building.fbx"));building.name="Modelo original · Bloques A y B";
   var dump=new List<string>();foreach(var r in building.GetComponentsInChildren<Renderer>())if(r.name.Contains("Tablero")||r.name.Contains("Escaleras")||r.name.Contains("Piso"))dump.Add(r.name+" "+r.bounds.center+" "+r.bounds.size);
   File.WriteAllLines("Documentation/unity_geometry.txt",dump);
   var shaders=Shader.Find("Universal Render Pipeline/Lit");CampusUpgrade.ApplyMaterials(building);
   var sim=new GameObject("Simulación del lunes").AddComponent<MondaySimulation>();sim.building=building.transform;
   var roomList=new List<RoomDefinition>();
   for(int i=0;i<20;i++){
     string id,prefix;int floor;
     if(i<8){id=(1102+i).ToString();floor=1;prefix="P1_"+(i<4?"N"+(i+1):"S"+(i-3));}
     else if(i<13){int j=i-8;id=(1204+j).ToString();floor=2;prefix="P2_"+(j<4?"N"+(j+1):"AulaNE");}
     else {int j=i-13;id=(1303+j).ToString();floor=3;prefix="P3_"+(j<4?"N"+(j+1):"S"+(j-3));}
     var all=building.GetComponentsInChildren<Renderer>();var board=all.First(r=>r.name==prefix+"_Tablero");
     var chairs=all.Where(r=>r.name.StartsWith(prefix+"_Silla")&&!r.name.Contains("Profesor")).ToArray();
     var bounds=chairs[0].bounds;foreach(var r in chairs)bounds.Encapsulate(r.bounds);
     float fy=board.bounds.min.y-.86f;var center=bounds.center;center.y=fy;
     var direction=board.bounds.center-center;direction.y=0;direction=Mathf.Abs(direction.x)>Mathf.Abs(direction.z)?new Vector3(Mathf.Sign(direction.x),0,0):new Vector3(0,0,Mathf.Sign(direction.z));
     var right=Vector3.Cross(Vector3.up,direction);var seats=new Vector3[32];
     var group=new GameObject("Aula "+id+" · "+prefix);group.transform.SetParent(building.transform);
     for(int k=0;k<32;k++){
       var pos=center+right*((k%8-3.5f)*.63f)+direction*((1.5f-k/8)*1.1f);seats[k]=pos;
       Cube("Asiento "+id+" "+k,pos+Vector3.up*.43f,new Vector3(.43f,.08f,.43f),new Color(.12f,.28f,.42f),group.transform);
       Cube("Respaldo "+id+" "+k,pos-direction*.22f+Vector3.up*.72f,new Vector3(.43f,.52f,.07f),new Color(.12f,.28f,.42f),group.transform,Quaternion.LookRotation(direction));
       Cube("Mesa "+id+" "+k,pos+direction*.42f+Vector3.up*.77f,new Vector3(.56f,.08f,.40f),new Color(.78f,.73f,.61f),group.transform,Quaternion.LookRotation(direction));
       Cube("Soporte "+id+" "+k,pos+Vector3.up*.21f,new Vector3(.08f,.42f,.08f),Color.gray,group.transform);
     }
     foreach(var r in all.Where(r=>r.name.StartsWith(prefix+"_")&&(r.name.Contains("Mesa")||r.name.Contains("Silla"))))r.gameObject.SetActive(false);
     // The corridor is the inner side of each wing; projection onto NavMesh resolves the actual doorway.
     var inner=board.bounds.size.x>board.bounds.size.z?new Vector3(-Mathf.Sign(center.x),0,0):new Vector3(0,0,-Mathf.Sign(center.z));
     var door=center+inner*3.7f-direction*1.5f;
     roomList.Add(new RoomDefinition{id=id,modelPrefix=prefix,floor=floor,board=board.bounds.center,teacher=new Vector3(board.bounds.center.x,fy,board.bounds.center.z)-direction*1.0f,seats=seats,door=door,corridor=door+inner});
   }
   sim.rooms=roomList.ToArray();
   var sources=new List<NavMeshBuildSource>();
   foreach(var m in building.GetComponentsInChildren<MeshFilter>()){
     if(!m.sharedMesh||m.sharedMesh.vertexCount==0||!m.gameObject.activeInHierarchy)continue;
     var r=m.GetComponent<Renderer>();if(r==null)continue;
     if(r.bounds.max.y<-.5f||r.bounds.min.y>11)continue;
     // Seats do not obstruct the final sitting location; tables and walls do.
     if(m.name.StartsWith("Asiento")||m.name.StartsWith("Respaldo")||m.name.StartsWith("Soporte")||m.name.Contains("Oficina_Silla")||m.name.Contains("Carpinteria"))continue;
     sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Mesh,sourceObject=m.sharedMesh,transform=m.transform.localToWorldMatrix,area=0});
   }
   var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=.12f;settings.agentHeight=1.65f;settings.agentClimb=.28f;settings.agentSlope=50;settings.overrideVoxelSize=true;settings.voxelSize=.05f;settings.minRegionArea=.05f;
   var nav=UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(new Vector3(0,5,0),new Vector3(200,25,200)),Vector3.zero,Quaternion.identity);
   if(!nav)throw new Exception("No se generó navegación");AssetDatabase.CreateAsset(nav,Root+"Generated/LunesNavigation.asset");sim.navigation=nav;var handle=NavMesh.AddNavMeshData(nav);
   Vector3 Snap(Vector3 p,float range=3){if(!NavMesh.SamplePosition(p,out var hit,range,NavMesh.AllAreas))throw new Exception("Punto fuera de navegación "+p);return hit.position;}
   foreach(var r in sim.rooms){r.door=Snap(r.door);for(int k=0;k<r.seats.Length;k++)r.seats[k]=Snap(r.seats[k],.6f);r.teacher=Snap(r.teacher);}
   var first=sim.rooms[0];var roomCenters=sim.rooms.Where(r=>r.floor==1).Select(r=>r.seats.Aggregate(Vector3.zero,(a,b)=>a+b)/32).ToArray();
   var blockCenter=roomCenters.Aggregate(Vector3.zero,(a,b)=>a+b)/roomCenters.Length;
   sim.mainEntrance=Snap(new Vector3(blockCenter.x,.15f,roomCenters.Min(p=>p.z)-7),15);
   sim.rearEntrance=Snap(new Vector3(blockCenter.x,.15f,roomCenters.Max(p=>p.z)+5),15);
   sim.officeSeats=new Vector3[12];sim.officeDoors=new Vector3[12];
   var officeChairs=building.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("P2_Oficina_Silla")&&!r.name.Contains("Visita")).OrderByDescending(r=>r.bounds.center.z).Take(6).ToArray();
   var visitorChairs=building.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("P2_Oficina_SillaVisita")).OrderByDescending(r=>r.bounds.center.z).ToArray();
   for(int k=0;k<12;k++){var p=(k%2==0?officeChairs[k/2]:visitorChairs[k/2]).bounds.center;p.y=3.4f;sim.officeSeats[k]=Snap(p,1);sim.officeDoors[k]=Snap(new Vector3(8.8f,3.4f,p.z),3);}
   sim.office=sim.officeSeats[0];
   var cam=new GameObject("Cámara de la simulación").AddComponent<Camera>();cam.tag="MainCamera";cam.backgroundColor=new Color(.56f,.68f,.74f);cam.clearFlags=CameraClearFlags.SolidColor;cam.farClipPlane=350;cam.nearClipPlane=.15f;cam.fieldOfView=50;
   cam.transform.position=blockCenter+new Vector3(30,40,-35);cam.transform.LookAt(blockCenter);
   var sun=new GameObject("Luz del campus").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.4f;sun.transform.rotation=Quaternion.Euler(48,-35,0);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.68f,.72f,.79f);
   sim.characters=new[]{"Ch01_nonPBR","Ch22_nonPBR","Ch33_nonPBR"}.Select(name=>{
     var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/info/"+name+".fbx"));
     int mi=0;var materials=new Dictionary<Material,Material>();
     foreach(var r in instance.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>{
       if(!m)return m;if(!materials.ContainsKey(m)){var c=new Material(shaders);c.name=m.name;c.SetColor("_BaseColor",Color.white);string tile=m.name.ToLowerInvariant().Contains("hair")&&name!="Ch01_nonPBR"?"1002":"1001";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Textures/"+name.Substring(0,4)+"_"+tile+"_Diffuse.png");c.SetTexture("_BaseMap",texture);c.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(c,Root+"Generated/"+name+"_"+(mi++)+".mat");materials[m]=c;}return materials[m];}).ToArray();
     var prefab=PrefabUtility.SaveAsPrefabAsset(instance,Root+"Generated/"+name+".prefab");UnityEngine.Object.DestroyImmediate(instance);return prefab;
   }).ToArray();
   if(sim.characters.Any(c=>!c))throw new Exception("Falta personaje FBX");
   ImportedAnimationSetup.ApplyExisting(sim);
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"Lunes.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"Lunes.unity",true)};
   NavMesh.RemoveNavMeshData(handle);AssetDatabase.SaveAssets();
   File.WriteAllText("Documentation/scene_config.json",JsonUtility.ToJson(new SceneExport{rooms=sim.rooms,main=sim.mainEntrance,rear=sim.rearEntrance,office=sim.office},true));
   Debug.Log("MONDAY_BUILD_OK");
 }
 [Serializable] class SceneExport{public RoomDefinition[] rooms;public Vector3 main,rear,office;}
 static void Cube(string name,Vector3 p,Vector3 size,Color color,Transform parent,Quaternion? rotation=null){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);go.transform.position=p;go.transform.localScale=size;go.transform.rotation=rotation??Quaternion.identity;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());string path=Root+"Generated/Furniture_"+(color.b>.4f?"seat":"wood")+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=color;AssetDatabase.CreateAsset(mat,path);}go.GetComponent<Renderer>().sharedMaterial=mat;}
 [MenuItem("Simulación/Lunes/2. Ejecutar cinco réplicas por escenario")]
 public static void Replicas(){
   EditorSceneManager.OpenScene(Root+"Lunes.unity");var sim=UnityEngine.Object.FindFirstObjectByType<MondaySimulation>();var nav=NavMesh.AddNavMeshData(sim.navigation);var data=JsonUtility.FromJson<MondayData>(Resources.Load<TextAsset>("monday").text);
   var summary=new List<string>{"escenario,semilla,estudiantes,profesores,administrativos,pico_total,pico_sin_profesores,espera_media_s,recorrido_entrada_medio_s,ultima_salida_min,rutas_fallidas"};
   for(int scenario=0;scenario<2;scenario++)for(int i=0;i<5;i++){
     var settings=new ModelSettings{seed=1401+i,alternative=scenario==1,mainShare=scenario==0?.9f:.6f};var e=new MondayEngine(data,sim.rooms,sim.mainEntrance,sim.rearEntrance,sim.office,settings,sim.Route,sim.officeSeats,sim.officeDoors);
     string tag=(scenario==0?"base":"alternativo")+"_"+settings.seed;e.Export("Results",tag,data);
     int peak=0,peakNo=0;foreach(var t in e.people.SelectMany(p=>new[]{p.arrival,p.depart})){peak=Math.Max(peak,e.Count(t));peakNo=Math.Max(peakNo,e.Count(t,Role.Estudiante)+e.Count(t,Role.Administrativo));}
     string F(double v)=>v.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture);
     summary.Add(string.Join(",",scenario==0?"Base":"Alternativo",settings.seed,e.people.Count(p=>p.role==Role.Estudiante),e.people.Count(p=>p.role==Role.Profesor),e.people.Count(p=>p.role==Role.Administrativo),peak,peakNo,F(e.people.Average(p=>(p.admitted-p.doorArrival)*60)),F(e.people.Average(p=>(p.seated-p.arrival)*60)),F(e.people.Max(p=>p.depart)),e.pathFailures));
     if(e.people.Any(p=>p.seated>p.release||p.depart<p.release||p.admitted<p.doorArrival))throw new Exception("Cronología inválida");
     if(e.Count(1380)!=0)throw new Exception("El edificio no quedó vacío");
     Debug.Log("REPLICA_OK "+tag);
   }
   File.WriteAllLines("Results/resumen_replicas.csv",summary);NavMesh.RemoveNavMeshData(nav);Debug.Log("MONDAY_REPLICAS_OK");
 }
 [Serializable] class NavDump{public Vector3[] vertices;public int[] indices;}
 public static void Diagnose(){var t=NavMesh.CalculateTriangulation();File.WriteAllText("Documentation/navmesh.json",JsonUtility.ToJson(new NavDump{vertices=t.vertices,indices=t.indices}));}
 public static void BuildAndRun(){Build();try{Replicas();}catch{Diagnose();throw;}}
 public static void UpdateVisuals(){
   foreach(var name in new[]{"Ch01_nonPBR","Ch22_nonPBR","Ch33_nonPBR"}){
     foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"Generated"})){
       var path=AssetDatabase.GUIDToAssetPath(guid);if(!Path.GetFileName(path).StartsWith(name))continue;var m=AssetDatabase.LoadAssetAtPath<Material>(path);
       string tile=m.name.ToLowerInvariant().Contains("hair")&&name!="Ch01_nonPBR"?"1002":"1001";
       m.SetColor("_BaseColor",Color.white);m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Textures/"+name.Substring(0,4)+"_"+tile+"_Diffuse.png"));EditorUtility.SetDirty(m);
     }
   }AssetDatabase.SaveAssets();
   var sim=UnityEngine.Object.FindFirstObjectByType<MondaySimulation>();if(sim){var handle=NavMesh.AddNavMeshData(sim.navigation);
     var chairs=sim.building.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("P2_Oficina_Silla")&&!r.name.Contains("Visita")).OrderByDescending(r=>r.bounds.center.z).Take(6).ToArray();
     var visitors=sim.building.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("P2_Oficina_SillaVisita")).OrderByDescending(r=>r.bounds.center.z).ToArray();
     for(int k=0;k<12;k++){var pos=(k%2==0?chairs[k/2]:visitors[k/2]).bounds.center;pos.y=3.4f;if(!NavMesh.SamplePosition(pos,out var h,.8f,NavMesh.AllAreas))throw new Exception("Silla de oficina desconectada");sim.officeSeats[k]=h.position;}
     NavMesh.RemoveNavMeshData(handle);EditorUtility.SetDirty(sim);EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());Replicas();
   }File.WriteAllText("Results/visual_status.txt","Texturas originales y puestos de oficina verificados");
 }
}
// Refresh compilation


[InitializeOnLoad]
public static class MondayAutoSetup {
 static bool busy;
 static MondayAutoSetup(){EditorApplication.update+=Tick;}
 static void Tick(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   if(File.Exists("Tools/visual.request")){try{File.Delete("Tools/visual.request");}catch(IOException){return;}MondayBuilder.UpdateVisuals();}
   if(!File.Exists("Tools/build.request"))return;
   try{File.Delete("Tools/build.request");}catch(IOException){return;}busy=true;EditorApplication.delayCall+=()=>{try{MondayBuilder.BuildAndRun();File.WriteAllText("Results/build_status.txt","OK "+DateTime.Now);}catch(Exception e){File.WriteAllText("Results/build_status.txt",e.ToString());Debug.LogException(e);}finally{busy=false;}};
 }
}


