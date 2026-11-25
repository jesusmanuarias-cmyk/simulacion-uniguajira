using System.IO;
using System.Linq;
using CampusMonday;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class FullCampusBuilder {
    [InitializeOnLoadMethod]
    static void ConfigurePlayScene(){
        var scene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Simulation/Universidad.unity");
        if(scene)EditorSceneManager.playModeStartScene=scene;
    }
    [MenuItem("Simulación/Universidad/Construir campus completo")]
    public static void Build(){
        CampusEditPreview.Clear();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
        var sim=new GameObject("Universidad · capacidad física").AddComponent<FullCampus>();
        sim.campusShader=Shader.Find("Campus/Color del modelo");
        sim.animations=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Simulation/ImportedAnimations/NPCImported.controller");
        sim.playerAnimations=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Simulation/ImportedAnimations/PlayerImported.controller");
        sim.characters=new[]{"Ch01","Ch22","Ch33"}.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Simulation/Generated/"+n+"_nonPBR.prefab")).ToArray();
        sim.LoadMap();var nav=sim.Bake();if(!nav)throw new System.Exception("Falló la generación de NavMesh del campus");
        const string path="Assets/Simulation/Generated/FullCampusNav.asset";
        var previous=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if(previous){EditorUtility.CopySerialized(nav,previous);sim.baked=previous;}else{AssetDatabase.CreateAsset(nav,path);sim.baked=nav;}
        var handle=NavMesh.AddNavMeshData(sim.baked);
        sim.LoadThresholdLinks();
        File.WriteAllText("Documentation/campus_validation.txt",sim.Audit());sim.RemoveThresholdLinks();handle.Remove();
        while(sim.transform.childCount>0)Object.DestroyImmediate(sim.transform.GetChild(0).gameObject);
        sim.inventory=null;
        const string campus="Assets/Simulation/Universidad.unity";
        EditorSceneManager.SaveScene(scene,campus);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(campus,true)};
        AssetDatabase.SaveAssets();Debug.Log("CAMPUS_BUILD_OK");
    }
}







