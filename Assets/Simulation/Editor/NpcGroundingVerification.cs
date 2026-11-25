using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using CampusMonday;

[InitializeOnLoad]
public static class NpcGroundingVerification {
 static bool busy;
 static NpcGroundingVerification(){EditorApplication.update+=Tick;}
 static void Tick(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Tools/verify_grounding.request"))return;
   try{File.Delete("Tools/verify_grounding.request");}catch(IOException){return;}busy=true;
   try{Verify();}catch(Exception e){File.WriteAllText("Results/npc_grounding_verification.txt",e.ToString());Debug.LogException(e);}finally{busy=false;}
 }
 static string F(float value)=>value.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture);
 [MenuItem("Simulación/Lunes/7. Verificar altura de NPC y escaleras")]
 public static void Verify(){
   foreach(string name in new[]{"Ch01_nonPBR","Ch22_nonPBR","Ch33_nonPBR"}){var importer=(ModelImporter)AssetImporter.GetAtPath("Assets/info/"+name+".fbx");if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}}
   var sim=UnityEngine.Object.FindFirstObjectByType<MondaySimulation>();if(!sim)throw new Exception("Abrir escena Lunes");Physics.SyncTransforms();var nav=NavMesh.AddNavMeshData(sim.navigation);
   var stairs=new List<(Vector3 p,Vector3 direction)>();int samples=0;float originalPenetration=0,fixedPenetration=0;var report=new List<string>();
   try{
     foreach(var room in sim.rooms)foreach(var entry in new[]{sim.mainEntrance,sim.rearEntrance}){
       var route=sim.Route(entry,room.seats[0]);if(route==null)throw new Exception("Ruta desconectada "+room.id);
       for(int i=1;i<route.Length;i++){float length=Vector3.Distance(route[i-1],route[i]);int count=Mathf.Max(1,Mathf.CeilToInt(length/.12f));
         for(int j=0;j<=count;j++){var p=Vector3.Lerp(route[i-1],route[i],j/(float)count);if(!NpcGroundSurface.TryHeight(p,out float ground))continue;
           samples++;originalPenetration=Mathf.Max(originalPenetration,ground-p.y);var corrected=NpcGroundSurface.Project(p);fixedPenetration=Mathf.Max(fixedPenetration,ground-corrected.y);
           if(Mathf.Abs(route[i].y-route[i-1].y)>.1f&&j%3==0)stairs.Add((corrected,route[i]-route[i-1]));
         }
       }
     }
     report.Add($"Rutas reales: 40; muestras de superficies: {samples}; puntos en escaleras: {stairs.Count}");
     report.Add($"Penetración máxima del pivote original: {F(originalPenetration)} m; corregida: {F(fixedPenetration)} m");
     if(samples<100||stairs.Count<10||fixedPenetration>.002f)throw new Exception("Cobertura insuficiente o pivote por debajo del piso");
     for(int role=0;role<3;role++){
       var view=sim.CreateView(role);var mesh=new Mesh();float minFlat=10,minStair=10,maxLift=0;int phaseSamples=0;string worst="";
       try{
         var flat=NpcGroundSurface.Project(sim.mainEntrance);NpcGroundSurface.TryHeight(flat,out float floor);
         for(int i=0;i<32;i++){view.transform.position=flat;view.Pose(false,true,i/32f*1.033333f);maxLift=Mathf.Max(maxLift,view.GroundLift);
           float sole=10;foreach(var renderer in view.GetComponentsInChildren<SkinnedMeshRenderer>()){renderer.BakeMesh(mesh);foreach(var vertex in mesh.vertices)sole=Mathf.Min(sole,renderer.transform.TransformPoint(vertex).y-floor);}
           minFlat=Mathf.Min(minFlat,sole);phaseSamples++;
         }
         foreach(var point in stairs.Where((p,i)=>i%Mathf.Max(1,stairs.Count/80)==0).Take(80)){view.transform.position=point.p;var d=point.direction;d.y=0;if(d.sqrMagnitude>.001f)view.transform.rotation=Quaternion.LookRotation(d);view.Pose(false,true,phaseSamples*.071f);maxLift=Mathf.Max(maxLift,view.GroundLift);phaseSamples++;
           foreach(var renderer in view.GetComponentsInChildren<SkinnedMeshRenderer>()){renderer.BakeMesh(mesh);var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i+=3){var world=renderer.transform.TransformPoint(vertices[i]);if(world.y>point.p.y+.5f)continue;var probe=world;probe.y=point.p.y;if(NpcGroundSurface.TryHeight(probe,out float surface)&&world.y-surface<minStair){minStair=world.y-surface;worst=" root="+point.p.ToString("F3")+" vertex="+world.ToString("F3")+" surface="+F(surface)+" lift="+F(view.GroundLift);}}}
         }
         report.Add($"Rol {(Role)role}: {phaseSamples} poses de Walking; mínima altura de la malla sobre piso plano: {F(minFlat)} m; en escaleras: {F(minStair)} m; ajuste máximo de pies: {F(maxLift)} m");
         if(minFlat<-.035f||minStair<-.04f)throw new Exception("La malla de los pies sigue entrando en el piso: "+(Role)role+" plano="+F(minFlat)+" escalera="+F(minStair)+worst);
       }finally{UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(view.gameObject);}
     }
     report.Insert(0,"OK: recorridos a los tres pisos, contacto con peldaños y mallas de pies en 32 fases de Walking por personaje.");File.WriteAllLines("Results/npc_grounding_verification.txt",report);Debug.Log("NPC_GROUNDING_VERIFIED");
   }finally{NavMesh.RemoveNavMeshData(nav);}
 }
}
