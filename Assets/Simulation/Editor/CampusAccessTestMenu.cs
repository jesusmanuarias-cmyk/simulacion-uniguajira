using CampusMonday;
using UnityEditor;
using UnityEngine;

static class CampusAccessTestMenu
{
    [MenuItem("Simulación/Universidad/Probar accesos por edificio", true)]
    static bool Enabled() => Application.isPlaying;

    [MenuItem("Simulación/Universidad/Probar accesos por edificio")]
    static void Run()
    {
        var campus = Object.FindFirstObjectByType<FullCampus>();
        if(campus) campus.TestBuildingAccess();
    }
    [MenuItem("Simulación/Universidad/Probar aforo máximo", true)]
    static bool LoadEnabled() => Application.isPlaying;

    [MenuItem("Simulación/Universidad/Probar aforo máximo")]
    static void RunLoad()
    {
        var campus = Object.FindAnyObjectByType<FullCampus>();
        if (campus) campus.TestFullCapacity();
    }
    [MenuItem("Simulación/Universidad/Probar Administrativo Principal", true)]
    static bool AdministrationEnabled() => Application.isPlaying;

    [MenuItem("Simulación/Universidad/Probar Administrativo Principal")]
    static void RunAdministration()
    {
        var campus = Object.FindAnyObjectByType<FullCampus>();
        if (campus) campus.TestAdministrationAccess();
    }

    const string FocusedMenu = "Simulación/Universidad/Probar Administrativo Principal y Biblioteca";

    [MenuItem(FocusedMenu, true)]
    static bool FocusedEnabled() => Application.isPlaying;

    [MenuItem(FocusedMenu)]
    static void RunFocused()
    {
        var campus = Object.FindAnyObjectByType<FullCampus>();
        if (campus) campus.TestAdministrationAndLibrary();
    }
}

