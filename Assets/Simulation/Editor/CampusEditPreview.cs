using System.Linq;
using CampusMonday;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AI;
using UnityEngine.AI;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class CampusEditPreview {
    static GameObject preview;
    static NavMeshDataInstance previewNavigation;
    static bool showingNavigation;
    static bool framePending;
    static CampusEditPreview(){
        EditorSceneManager.activeSceneChangedInEditMode+=(_,__)=>Schedule();
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.ExitingEditMode)Clear();
            if(state==PlayModeStateChange.EnteredEditMode)Schedule();
            if(state==PlayModeStateChange.EnteredPlayMode)ShowNavigation();
        };
        AssemblyReloadEvents.beforeAssemblyReload+=Clear;
        Schedule();
    }
    static void Schedule(){
        EditorApplication.delayCall-=Refresh;
        EditorApplication.delayCall+=Refresh;
    }

    static void Refresh(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Clear();
        var scene=SceneManager.GetActiveScene();
        var source=scene.GetRootGameObjects().Select(root=>root.GetComponent<FullCampus>()).FirstOrDefault(sim=>sim);
        if(!source||!source.campusShader)return;
        preview=new GameObject("Vista previa del campus"){hideFlags=HideFlags.HideAndDontSave};
        var map=preview.AddComponent<FullCampus>();map.campusShader=source.campusShader;
        try{
            map.LoadMap(true);
            if(source.baked) previewNavigation=NavMesh.AddNavMeshData(source.baked);
            map.LoadThresholdLinks();
            ShowNavigation();
            foreach(var child in preview.GetComponentsInChildren<Transform>())child.gameObject.hideFlags=HideFlags.HideAndDontSave;
            var overview=new GameObject("Cámara de vista previa"){hideFlags=HideFlags.HideAndDontSave};
            overview.transform.SetParent(preview.transform);
            var camera=overview.AddComponent<Camera>();
            if(Camera.main)camera.CopyFrom(Camera.main);
            camera.depth=100;camera.farClipPlane=2200;
            camera.transform.rotation=Quaternion.Euler(60,0,0);
            camera.transform.position=map.bounds.center-camera.transform.forward*720;
            if(!framePending&&SceneView.lastActiveSceneView){
                SceneView.lastActiveSceneView.Frame(map.bounds,false);
                framePending=true;
            }
            SceneView.RepaintAll();
        }catch(System.Exception error){Clear();Debug.LogException(error);}
    }

    static void ShowNavigation(){
        if(showingNavigation)return;
        NavMeshVisualizationSettings.showNavigation++;
        showingNavigation=true;
        SceneView.RepaintAll();
    }

    internal static void Clear(){
        if(previewNavigation.valid)previewNavigation.Remove();
        if(showingNavigation){
            NavMeshVisualizationSettings.showNavigation--;
            showingNavigation=false;
        }
        if(!preview)return;
        preview.GetComponent<FullCampus>()?.RemoveThresholdLinks();
        foreach(var filter in preview.GetComponentsInChildren<MeshFilter>())Object.DestroyImmediate(filter.sharedMesh);
        var renderer=preview.GetComponentInChildren<MeshRenderer>();
        if(renderer)Object.DestroyImmediate(renderer.sharedMaterial);
        Object.DestroyImmediate(preview);
    }
}








