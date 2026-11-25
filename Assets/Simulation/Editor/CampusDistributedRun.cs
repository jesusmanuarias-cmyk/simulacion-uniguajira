using CampusMonday;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
static class CampusDistributedRun
{
    const string Request="Tools/campus_distributed_run.request";
    const string Progress="Library/campus_distributed_run.txt";
    const BindingFlags Instance=BindingFlags.Instance|BindingFlags.NonPublic;
    static bool watching;
    static double at;
    static DateTime started;
    static CampusDistributedRun()=>EditorApplication.update+=Poll;

    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        if(File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying=true;
            return;
        }
        if(!Application.isPlaying)return;
        var campus=Object.FindAnyObjectByType<FullCampus>();
        if(!campus || !campus.GetComponent<MondayDashboard>())return;
        if(File.Exists(Request))
        {
            File.Delete(Request);
            campus.Restart();
            campus.SetRate(15);
            started=DateTime.Now;
            campus.ToggleRun();
            watching=true;
            at=0;
        }
        if(!watching || EditorApplication.timeSinceStartup<at)return;
        at=EditorApplication.timeSinceStartup+10;
        var type=typeof(FullCampus);
        int assigned=(int)type.GetField("next",Instance).GetValue(campus);
        int finished=(int)type.GetField("finished",Instance).GetValue(campus);
        int eligible=(int)type.GetField("eligibleStalls",Instance).GetValue(campus);
        int seated=0,moved=0;
        foreach(var trip in (IEnumerable)type.GetField("trips",Instance).GetValue(campus))
        {
            if((float)trip.GetType().GetField("sat").GetValue(trip)>0)seated++;
            if((bool)trip.GetType().GetField("relocated").GetValue(trip))moved++;
        }
        File.WriteAllText(Progress,$"{campus.Clock} | {assigned} gestionados | {finished} finalizaron | {seated} ocuparon puesto | {eligible} elegibles | {moved} trasladados");
        if(campus.Running)return;
        watching=false;
        var folder=Path.Combine(Application.persistentDataPath,"Campus");
        var output=Directory.GetDirectories(folder).Where(p=>Directory.GetLastWriteTime(p)>=started)
            .Select(p=>Path.Combine(p,"agentes.csv")).Where(File.Exists).OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
        if(output!=null)
            File.Copy(output,"Documentation/campus_simulacion_distribuida.csv",true);
    }
}
