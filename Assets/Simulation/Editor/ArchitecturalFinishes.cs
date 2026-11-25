using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CampusMonday;

[InitializeOnLoad]
public static class ArchitecturalFinishes {
 const string Folder="Assets/Simulation/ArchitecturalMaterials/";
 static bool busy;
 static ArchitecturalFinishes(){EditorApplication.update+=Tick;}
 static void Tick(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Tools/surfaces.request"))return;
   try{File.Delete("Tools/surfaces.request");}catch(IOException){return;}busy=true;
   EditorApplication.delayCall+=()=>{try{UpdateScene();}catch(Exception e){File.WriteAllText("Results/surfaces_status.txt",e.ToString());Debug.LogException(e);}finally{busy=false;}};
 }
 static Material Material(string name,string shader,Color color){var path=Folder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}m.shader=Shader.Find(shader);m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);return m;}
 public static void Apply(GameObject building){
   Directory.CreateDirectory(Folder);
   var floor=Material("Piso_Gris_Baldosa_60cm","Campus/Piso gris con juntas",new Color(.68f,.68f,.67f));
   floor.SetColor("_GroutColor",new Color(.44f,.44f,.43f));floor.SetColor("_UndersideColor",new Color(.74f,.74f,.73f));floor.SetFloat("_TileSize",.6f);floor.SetFloat("_JointWidth",.004f);
   var ceiling=Material("Techo_Gris_Claro_Mate","Universal Render Pipeline/Lit",new Color(.74f,.74f,.73f));ceiling.SetFloat("_Smoothness",0);ceiling.SetFloat("_Metallic",0);
   var roof=Material("Cubierta_Gris_Mate","Universal Render Pipeline/Lit",new Color(.36f,.37f,.38f));roof.SetFloat("_Smoothness",0);roof.SetFloat("_Metallic",0);
   var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Simulation/Building.fbx").GetComponentsInChildren<Renderer>(true).ToDictionary(r=>r.name);
   int floors=0,ceilings=0,roofs=0;
   foreach(var r in building.GetComponentsInChildren<Renderer>(true)){
     if(!source.TryGetValue(r.name,out var original))continue;string objectName=r.name.ToLowerInvariant();
     bool cielo=objectName.Contains("cieloraso")||objectName.Contains("cielo_raso")||objectName.Contains("plafon");
     bool cubierta=objectName=="losa_cubierta"||objectName.Contains("azotea");
     var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++){
       string originalName=i<original.sharedMaterials.Length&&original.sharedMaterials[i]?original.sharedMaterials[i].name.ToLowerInvariant():"";
       if(cielo){mats[i]=ceiling;ceilings++;}
       else if(cubierta||originalName=="cubierta"){mats[i]=roof;roofs++;}
       else if(originalName=="terrazo"||originalName=="anden"||originalName=="mat_anden_concreto"||originalName=="sit_anden_concreto"||originalName.Contains("baldosas")||originalName.Contains("porcelanato")||((objectName.Contains("piso")||objectName.StartsWith("losa_"))&&originalName.Contains("concreto"))){mats[i]=floor;floors++;}
       else if(objectName.EndsWith("_losa")&&originalName=="blanco"){mats[i]=ceiling;ceilings++;}
     }r.sharedMaterials=mats;
   }
   File.WriteAllText("Documentation/architectural_finishes.txt",$"Referencias: Cam_Pasillo_P3.png y Cam_Patio.png suministradas por el usuario.\nPiso: gris mate con cuadrícula de baldosas 0,60 x 0,60 m y junta gris de 4 mm; proyección en coordenadas mundiales para evitar UV deformadas.\nCielo raso: gris claro mate; cubierta: gris oscuro mate.\nAsignaciones de suelo: {floors}; cielos rasos: {ceilings}; cubiertas: {roofs}.\nLa geometría, navegación y los horarios no se modifican.");
 }
 [MenuItem("Simulación/Lunes/4. Corregir pisos y techos grises")]
 public static void UpdateScene(){
   var sim=UnityEngine.Object.FindFirstObjectByType<MondaySimulation>();if(!sim){EditorSceneManager.OpenScene("Assets/Simulation/Lunes.unity");sim=UnityEngine.Object.FindFirstObjectByType<MondaySimulation>();}
   Apply(sim.building.gameObject);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
   RenderViews(sim);
   File.WriteAllText("Results/surfaces_status.txt","OK pisos grises con baldosas, cielos rasos y cubiertas mates");Debug.Log("ARCHITECTURAL_FINISHES_OK");
 }
 static void RenderViews(MondaySimulation sim){
   var go=new GameObject("Verificación temporal de acabados");var camera=go.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.39f,.61f,.82f);camera.nearClipPlane=.06f;camera.farClipPlane=200;camera.fieldOfView=65;
   void Capture(string filename,Vector3 position,Vector3 target){camera.transform.position=position;camera.transform.LookAt(target);var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("Documentation/"+filename,texture.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);}
   var r=sim.rooms.First(x=>x.floor==3);var pos=r.door+new Vector3(1,1.6f,1);Capture("piso_pasillo_unity.png",pos,pos+new Vector3(0,-.25f,15));
   var center=sim.rooms[0].board+new Vector3(15,0,15);Capture("cubierta_gris_unity.png",center+new Vector3(20,24,-25),center);
   UnityEngine.Object.DestroyImmediate(go);
 }
}
