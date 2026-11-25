using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using CampusMonday;

[InitializeOnLoad]
public static class LandingAudit {
 static LandingAudit(){EditorApplication.update+=Tick;}
 static void Tick(){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Tools/landing_audit.request"))return;File.Delete("Tools/landing_audit.request");Run();}
 static void Run(){var sim=Object.FindAnyObjectByType<MondaySimulation>();Physics.SyncTransforms();var nav=NavMesh.AddNavMeshData(sim.navigation);var report=new List<string>();int missed=0,total=0;float max=0;
 try {foreach(var room in sim.rooms)foreach(var entry in new[]{sim.mainEntrance,sim.rearEntrance}){var path=sim.Route(entry,room.seats[0]);for(int i=1;i<path.Length;i++){int n=Mathf.CeilToInt(Vector3.Distance(path[i-1],path[i])/.1f);for(int j=0;j<=n;j++){var p=Vector3.Lerp(path[i-1],path[i],j/(float)n);total++;var hits=Physics.RaycastAll(p+Vector3.up*1.6f,Vector3.down,3.1f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.normal.y>.55f&&NpcGroundSurface.IsWalkableSurface(h.collider)).OrderBy(h=>Mathf.Abs(h.point.y-p.y)).ToArray();bool found=NpcGroundSurface.TryHeight(p,out float y);if(hits.Length>0){var h=hits[0];float delta=h.point.y-p.y;max=Mathf.Max(max,delta);if((!found||Mathf.Abs(y-h.point.y)>.05f)&&delta>0){if(missed++<100)report.Add($"ROOM {room.id} p={p:F3} found={found} y={y} real={h.point:F3} surface={h.collider.name} segment={path[i-1]:F3} -> {path[i]:F3}");}}}}}
 report.Insert(0,$"total={total} missed={missed} maximumFloorAbovePath={max}");File.WriteAllLines("Results/landing_audit.txt",report);}finally{NavMesh.RemoveNavMeshData(nav);}}
}
