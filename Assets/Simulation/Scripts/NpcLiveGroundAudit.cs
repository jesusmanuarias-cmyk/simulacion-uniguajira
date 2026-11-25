#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CampusMonday {
 public class NpcLiveGroundAudit:MonoBehaviour {
   [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
   static void StartAudit(){if(File.Exists(Path.Combine(Application.dataPath,"../Tools/audit_live.request")))new GameObject("Auditoría temporal NPC").AddComponent<NpcLiveGroundAudit>();}
   IEnumerator Start(){
     string request=Path.Combine(Application.dataPath,"../Tools/audit_live.request");File.Delete(request);
     var sim=FindAnyObjectByType<MondaySimulation>();yield return null;sim.paused=false;sim.Seek(390);sim.minutesPerSecond=.25f;sim.floor=0;
     var report=new List<string>();float worst=10,maxHeight=0;int samples=0;var mesh=new Mesh();
     foreach(var floor in sim.building.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name.Contains("_Losa")||m.name.Contains("Escaleras"))){var c=floor.GetComponent<Collider>();report.Add($"SURFACE {floor.name} active={floor.gameObject.activeInHierarchy} layer={floor.gameObject.layer} renderer={floor.GetComponent<Renderer>().bounds} collider={(c?c.bounds.ToString():"NONE")} enabled={(c&&c.enabled)}");}
     foreach(float y in new[]{.15f,3.4f,6.65f}){var point=new Vector3(-10.065f,y,-68.935f);foreach(var h in Physics.RaycastAll(point+Vector3.up*.6f,Vector3.down,1.4f,~0,QueryTriggerInteraction.Ignore))report.Add($"RAY y={y} hit={h.collider.name} layer={h.collider.gameObject.layer} height={h.point.y:F3} normal={h.normal:F3}");bool previous=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;foreach(var h in Physics.RaycastAll(point+Vector3.up*.95f,Vector3.down,1.8f,~0,QueryTriggerInteraction.Ignore))report.Add($"BACKFACE_RAY y={y} hit={h.collider.name} height={h.point.y:F3} normal={h.normal:F3}");Physics.queriesHitBackfaces=previous;}
     for(int frame=0;frame<360;frame++){
       yield return new WaitForEndOfFrame();
       if(frame%6!=0)continue;
       foreach(var view in FindObjectsByType<PersonView>().Where(v=>v.MotionState=="Walk").OrderByDescending(v=>v.transform.position.y).Take(10)){
         float clearance=10;maxHeight=Mathf.Max(maxHeight,view.transform.position.y);
         foreach(var renderer in view.GetComponentsInChildren<SkinnedMeshRenderer>()){renderer.BakeMesh(mesh);var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i+=6){var w=renderer.transform.TransformPoint(vertices[i]);if(w.y>view.transform.position.y+.5f)continue;var p=w;p.y=view.transform.position.y;if(NpcGroundSurface.TryHeight(p,out float ground))clearance=Mathf.Min(clearance,w.y-ground);}}
         samples++;if(clearance<worst){worst=clearance;report.Add($"frame={frame} t={sim.clock:F3} {view.name} root={view.transform.position:F3} lift={view.GroundLift:F3} actualVisualOffset={view.VisualOffset:F3} probeClearance={view.ProbeClearance():F3} meshClearance={clearance:F3}");foreach(var h in Physics.RaycastAll(view.transform.position+Vector3.up*.6f,Vector3.down,1.4f,~0,QueryTriggerInteraction.Ignore))report.Add($"WORST_RAY hit={h.collider.name} layer={h.collider.gameObject.layer} height={h.point.y:F3} normal={h.normal:F3}");}
       }
     }
     report.Insert(0,$"{(samples>100&&worst>=-.01f&&maxHeight>6?"OK":"FAILED")} LIVE_END_OF_FRAME samples={samples}; minimumMeshClearance={worst:F4}; maxRootHeight={maxHeight:F3}");File.WriteAllLines(Path.Combine(Application.dataPath,"../Results/npc_live_ground_audit.txt"),report);Destroy(mesh);Destroy(gameObject);
   }
 }
}
#endif
