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
public static class ImportedAnimationSetup {
 public const string Folder="Assets/Simulation/ImportedAnimations/";
 static bool busy;
 static ImportedAnimationSetup(){EditorApplication.update+=Tick;}
 static void Tick(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Tools/import_animations.request"))return;
   try{File.Delete("Tools/import_animations.request");}catch(IOException){return;}busy=true;EditorApplication.delayCall+=()=>{try{Install();File.WriteAllText("Results/imported_animations_status.txt","OK: cinco clips FBX, tres Avatars humanoides, jugador y NPC");}catch(Exception e){File.WriteAllText("Results/imported_animations_status.txt",e.ToString());Debug.LogException(e);}finally{busy=false;}};
 }
 static string Source(string word){var found=Directory.GetFiles("Assets/info","*.fbx").Where(p=>Path.GetFileName(p).IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0).ToArray();if(found.Length!=1)throw new Exception("Se requiere un FBX completo para "+word+": "+string.Join(",",found));return found[0].Replace('\\','/');}
 static void HumanRig(string path,string clipName=null){
   var importer=(ModelImporter)AssetImporter.GetAtPath(path);if(!importer)throw new Exception("FBX todavía no importado: "+path);
   var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var bones=model.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name.ToLowerInvariant().Split(':').Last()).ToDictionary(g=>g.Key,g=>g.First().name);
   var map=new Dictionary<string,string>{{"Hips","hips"},{"Spine","spine"},{"Chest","spine1"},{"UpperChest","spine2"},{"Neck","neck"},{"Head","head"}};
   foreach(var side in new[]{"Left","Right"}){
     string s=side.ToLowerInvariant();foreach(var pair in new[]{("Shoulder","shoulder"),("UpperArm","arm"),("LowerArm","forearm"),("Hand","hand"),("UpperLeg","upleg"),("LowerLeg","leg"),("Foot","foot"),("Toes","toebase")})map[side+pair.Item1]=s+pair.Item2;
     foreach(var finger in new[]{"Thumb","Index","Middle","Ring","Little"})for(int j=0;j<3;j++)map[side+finger+new[]{"Proximal","Intermediate","Distal"}[j]]=s+"hand"+(finger=="Little"?"pinky":finger.ToLowerInvariant())+(j+1);
   }
   var human=new List<HumanBone>();foreach(var name in HumanTrait.BoneName){string key=name.Replace(" ","");if(map.TryGetValue(key,out var suffix)&&bones.TryGetValue(suffix,out var bone))human.Add(new HumanBone{humanName=name,boneName=bone,limit=new HumanLimit{useDefaultValues=true}});}
   var description=importer.humanDescription;description.human=human.ToArray();description.armStretch=.05f;description.legStretch=.05f;description.upperArmTwist=.5f;description.lowerArmTwist=.5f;description.upperLegTwist=.5f;description.lowerLegTwist=.5f;
   importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.humanDescription=description;importer.optimizeGameObjects=false;importer.isReadable=true;
   if(clipName!=null){importer.importAnimation=true;var definitions=importer.defaultClipAnimations;if(definitions.Length==0)throw new Exception("Sin pistas de animación en "+path);
     var clip=definitions[0];clip.name=clipName;clip.loopTime=clipName!="Jump";clip.loopPose=clipName!="Jump";clip.lockRootRotation=true;clip.keepOriginalOrientation=true;
     clip.lockRootPositionXZ=false;clip.keepOriginalPositionXZ=false; // Remove authored forward translation; controller/routes own movement.
     clip.lockRootHeightY=true;clip.keepOriginalPositionY=true;clip.heightFromFeet=false;
     importer.clipAnimations=new[]{clip};
   }
   importer.SaveAndReimport();var avatar=AvatarAt(path);if(!avatar.isValid||!avatar.isHuman)throw new Exception("Avatar inválido después de importar "+path);
 }
 static Avatar AvatarAt(string path){var a=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();if(!a)throw new Exception("Falta Avatar "+path);return a;}
 static AnimatorController Controller(string name,Dictionary<string,AnimationClip> clips,bool npc){
   string path=Folder+name+".controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
   if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
   var machine=controller.layers[0].stateMachine;foreach(var item in machine.states.ToArray())machine.RemoveState(item.state);foreach(var transition in machine.anyStateTransitions.ToArray())machine.RemoveAnyStateTransition(transition);
   controller.parameters=Array.Empty<AnimatorControllerParameter>();
   if(npc){controller.AddParameter("MotionTime",AnimatorControllerParameterType.Float);foreach(var kv in clips){var state=machine.AddState(kv.Key);state.motion=kv.Value;state.timeParameter="MotionTime";state.timeParameterActive=true;if(kv.Key=="Idle")machine.defaultState=state;}}
   else {controller.AddParameter("Speed",AnimatorControllerParameterType.Float);var state=machine.AddState("Locomotion");var blend=new BlendTree{name="Idle Walk Run FBX",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(blend,controller);blend.AddChild(clips["Idle"],0);blend.AddChild(clips["Walk"],1.8f);blend.AddChild(clips["Run"],5.2f);state.motion=blend;machine.defaultState=state;
     machine.AddState("Jump").motion=clips["Jump"];machine.AddState("Seated").motion=clips["Seated"];
   }EditorUtility.SetDirty(controller);return controller;
 }
 public static bool Available=>AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Folder+"PlayerImported.controller")!=null;
 public static void ApplyExisting(MondaySimulation sim){
   if(!Available)return;sim.npcAnimations=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Folder+"NPCImported.controller");
   if(sim.player){var player=sim.player;var visual=player.visual;var rig=visual.GetChild(0);var legacy=visual.GetComponent<Animator>();if(legacy)UnityEngine.Object.DestroyImmediate(legacy);
     var animator=rig.GetComponent<Animator>();if(!animator)animator=rig.gameObject.AddComponent<Animator>();animator.enabled=true;animator.avatar=AvatarAt("Assets/info/Ch01_nonPBR.fbx");animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Folder+"PlayerImported.controller");animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;player.animator=animator;player.animationState="Locomotion";EditorUtility.SetDirty(player);
   }EditorUtility.SetDirty(sim);
 }
 [MenuItem("Simulación/Lunes/5. Usar animaciones FBX de info")]
 public static void Install(){
   Directory.CreateDirectory(Folder);var sources=new Dictionary<string,string>{{"Idle",Source("@Idle")},{"Walk",Source("@Walking")},{"Run",Source("@Running")},{"Jump",Source("@Jumping")},{"Seated",Source("Seated")}};
   var evidence=new List<string>();foreach(var character in new[]{"Ch01_nonPBR","Ch22_nonPBR","Ch33_nonPBR"}){string path="Assets/info/"+character+".fbx";HumanRig(path);evidence.Add("Avatar válido: "+path);}
   var clips=new Dictionary<string,AnimationClip>();foreach(var kv in sources){HumanRig(kv.Value,kv.Key);var clip=AssetDatabase.LoadAllAssetsAtPath(kv.Value).OfType<AnimationClip>().FirstOrDefault(c=>c.name==kv.Key);if(!clip||!clip.humanMotion||clip.length<=0)throw new Exception("Clip humanoide inválido "+kv.Value);clips[kv.Key]=clip;evidence.Add($"{kv.Key}: {kv.Value}; {clip.length:0.000} s; humanoide={clip.humanMotion}");}
   Controller("PlayerImported",clips,false);Controller("NPCImported",clips,true);
   foreach(var character in new[]{"Ch01_nonPBR","Ch22_nonPBR","Ch33_nonPBR"}){
     string path="Assets/Simulation/Generated/"+character+".prefab";var prefab=PrefabUtility.LoadPrefabContents(path);try{var animator=prefab.GetComponent<Animator>();if(!animator)animator=prefab.AddComponent<Animator>();animator.avatar=AvatarAt("Assets/info/"+character+".fbx");animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Folder+"NPCImported.controller");animator.applyRootMotion=false;animator.enabled=true;PrefabUtility.SaveAsPrefabAsset(prefab,path);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
   }
   var sim=UnityEngine.Object.FindFirstObjectByType<MondaySimulation>();if(!sim){EditorSceneManager.OpenScene("Assets/Simulation/Lunes.unity");sim=UnityEngine.Object.FindFirstObjectByType<MondaySimulation>();}ApplyExisting(sim);
   AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
   File.WriteAllLines("Documentation/animaciones_importadas.txt",evidence.Concat(new[]{"Jugador: BlendTree Idle/Walk/Run, Jump y Seated; transiciones CrossFade.","NPC: muestreo de clips humanoides según la fase y el reloj de simulación. No se modifican trayectos ni horarios.","Desplazamiento raíz desactivado: el movimiento lo controla CharacterController o la ruta de cada NPC."}));Debug.Log("IMPORTED_ANIMATIONS_OK");
 }
}
