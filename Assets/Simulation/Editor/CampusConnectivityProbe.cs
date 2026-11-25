using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampusMonday;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Experimental.AI;

// Compare connected polygons independently of the normal shortest-path search.
static class CampusConnectivityProbe
{
    const string Menu = "Simulación/Universidad/Diagnosticar conectividad de puestos";
    [MenuItem(Menu, true)]
    static bool CanCompare() => Application.isPlaying;
    [MenuItem(Menu)]
    static void Run()
    {
        var sim = UnityEngine.Object.FindFirstObjectByType<FullCampus>();
        if (sim && sim.baked && sim.inventory != null) Compare(sim);
    }
    static void Compare(FullCampus sim)
    {
        var standard = Classify(sim,Vector3.zero);
        var removed = sim.sources.Where(s => s.shape == NavMeshBuildSourceShape.Box && s.area == 1).ToArray();
        sim.sources.RemoveAll(s => s.shape == NavMeshBuildSourceShape.Box && s.area == 1);
        NavMeshData test = null;
        NavMeshDataInstance handle = default;
        try
        {
            test = sim.Bake();
            var offset = new Vector3(2048,0,0);
            handle = NavMesh.AddNavMeshData(test,offset,Quaternion.identity);
            if (!handle.valid) throw new InvalidOperationException("Fallo al cargar la malla de contraste");
            var alternate = Classify(sim,offset);
            using var writer = new StreamWriter("Documentation/campus_conectividad_comparada.csv");
            writer.WriteLine("edificio,puesto,con_cajas,sin_cajas");
            string Q(string v) => "\"" + v.Replace("\"","\"\"") + "\"";
            for (int i=0;i<standard.Length;i++)
            {
                var s = sim.inventory.seats[i];
                writer.WriteLine($"{Q(s.building)},{Q(s.name)},{standard[i]},{alternate[i]}");
            }
            Debug.Log("CAMPUS_DIAGNOSTICO_OK");
        }
        finally
        {
            if(handle.valid) handle.Remove();
            if(test) UnityEngine.Object.Destroy(test);
            sim.sources.AddRange(removed);
        }
    }
    static string[] Classify(FullCampus sim, Vector3 offset)
    {
        using var query = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(),Allocator.TempJob);
        using var neighbors = new NativeArray<PolygonId>(128,Allocator.TempJob);
        var seen = new HashSet<PolygonId>();
        var pending = new Queue<PolygonId>();
        if (!NavMesh.SamplePosition(sim.inventory.entrance+offset,out var start,8,NavMesh.AllAreas))
            throw new InvalidOperationException("Portería fuera de la malla");
        var extents = new Vector3(.18f,.55f,.18f);
        var first = query.MapLocation(start.position,extents,0);
        if (!query.IsValid(first)) throw new InvalidOperationException("Portería sin polígono");
        seen.Add(first.polygon); pending.Enqueue(first.polygon);
        while(pending.Count>0)
        {
            var status = query.GetEdgesAndNeighbors(pending.Dequeue(),default,neighbors,default,out _,out int count);
            if ((status & PathQueryStatus.Success)==0 || count>neighbors.Length) throw new InvalidOperationException(status.ToString());
            for(int i=0;i<count;i++) if(seen.Add(neighbors[i])) pending.Enqueue(neighbors[i]);
        }
        var results = new string[sim.inventory.seats.Length];
        for(int i=0;i<results.Length;i++)
        {
            var seat=sim.inventory.seats[i];
            var p=seat.position+offset;
            var f=Vector3.ProjectOnPlane(seat.forward,Vector3.up).normalized;
            if(f.sqrMagnitude<.01f) f=Vector3.forward;
            var r=Vector3.Cross(Vector3.up,f);
            bool unmapped = false;
            results[i]="sin aproximacion";
            foreach(var distance in new[]{.48f,.72f})
            {
                foreach(var dir in new[]{r,-r,f,-f,(r+f).normalized,(r-f).normalized,(-r+f).normalized,(-r-f).normalized})
                {
                    if(!NavMesh.SamplePosition(p+dir*distance,out var hit,.57f,NavMesh.AllAreas) ||
                        Mathf.Abs(hit.position.y-p.y)>.55f || Vector3.ProjectOnPlane(hit.position-p,Vector3.up).magnitude>1.05f) continue;
                    var node=query.MapLocation(hit.position,extents,0);
                    if(!query.IsValid(node) || (node.position-hit.position).sqrMagnitude>.04f){ unmapped=true; continue; }
                    results[i]=seen.Contains(node.polygon)?"conectado":"aislado";
                    if(results[i]=="conectado") break;
                }
                if(results[i]=="conectado") break;
            }
            if(unmapped && results[i]!="conectado") results[i]="indeterminado";
        }
        return results;
    }
}


