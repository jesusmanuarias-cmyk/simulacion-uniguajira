using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using CampusMonday;

[InitializeOnLoad]
public static class CampusUpgrade {
 const string Folder="Assets/Simulation/Player/";
 [Serializable] class MaterialEntry { public string name;public float[] color;public float metallic,roughness; }
 [Serializable] class ObjectMaterials { public string name;public string[] materials; }
 [Serializable] class MaterialManifest { public MaterialEntry[] materials;public ObjectMaterials[] objects; }
 static bool busy;
 static CampusUpgrade(){EditorApplication.update+=Tick;}
 static void Tick(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Tools/upgrade.request"))return;
   try{File.Delete("Tools/upgrade.request");}catch(IOException){return;}busy=true;EditorApplication.delayCall+=()=>{try{Upgrade();File.WriteAllText("Results/upgrade_status.txt","OK colores originales, jugador, clips y colisiones");}catch(Exception ex){Debug.LogException(ex);File.WriteAllText("Results/upgrade_status.txt",ex.ToString());}finally{busy=false;}};
 }
 public static void ApplyMaterials(GameObject building){
   var manifest=JsonUtility.FromJson<MaterialManifest>(Resources.Load<TextAsset>("original_materials").text);var colors=manifest.materials.ToDictionary(x=>x.name);var originalSlots=manifest.objects.ToDictionary(x=>x.name,x=>x.materials);
   var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Simulation/Building.fbx");
   var renderers=source.GetComponentsInChildren<Renderer>(true).ToDictionary(r=>r.name);
   var materials=new Dictionary<string,Material>();Directory.CreateDirectory("Assets/Simulation/OriginalMaterials");int corrected=0;
   AssetDatabase.StartAssetEditing();try {
     foreach(var r in building.GetComponentsInChildren<Renderer>(true)){
       if(!renderers.TryGetValue(r.name,out var original))continue;
       r.sharedMaterials=original.sharedMaterials.Select((m,slot)=>{
         string name=m&&colors.ContainsKey(m.name)?m.name:originalSlots.TryGetValue(r.name,out var slots)&&slot<slots.Length?slots[slot]:"Blanco";if(!colors.ContainsKey(name))name="Blanco";if(materials.TryGetValue(name,out var existing))return existing;
         int index=Array.FindIndex(manifest.materials,e=>e.name==name);
         string path="Assets/Simulation/OriginalMaterials/Original_"+index+".mat";
         var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
         var e=colors[name];mat.name=Path.GetFileNameWithoutExtension(path);var color=new Color(e.color[0],e.color[1],e.color[2],e.color[3]);
         mat.SetColor("_BaseColor",color.gamma);mat.SetFloat("_Metallic",e.metallic);mat.SetFloat("_Smoothness",1-e.roughness);
         EditorUtility.SetDirty(mat);materials[name]=mat;return mat;
       }).ToArray();corrected++;
     }
   }finally{AssetDatabase.StopAssetEditing();}
   ArchitecturalFinishes.Apply(building);
   File.WriteAllText("Documentation/material_recovery.txt",$"Renderers restaurados: {corrected}\nMateriales originales: {materials.Count}\nFuente: nodos Principled BSDF de Assets/info/Conjunto_Bloques_A_B_U.blend\nConversión: lineal Blender -> color sRGB Unity. Asignación por nombre y posición de material de cada renderer del FBX original. Acabados de pisos y techos ajustados según los renders de referencia.");
 }
 [MenuItem("Simulación/Lunes/3. Restaurar colores y crear jugador")]
 public static void Upgrade(){
   EditorSceneManager.OpenScene("Assets/Simulation/Lunes.unity");var sim=UnityEngine.Object.FindFirstObjectByType<MondaySimulation>();
   if(!sim)throw new Exception("No se encontró la simulación");ApplyMaterials(sim.building.gameObject);
   // Physical geometry is separate from the NavMesh used by the scheduled agents.
   int colliders=0;foreach(var filter in sim.building.GetComponentsInChildren<MeshFilter>(true)){
     if(!filter.gameObject.activeInHierarchy||!filter.sharedMesh||filter.sharedMesh.vertexCount<3)continue;
     var collider=filter.GetComponent<MeshCollider>();if(!collider)collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;colliders++;
   }
   var previous=UnityEngine.Object.FindFirstObjectByType<CampusPlayer>();if(previous)UnityEngine.Object.DestroyImmediate(previous.gameObject);
   Directory.CreateDirectory(Folder);var player=new GameObject("Jugador · tercera persona");player.layer=2;player.transform.position=sim.mainEntrance+Vector3.up*.1f;
   var visual=new GameObject("Visual");visual.transform.SetParent(player.transform,false);
   var rig=(GameObject)PrefabUtility.InstantiatePrefab(sim.characters[0]);rig.name="Rig";rig.transform.SetParent(visual.transform,false);
   // Detach the instance before authoring the additional generic animation controller.
   PrefabUtility.UnpackPrefabInstance(rig,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
   foreach(var a in rig.GetComponentsInChildren<Animator>())UnityEngine.Object.DestroyImmediate(a);
   var rs=rig.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
   rig.transform.localScale*=1.75f/bounds.size.y;bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
   rig.transform.position-=new Vector3(bounds.center.x-player.transform.position.x,bounds.min.y-player.transform.position.y,bounds.center.z-player.transform.position.z);
   foreach(var t in player.GetComponentsInChildren<Transform>())t.gameObject.layer=2;
   var clips=new Dictionary<string,AnimationClip>();
   foreach(var state in new[]{"Idle","Walk","Run","Jump","SitDown","Seated","StandUp"})clips[state]=Bake(visual.transform,state);
   // Baking is performed on a disposable clone so the saved player keeps the bind pose.
   var controllerPath=Folder+"CampusPlayer.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
   if(controller){foreach(var sub in AssetDatabase.LoadAllAssetsAtPath(controllerPath).Where(x=>x!=controller))UnityEngine.Object.DestroyImmediate(sub,true);controller.layers=Array.Empty<AnimatorControllerLayer>();controller.parameters=Array.Empty<AnimatorControllerParameter>();controller.AddLayer("Base Layer");}
   else controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
   controller.AddParameter("Speed",AnimatorControllerParameterType.Float);var machine=controller.layers[0].stateMachine;
   var locomotion=machine.AddState("Locomotion");var tree=new BlendTree{name="Idle / caminar / correr",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,controller);
   tree.AddChild(clips["Idle"],0);tree.AddChild(clips["Walk"],1.8f);tree.AddChild(clips["Run"],5.2f);locomotion.motion=tree;machine.defaultState=locomotion;
   foreach(var state in new[]{"Jump","SitDown","Seated","StandUp"})machine.AddState(state).motion=clips[state];
   var animator=visual.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
   var body=player.AddComponent<CharacterController>();body.height=1.75f;body.radius=.22f;body.center=Vector3.up*.89f;body.stepOffset=.31f;body.slopeLimit=50;body.skinWidth=.025f;body.minMoveDistance=0;
   var movement=player.AddComponent<CampusPlayer>();movement.visual=visual.transform;movement.animator=animator;movement.simulation=sim;sim.player=movement;
   var sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);if(sun){sun.intensity=1.1f;sun.shadows=LightShadows.Soft;}
   ImportedAnimationSetup.ApplyExisting(sim);
   EditorUtility.SetDirty(sim);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
   File.WriteAllText("Documentation/player_setup.txt",$"Jugador: Ch01 original\nAnimaciones nativas: {string.Join(", ",clips.Keys)}\nControlador Animator con BlendTree y transiciones CrossFade\nColisionadores: {colliders}\nWASD / Shift / Espacio / E / Tab / Escape / R\nLos clips se generaron para el esqueleto original; no son animaciones extraídas de GTA.");
   Debug.Log("CAMPUS_UPGRADE_OK");
 }
 static AnimationClip Bake(Transform visual,string state){
   float duration=state=="Idle"||state=="Seated"?3:state=="Walk"?1:state=="Run"?.65f:state=="Jump"?.7f:.6f;
   var clone=UnityEngine.Object.Instantiate(visual.gameObject);clone.transform.position=Vector3.zero;clone.transform.rotation=Quaternion.identity;
   var sampler=new PlayerPoseSampler(clone.transform);var transforms=clone.GetComponentsInChildren<Transform>();
   var curves=new Dictionary<(string,string),AnimationCurve>();
   for(int sample=0;sample<=Mathf.CeilToInt(duration*30);sample++){
     float t=Mathf.Min(duration,sample/30f);sampler.Sample(state,t,duration);
     foreach(var bone in transforms){if(bone==clone.transform)continue;string suffix=bone.name.ToLowerInvariant().Split(':').Last();
       if(bone!=clone.transform.GetChild(0)&&!new[]{"leftarm","rightarm","leftforearm","rightforearm","leftupleg","rightupleg","leftleg","rightleg","spine2"}.Contains(suffix))continue;
       string path=AnimationUtility.CalculateTransformPath(bone,clone.transform);var q=bone.localRotation;
       void Key(string property,float value){var key=(path,property);if(!curves.TryGetValue(key,out var curve)){curve=new AnimationCurve();curves[key]=curve;}curve.AddKey(t,value);}
       Key("m_LocalRotation.x",q.x);Key("m_LocalRotation.y",q.y);Key("m_LocalRotation.z",q.z);Key("m_LocalRotation.w",q.w);
       if(bone==clone.transform.GetChild(0))Key("m_LocalPosition.y",bone.localPosition.y);
     }
   }
   var pathClip=Folder+state+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(pathClip);if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,pathClip);}clip.ClearCurves();clip.name=state;clip.frameRate=30;
   foreach(var kv in curves){var curve=kv.Value;if(curve.keys.All(k=>Mathf.Abs(k.value-curve.keys[0].value)<.00001f))curve=AnimationCurve.Constant(0,duration,curve.keys[0].value);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(kv.Key.Item1,typeof(Transform),kv.Key.Item2),curve);}
   clip.EnsureQuaternionContinuity();var options=AnimationUtility.GetAnimationClipSettings(clip);options.loopTime=state=="Idle"||state=="Walk"||state=="Run"||state=="Seated";AnimationUtility.SetAnimationClipSettings(clip,options);
   EditorUtility.SetDirty(clip);UnityEngine.Object.DestroyImmediate(clone);return clip;
 }
}
