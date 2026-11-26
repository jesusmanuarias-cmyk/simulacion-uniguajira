using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace CampusMonday
{
    public partial class FullCampus
    {
        // Exterior ground points checked against the placed Blender campus.
        static readonly (string Building, Vector3[] Points)[] AccessPoints =
        {
            ("Admisiones", new[]{new Vector3(86.000f,1.208f,76.000f),new Vector3(65.000f,1.472f,76.000f),new Vector3(80.000f,1.158f,55.000f)}),
            ("Bloque Administrativo A · oficinas en dos pisos", new[]{new Vector3(25.727f,-0.278f,212.067f),new Vector3(39.171f,-0.200f,252.718f),new Vector3(2.071f,-0.204f,217.030f)}),
            ("Bloque Administrativo B", new[]{new Vector3(20.899f,-0.169f,162.244f),new Vector3(35.000f,-0.122f,173.950f),new Vector3(-4.000f,-0.183f,182.000f)}),
            ("Bloque Administrativo Principal", new[]{new Vector3(129.367f,0.616f,245.092f),new Vector3(77.029f,-0.163f,227.235f),new Vector3(64.000f,0.557f,251.000f)}),
            ("Bloque I", new[]{new Vector3(-16.000f,-0.165f,183.000f),new Vector3(-37.000f,-0.065f,166.000f),new Vector3(-49.629f,-0.065f,196.160f)}),
            ("Bloque II", new[]{new Vector3(4.038f,0.153f,110.453f),new Vector3(21.161f,-0.285f,135.965f)}),
            ("Bloque III", new[]{new Vector3(-26.223f,-0.298f,128.843f),new Vector3(-38.000f,-0.259f,91.000f),new Vector3(-11.000f,-0.331f,111.000f)}),
            ("Bloque V", new[]{new Vector3(-59.371f,-0.220f,80.456f),new Vector3(-19.921f,-0.293f,77.191f),new Vector3(-39.569f,-0.235f,38.148f)}),
            ("Bloque VI", new[]{new Vector3(-73.000f,-0.038f,13.000f),new Vector3(-94.000f,-0.131f,40.000f),new Vector3(-120.000f,-0.052f,24.000f)}),
            ("Bloque X", new[]{new Vector3(-131.000f,-0.128f,125.000f),new Vector3(-164.000f,-0.668f,170.000f),new Vector3(-150.335f,-0.282f,122.056f)}),
            ("Bloque X.001", new[]{new Vector3(-92.000f,-0.560f,263.000f),new Vector3(-105.000f,0.268f,210.000f)})
        };

        bool checkingLocalAccess;
        Trip accessTestTrip;

        public void TestBuildingAccess()
        {
            if (!isActiveAndEnabled || running || checkingLocalAccess) return;
            StartCoroutine(ObserveBuildingAccess());
        }

        IEnumerator ObserveBuildingAccess()
        {
            checkingLocalAccess = true;
            SetPlayer(false);
            SetFloor(0);
            string file = Path.Combine(Application.isEditor ? "Documentation" : Application.persistentDataPath,
                "campus_prueba_accesos_locales.csv");
            using var report = new StreamWriter(file, false, new UTF8Encoding(true));
            report.WriteLine("edificio,piso,estado,puesto,spawn_x,spawn_y,spawn_z,metros,cota_maxima,animacion_sentado");
            foreach (var (building, points) in AccessPoints)
            {
                var seats = inventory.seats.Where(s => s.building == building).OrderBy(s => s.position.y).ToArray();
                if (seats.Length == 0) continue;
                var floors = new List<List<Seat>>();
                foreach (var seat in seats)
                {
                    if (floors.Count == 0 || seat.position.y - floors.Last()[0].position.y > 1.65f)
                        floors.Add(new List<Seat>());
                    floors.Last().Add(seat);
                }

                message = "Probando acceso local · " + building;
                var tests = new Trip[floors.Count];
                var origin = points[0];
                int best = -1;
                foreach (var point in points)
                {
                    if (!NavMesh.SamplePosition(point, out var start, .38f, NavMesh.AllAreas)) continue;
                    var planned = new Trip[floors.Count];
                    for (int i = 0; i < floors.Count; i++)
                    {
                        foreach (var seat in floors[i].OrderBy(s => (s.position-start.position).sqrMagnitude).Take(28))
                        {
                            planned[i] = PlanLocalTrip(seat, start.position);
                            if (planned[i] != null) break;
                        }
                        yield return null;
                    }
                    int count = planned.Count(t => t != null);
                    if (count <= best) continue;
                    best = count;
                    origin = start.position;
                    tests = planned;
                }

                focus = origin;
                zoom = zoomTarget = 42;
                MoveCamera();
                for (int i = 0; i < tests.Length; i++)
                {
                    var trip = tests[i];
                    string status = best < 0 ? "sin punto de aparición" : "sin ruta en la muestra";
                    float meters = 0, highest = origin.y;
                    bool seated = false;
                    if (trip != null)
                    {
                        accessTestTrip = trip;
                        Spawn(trip, origin);
                        trip.agent.speed = 5;
                        trip.agent.acceleration = 16;
                        var target = trip.route.Last();
                        var previous = origin;
                        float timeout = Time.realtimeSinceStartup + 75;
                        status = "agente no llegó";
                        while (Time.realtimeSinceStartup < timeout)
                        {
                            yield return null;
                            var position = trip.agent.transform.position;
                            meters += Vector3.Distance(previous,position);
                            highest = Mathf.Max(highest,position.y);
                            previous = position;
                            Visual(trip,position);
                            if (!trip.agent.pathPending && trip.agent.pathStatus != NavMeshPathStatus.PathComplete)
                                break;
                            if (Vector3.Distance(position,target) > .22f) continue;
                            trip.phase = 2;
                            trip.agent.enabled = false;
                            trip.sat = elapsed - 61;
                            focus = trip.seat.position;
                            MoveCamera();
                            for (int frame = 0; frame < 8; frame++)
                            {
                                Visual(trip,trip.seat.position);
                                yield return null;
                            }
                            seated = trip.animator && trip.animator.GetCurrentAnimatorStateInfo(0).IsName("Seated");
                            status = "llegó al puesto";
                            yield return new WaitForSecondsRealtime(1.5f);
                            break;
                        }
                        Destroy(trip.agent.gameObject);
                        if (trip.visual) Destroy(trip.visual);
                        accessTestTrip = null;
                    }
                    report.WriteLine(FormattableString.Invariant($"{Q(building)},{i+1},{Q(status)},{Q(trip?.seat.name ?? "")},{origin.x:F3},{origin.y:F3},{origin.z:F3},{meters:F2},{highest:F2},{(seated ? "sí" : "no")}"));
                    report.Flush();
                }
            }
            checkingLocalAccess = false;
            message = "Prueba local guardada en " + file;
            Debug.Log("CAMPUS_ACCESOS_LOCALES_OK " + file);
        }

        static readonly float[] SeatProbeRadii = { .50f, .72f, .28f };

        static readonly float[] DiningSeatProbeRadii = { .50f, .72f, .28f, 1.10f, 1.35f };

        static IEnumerable<Vector3> SeatApproaches(Seat seat)
        {
            if(seat.building==ParkingShelters)
            {
                var front=seat.position+seat.forward*(seat.name.StartsWith("Fila 2")?.53f:.73f);
                if(NavMesh.SamplePosition(front,out var approach,.32f,NavMesh.AllAreas) &&
                   Vector3.Dot(approach.position-seat.position,seat.forward)>.33f)
                    yield return approach.position;
                yield break;
            }
            var tested = new HashSet<Vector3>();
            bool dining=seat.role=="Comensal";
            float maxDistance=dining?1.45f:1.05f;
            foreach(var radius in dining?DiningSeatProbeRadii:SeatProbeRadii)
            for (int i = 0; i < 8; i++)
            {
                var probe = seat.position + Quaternion.Euler(0, i * 45, 0) * Vector3.forward * radius;
                if (NavMesh.SamplePosition(probe,out var hit,.5f,NavMesh.AllAreas) &&
                    Mathf.Abs(hit.position.y-seat.position.y) <= .55f &&
                    Vector3.ProjectOnPlane(hit.position-seat.position,Vector3.up).sqrMagnitude <= maxDistance*maxDistance &&
                    tested.Add(hit.position))
                    yield return hit.position;
            }
        }

        static Trip PlanLocalTrip(Seat seat, Vector3 origin)
        {
            var path = new NavMeshPath();
            foreach(var approach in SeatApproaches(seat))
                if (CompletePath(origin,approach,path))
                    return new Trip { seat = seat, route = path.corners };
            return null;
        }
    }
}
