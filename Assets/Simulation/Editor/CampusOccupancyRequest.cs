using CampusMonday;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class CampusOccupancyRequest
{
    const string Request = "Tools/campus_routes_check.request";
    static CampusOccupancyRequest() => EditorApplication.update += Poll;
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = true;
            return;
        }
        if (!Application.isPlaying) return;
        var campus = Object.FindAnyObjectByType<FullCampus>();
        if (!campus || campus.inventory == null || !campus.GetComponent<MondayDashboard>()) return;
        File.Delete(Request);
        campus.TestCampusRoutes();
    }
}

