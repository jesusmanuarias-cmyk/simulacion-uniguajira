#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CampusMonday {
 public class LandingLiveAudit:MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Begin(){if(File.Exists("Tools/landing_live.request"))new GameObject("Prueba del descanso").AddComponent<LandingLiveAudit>();}
  IEnumerator Start(){File.Delete("Tools/landing_live.request");yield return null;var sim=FindAnyObjectByType<MondaySimulation>();sim.paused=true;var room=sim.rooms.First(r=>r.id=="1206");var route=sim.Route(sim.mainEntrance,room.seats[0]);var points=new List<(Vector3 p,Vector3 direction)>();
   for(int i=1;i<route.Length;i++){int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(route[i-1],route[i])/.12f));for(int j=0;j<=n;j++){var p=Vector3.Lerp(route[i-1],route[i],j/(float)n);if(p.x< -7&&p.z< -50&&p.z> -68&&p.y>1.8f&&p.y<3.6f)points.Add((p,route[i]-route[i-1]));}}
   var views=Enumerable.Range(0,3).Select(r=>sim.CreateView(r)).ToArray();var mesh=new Mesh();float worst=10;int samples=0,missing=0,missingRoots=0;var report=new List<string>();
   foreach(int sign in new[]{1,-1})foreach(var item in points){for(int r=0;r<3;r++){var v=views[r];v.transform.position=NpcGroundSurface.Project(item.p);var d=item.direction*sign;d.y=0;if(d.sqrMagnitude>.000001f)v.transform.rotation=Quaternion.LookRotation(d);v.Pose(false,true,samples*.071f+r*.19f);}
    yield return new WaitForEndOfFrame();
    foreach(var v in views)if(!Physics.RaycastAll(v.transform.position+Vector3.up*.3f,Vector3.down,.5f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.normal.y>.55f&&NpcGroundSurface.IsWalkableSurface(h.collider)))missingRoots++;
    foreach(var v in views){samples++;foreach(var renderer in v.GetComponentsInChildren<SkinnedMeshRenderer>()){renderer.BakeMesh(mesh);var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i+=6){var w=renderer.transform.TransformPoint(vertices[i]);if(w.y>v.transform.position.y+.55f)continue;var origin=new Vector3(w.x,v.transform.position.y+1.6f,w.z);var hits=Physics.RaycastAll(origin,Vector3.down,2.45f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.normal.y>.55f&&NpcGroundSurface.IsWalkableSurface(h.collider)&&h.point.y<=v.transform.position.y+.75f).OrderByDescending(h=>h.point.y).ToArray();if(hits.Length==0){missing++;continue;}float clearance=w.y-hits[0].point.y;if(clearance<worst){worst=clearance;report.Add($"role={v.name} direction={sign} root={v.transform.position:F3} meshClearance={clearance:F4} surface={hits[0].collider.name}");}}}}
   }
   report.Insert(0,$"{(samples>100&&worst>=-.01f&&missingRoots==0?"OK":"FAILED")} LANDING_BOTH_DIRECTIONS points={points.Count} poses={samples} minimumClearance={worst:F4} missingRootSurfaces={missingRoots} verticesOutsideSurface={missing}");File.WriteAllLines("Results/landing_live_audit.txt",report);foreach(var v in views)Destroy(v.gameObject);Destroy(mesh);Destroy(gameObject);
  }
 }
}
#endif
