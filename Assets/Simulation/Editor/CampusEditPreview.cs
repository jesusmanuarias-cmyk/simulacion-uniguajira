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
    static NavMeshData previewData;
    static bool showingNavigation;
    static bool framePending;
    static CampusEditPreview(){
        EditorSceneManager.activeSceneChangedInEditMode+=(_,__)=>Schedule();
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.ExitingEditMode)Clear();
            if(state==PlayModeStateChange.EnteredEditMode)Schedule();
            if(state==PlayModeStateChange.EnteredPlayMode)ApplyNavigationVisibility();
        };
        AssemblyReloadEvents.beforeAssemblyReload+=Clear;
        Schedule();
    }
    [MenuItem("Simulación/Universidad/Recargar vista del campus")]
    internal static void Schedule(){
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
            previewData=source.baked;
            ApplyNavigationVisibility();
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

    const string NavigationMenu = "Simulación/Universidad/Mostrar NavMesh";
    const string NavigationPreference = "Campus.ShowNavigation";

    [MenuItem(NavigationMenu)]
    static void ToggleNavigation()
    {
        SessionState.SetBool(NavigationPreference,!SessionState.GetBool(NavigationPreference,false));
        ApplyNavigationVisibility();
    }

    [MenuItem(NavigationMenu,true)]
    static bool ValidateNavigation()
    {
        Menu.SetChecked(NavigationMenu,SessionState.GetBool(NavigationPreference,false));
        return true;
    }

    static void ApplyNavigationVisibility()
    {
        bool requested=SessionState.GetBool(NavigationPreference,false);
        if(preview && previewData)
        {
            var map=preview.GetComponent<FullCampus>();
            if(requested && !previewNavigation.valid)
            {
                previewNavigation=NavMesh.AddNavMeshData(previewData);
                map.LoadThresholdLinks();
            }
            else if(!requested && previewNavigation.valid)
            {
                map.RemoveThresholdLinks();
                previewNavigation.Remove();
            }
        }
        NavMeshVisualizationSettings.showNavigation=requested?1:0;
        showingNavigation=requested;
        SceneView.RepaintAll();
    }

    internal static void Clear(){
        if(previewNavigation.valid)previewNavigation.Remove();
        previewData=null;
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

sealed class CampusPreviewAssets : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (importedAssets.Concat(deletedAssets).Concat(movedAssets).Concat(movedFromAssetPaths)
            .Any(path => path.StartsWith("Assets/StreamingAssets/Campus/", System.StringComparison.Ordinal) ||
                         path == "Assets/Simulation/Generated/FullCampusNav.asset"))
        {
            CampusEditPreview.Schedule();
        }
    }
}








