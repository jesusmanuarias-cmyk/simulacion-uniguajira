using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CampusMonday;

[InitializeOnLoad]
public static class Week14Runner {
 static bool busy;
 static Week14Runner(){EditorApplication.update+=Tick;}
 static void Tick(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Tools/week14.request"))return;File.Delete("Tools/week14.request");Run();}
 [MenuItem("Simulación/Lunes/8. Actualizar evidencia Semana 14")]
 public static void Run(){busy=true;try{
  var sim=UnityEngine.Object.FindAnyObjectByType<MondaySimulation>();if(!sim||sim.gameObject.scene.path!="Assets/Simulation/Lunes.unity")throw new Exception("Abrir Assets/Simulation/Lunes.unity antes de ejecutar la evidencia.");
  if(sim.gameObject.scene.isDirty)EditorSceneManager.SaveScene(sim.gameObject.scene);
  MondayBuilder.Replicas();
  var evidence=new System.Collections.Generic.List<string>{"EJECUCION_UNITY_COMPLETADA", "UTC="+DateTime.UtcNow.ToString("o"),"Unity="+Application.unityVersion,"Escena=Assets/Simulation/Lunes.unity","Semillas=1401,1402,1403,1404,1405","Base principal/trasera=0.9/0.1","Alternativo principal/trasera=0.6/0.4","Cambian acceso, velocidad individual y llegada por semilla; solo mainShare cambia entre escenarios.","Referencia declarada real por el grupo. Consultar Documentation/aclaracion_procedencia_datos.json para distinguir agregados derivados y supuestos; los tiempos requieren contraste medido."};
  foreach(var path in new[]{"Assets/info/simulasion.xlsx","Assets/Simulation/Resources/monday.json","Assets/Simulation/Lunes.unity","Assets/Simulation/Generated/LunesNavigation.asset","Assets/Simulation/Scripts/MondayModel.cs","Assets/Simulation/Scripts/MondaySimulation.cs","Assets/Simulation/Scripts/NpcGroundSurface.cs","Results/resumen_replicas.csv"}){
   using(var sha=SHA256.Create())evidence.Add(path+" SHA256="+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());
  }
  File.WriteAllLines("Results/ejecucion_semana14.txt",evidence);Debug.Log("SEMANA14_EVIDENCIA_OK");
 }catch(Exception e){File.WriteAllText("Results/ejecucion_semana14.txt","FALLO\n"+e);Debug.LogException(e);}finally{busy=false;}}
}
