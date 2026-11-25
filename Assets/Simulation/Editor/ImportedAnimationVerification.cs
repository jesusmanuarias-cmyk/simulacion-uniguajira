using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ImportedAnimationVerification {
 static bool busy;
 static ImportedAnimationVerification(){EditorApplication.update+=Tick;}
 static void Tick(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Tools/verify_animations.request"))return;
   try{File.Delete("Tools/verify_animations.request");}catch(IOException){return;}busy=true;
   try{Verify();}catch(Exception e){File.WriteAllText("Results/animation_verification.txt",e.ToString());Debug.LogException(e);}finally{busy=false;}
 }
 static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
 [MenuItem("Simulación/Lunes/6. Verificar animaciones importadas")]
 public static void Verify(){
   var report=new List<string>();var ci=System.Globalization.CultureInfo.InvariantCulture;
   foreach(string name in new[]{"Ch01_nonPBR","Ch22_nonPBR","Ch33_nonPBR"}){
     var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Simulation/Generated/"+name+".prefab"));
     try{go.transform.position=new Vector3(200,0,200);var animator=go.GetComponent<Animator>();Require(animator.avatar.isHuman&&animator.avatar.isValid,"Avatar inválido "+name);
       animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;animator.speed=0;animator.Rebind();animator.Update(0);
       float idleHeight=0,seatedHeight=0;var start=go.transform.position;
       foreach(string state in new[]{"Idle","Walk","Run","Jump","Seated"}){
         var clip=animator.runtimeAnimatorController.animationClips.First(c=>c.name==state);Require(clip.humanMotion&&clip.length>0,"Clip inválido "+state);
         animator.SetFloat("MotionTime",.2f);animator.Play(state,0,.2f);animator.Update(0);
         var hip=animator.GetBoneTransform(HumanBodyBones.Hips);var knee=animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);var kneeFirst=knee.position;
         float hipHeight=hip.position.y-go.transform.position.y;
         if(state=="Idle")idleHeight=hipHeight;if(state=="Seated")seatedHeight=hipHeight;
         animator.SetFloat("MotionTime",.7f);animator.Update(0);float motion=Vector3.Distance(kneeFirst,knee.position);
         Require(animator.GetCurrentAnimatorStateInfo(0).IsName(state),"Estado no reproducido "+name+"/"+state);
         Require(Vector3.Distance(start,go.transform.position)<.001f,"Root motion desplaza la raíz "+name+"/"+state);
         if(state=="Walk"||state=="Run")Require(motion>.025f,"Las piernas no se animan "+name+"/"+state);
         report.Add($"{name}/{state}: {clip.length.ToString("0.000",ci)} s; cadera={hipHeight.ToString("0.000",ci)} m; movimiento rodilla={motion.ToString("0.000",ci)} m; raíz estable");
       }
       Require(seatedHeight<idleHeight-.15f,"El clip sentado no baja la cadera "+name);
     }finally{UnityEngine.Object.DestroyImmediate(go);}
   }
   report.Insert(0,"OK: 15 combinaciones de personaje y animación verificadas, humanoides válidos, movimiento de piernas, pose sentada y raíz estable.");File.WriteAllLines("Results/animation_verification.txt",report);Debug.Log("IMPORTED_ANIMATION_VERIFICATION_OK");
 }
}
