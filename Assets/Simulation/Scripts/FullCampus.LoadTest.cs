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
        enum LoadState { Waiting, Walking, Arrived, Partial, Stalled, NoApproach, NoRoute, NoSpawn, TimedOut }

        sealed class LoadPerson
        {
            public Seat Seat;
            public Trip Display;
            public Vector3 Origin, Target, Position, ProgressPoint, Approach;
            public bool HasApproach;
            public NavMeshAgent Agent;
            public LoadState State;
            public bool CompletePlan;
            public float LastProgress, ReleasedAt;
            public float Distance;
        }

        readonly List<LoadPerson> loadPeople = new List<LoadPerson>();
        const string MainAdministration = "Bloque Administrativo Principal";
        const string LibraryBuilding = "Biblioteca · edificio";
        public void TestAdministrationAndLibrary()
        {
            if (running || checkingLocalAccess) return;
            StartCoroutine(ObserveFullCapacity(null, true));
        }

        static readonly Vector3[] LibraryOrigins =
        {
            new(46,-.27f,205), new(45,-.27f,201), new(42,-.27f,185),
            new(68,-.27f,174), new(56,-.27f,178), new(75,-.27f,211)
        };

        static Seat[] LibraryDestinations() => new[]
        {
            LibraryPoint("Piso 1 · circulación", new(67.5f,-.25f,208.1f)),
            LibraryPoint("Piso 1 · acceso a escalera", new(58.8f,-.25f,183.1f)),
            LibraryPoint("Piso 2 · descanso helicoidal", new(68.9f,3.32f,208.72f)),
            LibraryPoint("Piso 2 · circulación", new(58.6f,3.32f,182.4f)),
            LibraryPoint("Piso 3 · circulación", new(58.84f,7.13f,182.93f))
        };

        static readonly (string Building, Vector3 Origin, Vector3[] Targets)[] VisitorBuildings =
        {
            ("Bloque IV", new(-77,0,130), new Vector3[]{new(-78,0,103),new(-60,3.1f,91),new(-60,6.2f,91)}),
            ("Bienestar Universitario", new(59,0,174), new Vector3[]{new(58,.4f,159),new(56,.4f,166)}),
            ("Casa Verde", new(-45,0,151), new Vector3[]{new(-44,.4f,146),new(-43,.4f,148)}),
            ("Centro Recreacional y Comedor", new(-318,0,-172), new Vector3[]{new(-306,.02f,-197),new(-334,.02f,-198)}),
            ("SILAB y Herbarios", new(120,.3f,-65), new Vector3[]{new(100,.34f,-85),new(119,.34f,-104),new(139,.94f,-88)}),
            ("Estadio, canchas y coliseo", new(-94,.2f,-166), new Vector3[]{new(-109,.2f,-186),new(-66,.2f,-92)}),
            ("Recursos Físicos", new(81,.5f,115), new Vector3[]{new(81,.49f,107),new(81,3.69f,101)}),
            ("Anexo de Recursos Físicos", new(77,.8f,125), new Vector3[]{new(77,.78f,120),new(78,.78f,118)})
        };

        public List<string> BuildingTests() => inventory.seats.Select(s => s.building)
            .Concat(VisitorBuildings.Select(entry => entry.Building)).Append(LibraryBuilding)
            .Distinct().OrderBy(name => name, StringComparer.CurrentCulture).ToList();

        public void TestOneBuilding(string name)
        {
            if (checkingLocalAccess || string.IsNullOrEmpty(name) || !BuildingTests().Contains(name)) return;
            StartCoroutine(ObserveFullCapacity(name, false, true));
        }

        Seat[] BuildingDestinations(string building)
        {
            if (building == LibraryBuilding) return LibraryDestinations();
            var selection = inventory.seats.Where(seat => seat.building == building)
                .OrderBy(seat => seat.position.y).ToArray();
            if (selection.Length > 0) return selection;
            var targets = VisitorBuildings.FirstOrDefault(entry => entry.Building == building).Targets;
            return targets?.Select((target, index) =>
                VisitPoint(building, $"Punto de visita {index + 1}", target)).ToArray() ?? Array.Empty<Seat>();
        }

        static Seat LibraryPoint(string label, Vector3 p) => VisitPoint(LibraryBuilding,label,p);

        static Seat VisitPoint(string building, string label, Vector3 p) => new Seat
        {
            building = building, role = "Visitante de prueba", room = label, name = label,
            position = p, forward = Vector3.forward
        };

        public void TestAdministrationAccess() => TestFullCapacity(MainAdministration);

        public void TestFullCapacity(string buildingFilter = null)
        {
            if (running || checkingLocalAccess) return;
            StartCoroutine(ObserveFullCapacity(buildingFilter));
        }

        public void TestCampusRoutes()
        {
            if (running || checkingLocalAccess) return;
            StartCoroutine(ObserveFullCapacity(null, reportRoutes:true));
        }

        IEnumerator ObserveFullCapacity(string buildingFilter, bool focused = false, bool singleBuilding = false, bool reportRoutes = false)
        {
            Restart();
            checkingLocalAccess = true;
            SetPlayer(false);
            SetFloor(0);

            var selection = reportRoutes ? BuildingTests().SelectMany(BuildingDestinations).ToArray() : singleBuilding ? BuildingDestinations(buildingFilter) : focused
                ? inventory.seats.Where(seat => seat.building == MainAdministration).Concat(LibraryDestinations()).ToArray()
                : inventory.seats.Where(seat => buildingFilter == null || seat.building == buildingFilter).ToArray();
            var queues = new List<Queue<LoadPerson>>();
            bool followFirst = buildingFilter != null || focused;
            foreach (var building in selection.GroupBy(seat => seat.building))
            {
                var points = BuildingOrigins(building.Key);
                bool hasOrigin = PickLoadOrigin(points, building, out var origin);
                if (!hasOrigin && NavMesh.SamplePosition(entrance, out var gate, 1f, NavMesh.AllAreas))
                {
                    hasOrigin = true;
                    origin = gate.position;
                }
                if (hasOrigin && followFirst)
                {
                    focus = origin;
                    zoom = 58;
                    MoveCamera();
                    followFirst = false;
                }
                var queue = new Queue<LoadPerson>();
                var inspectionLanes = hasOrigin ? LoadLanes(origin) : Array.Empty<Vector3>();
                int idleIndex = 0;
                foreach (var seat in building)
                {
                    var person = new LoadPerson { Seat = seat, Origin = origin, Position = origin, Target = origin };
                    loadPeople.Add(person);
                    if (hasOrigin) PlanLoadPerson(person);
                    else person.State = LoadState.NoSpawn;
                    if (person.State == LoadState.Waiting) queue.Enqueue(person);
                    else if (hasOrigin && singleBuilding)
                    {
                        person.Position = inspectionLanes[idleIndex++ % inspectionLanes.Length];
                        person.Display = new Trip { seat = seat };
                    }
                    if (loadPeople.Count % 24 == 0)
                    {
                        message = $"Asignando puestos · {loadPeople.Count}/{selection.Length}";
                        yield return null;
                    }
                }
                queues.Add(queue);
            }

            var active = new List<LoadPerson>();
            float nextReport = 0;
            var laneStarts = queues.Select(q => q.Count == 0 ? Array.Empty<Vector3>() : LoadLanes(q.Peek().Origin)).ToArray();
            var releaseAt = laneStarts.Select(lanes => new float[lanes.Length]).ToArray();

            do
            {
                float now = Time.time;
                for (int b = 0; b < queues.Count; b++)
                {
                    for (int lane = 0; lane < laneStarts[b].Length && queues[b].Count > 0 && active.Count < 384; lane++)
                    {
                        if (now < releaseAt[b][lane]) continue;
                        var person = queues[b].Dequeue();
                        ReleaseLoadPerson(person, laneStarts[b][lane], now);
                        if (person.State == LoadState.Walking) active.Add(person);
                        releaseAt[b][lane] = now + .38f;
                    }
                }

                for (int i = active.Count - 1; i >= 0; i--)
                {
                    var person = active[i];
                    var agent = person.Agent;
                    var position = agent.transform.position;
                    person.Distance += Vector3.Distance(person.Position, position);
                    person.Position = position;
                    if ((position - person.ProgressPoint).sqrMagnitude > .16f)
                    {
                        person.ProgressPoint = position;
                        person.LastProgress = now;
                    }
                    if (!agent.pathPending && Vector3.Distance(position, person.Target) <= .30f)
                        person.State = person.CompletePlan ? LoadState.Arrived : LoadState.Partial;
                    else if (now - person.LastProgress > 15)
                        person.State = LoadState.Stalled;
                    else if (now - person.ReleasedAt > 210)
                        person.State = LoadState.TimedOut;
                    if (person.State == LoadState.Walking) continue;
                    if(reportRoutes && person.Seat.role != "Visitante de prueba" && person.HasApproach &&
                       (person.State == LoadState.Stalled || person.State == LoadState.Partial))
                    {
                        person.Display.agent = agent;
                        person.Display.destination = person.Approach;
                        if(TryRelocateNearSeat(person.Display))
                        {
                            person.State = LoadState.Arrived;
                            person.Position = agent.transform.position;
                        }
                    }
                    agent.enabled = false;
                    // Preserve the agent object and rig at the observed endpoint for inspection.
                    active.RemoveAt(i);
                }

                var label = singleBuilding ? buildingFilter : "Prueba de aforo";
                int pending = queues.Sum(q => q.Count);
                message = $"{label} · {active.Count} caminando · {pending} en espera";
                if(Application.isEditor && !singleBuilding && !focused && buildingFilter == null && now >= nextReport)
                {
                    nextReport = now + 10;
                    File.WriteAllText("Library/campus_occupancy_progress.txt",
                        $"{loadPeople.Count} asignados, {loadPeople.Count(p => p.State == LoadState.Arrived)} llegaron, {eligibleStalls} elegibles, {loadPeople.Count(p => p.Display?.relocated == true)} trasladados, {active.Count} caminando, {pending} en espera");
                }
                yield return null;
            }
            while (active.Count > 0 || queues.Any(q => q.Count > 0));

            WriteLoadReport(reportRoutes ? "campus_rutas_y_traslados.csv" : singleBuilding ? $"prueba_edificio_{buildingFilter}.csv" : focused ? "administrativo_principal_biblioteca_rutas.csv"
                : buildingFilter == null ? "campus_aforo_maximo.csv" : "administrativo_principal_rutas.csv");
            checkingLocalAccess = false;
            var testName = singleBuilding ? buildingFilter : "Prueba de aforo";
            message = $"{testName} · {loadPeople.Count(p => p.State == LoadState.Arrived)}/{loadPeople.Count} llegaron · los demás quedan para inspección";
            Debug.Log(message);
        }

        static Vector3[] BuildingOrigins(string building)
        {
            var points = building == LibraryBuilding ? LibraryOrigins
                    : AccessPoints.FirstOrDefault(entry => entry.Building == building).Points;
            if (points == null && building == "Bloque VII")
                    points = new Vector3[]{new(145,0,-169),new(111,0,-198),new(181,0,-196)};
            if (points == null && building == "Bloque VIII")
                    points = new Vector3[]{new(141,0,-218),new(105,0,-243),new(175,0,-243)};
            if (points == null)
            {
                    var site = VisitorBuildings.FirstOrDefault(entry => entry.Building == building);
                    if (site.Targets != null) points = new[]{site.Origin};
            }
            return points;
        }

        static bool PickLoadOrigin(Vector3[] points, IEnumerable<Seat> seats, out Vector3 origin)
        {
            origin = points?.FirstOrDefault() ?? Vector3.zero;
            if (points == null) return false;
            int best = -1;
            foreach (var point in points)
            {
                if (!NavMesh.SamplePosition(point, out var hit, .38f, NavMesh.AllAreas)) continue;
                var sample = seats.OrderBy(s => (s.position-hit.position).sqrMagnitude).Take(16);
                int score = sample.Count(s => PlanLocalTrip(s, hit.position) != null);
                if (score <= best) continue;
                best = score;
                origin = hit.position;
            }
            return best >= 0;
        }

        static void PlanLoadPerson(LoadPerson person)
        {
            var seat = person.Seat;
            var path = new NavMeshPath();
            float bestRemaining = float.PositiveInfinity;
            bool hasApproach = false;
            foreach (float radius in new[] { .50f, .72f })
            {
                for (int direction = 0; direction < 8; direction++)
                {
                    var probe = seat.position + Quaternion.Euler(0, direction * 45, 0) * Vector3.forward * radius;
                    if (!NavMesh.SamplePosition(probe, out var hit, .50f, NavMesh.AllAreas) ||
                        Mathf.Abs(hit.position.y - seat.position.y) > .55f ||
                        Vector3.ProjectOnPlane(hit.position - seat.position, Vector3.up).magnitude > 1.05f) continue;
                    hasApproach = true;
                    if (!NavMesh.CalculatePath(person.Origin, hit.position, NavMesh.AllAreas, path)) continue;
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        person.Target = hit.position;
                        person.Approach = hit.position;
                        person.HasApproach = true;
                        person.CompletePlan = true;
                        return;
                    }

                    var corners = path.corners;
                    if (corners.Length < 2) continue;
                    var end = corners[corners.Length - 1];
                    float remaining = (hit.position - end).sqrMagnitude;
                    if (remaining >= bestRemaining || (person.Origin - end).sqrMagnitude < .25f) continue;
                    bestRemaining = remaining;
                    person.Target = end;
                    person.Approach = hit.position;
                    person.HasApproach = true;
                }
            }
            if (!hasApproach) person.State = LoadState.NoApproach;
            else if (float.IsPositiveInfinity(bestRemaining)) person.State = LoadState.NoRoute;
        }

        static Vector3[] LoadLanes(Vector3 origin)
        {
            var lanes = new List<Vector3> { origin };
            for (int i = 0; i < 8; i++)
            {
                var offset = Quaternion.Euler(0,i*45,0) * Vector3.forward * 1.2f;
                if (NavMesh.SamplePosition(origin + offset, out var hit, .28f, NavMesh.AllAreas) &&
                    !NavMesh.Raycast(origin, hit.position, out _, NavMesh.AllAreas))
                    lanes.Add(hit.position);
            }
            return lanes.ToArray();
        }

        static void ReleaseLoadPerson(LoadPerson person, Vector3 origin, float now)
        {
            var obj = new GameObject(person.Seat.role + " · " + person.Seat.name);
            obj.transform.position = origin;
            var agent = obj.AddComponent<NavMeshAgent>();
            agent.radius = CampusAgentRadius;
            agent.height = CampusAgentHeight;
            agent.speed = 3.5f;
            agent.acceleration = 10;
            agent.avoidancePriority = 20 + (int)(origin.sqrMagnitude % 60);
            agent.stoppingDistance = .12f;
            person.State = LoadState.Walking;
            person.Position = origin;
            person.ProgressPoint = origin;
            person.LastProgress = now;
            person.ReleasedAt = now;
            person.Agent = agent;
            person.Display = new Trip { seat = person.Seat };
            if (agent.isOnNavMesh && agent.SetDestination(person.Target)) return;
            person.State = LoadState.NoRoute;
            agent.enabled = false;
        }

        void ClearLoadPeople()
        {
            foreach (var person in loadPeople)
            {
                if (person.Agent)
                {
                    person.Agent.enabled = false;
                    Destroy(person.Agent.gameObject);
                }
                if (person.Display != null && person.Display.visual) Destroy(person.Display.visual);
            }
            loadPeople.Clear();
        }

        void LateUpdate()
        {
            foreach (var person in loadPeople)
            {
                if (person.Display == null) continue;
                bool seated = person.State == LoadState.Arrived && person.Seat.role != "Visitante de prueba";
                person.Display.phase = seated ? 2 : 1;
                Vector3 velocity = person.State == LoadState.Walking ? person.Agent.velocity : Vector3.zero;
                Visual(person.Display, seated ? person.Seat.position : person.Position, velocity);
                if (!person.Display.visual) continue;
                if (person.Agent)
                {
                    var label = $"Prueba · {person.Seat.building} · {person.Seat.name} · {person.State}";
                    if (person.Agent.gameObject.name != label) person.Agent.gameObject.name = label;
                    if (person.Display.visual.transform.parent != person.Agent.transform)
                        person.Display.visual.transform.SetParent(person.Agent.transform, true);
                }
                else if (person.Display.visual.name.EndsWith("(Clone)"))
                    person.Display.visual.name = $"Prueba · {person.Seat.building} · {person.Seat.name} · {person.State}";
            }
        }
        void WriteLoadReport(string filename)
        {
            string directory = Application.isEditor ? "Documentation" : Application.persistentDataPath;
            using var writer = new StreamWriter(Path.Combine(directory,filename),false,new UTF8Encoding(true));
            writer.WriteLine("id,tipo,edificio,zona,puesto,resultado,ruta_planeada,origen_x,origen_y,origen_z,puesto_x,puesto_y,puesto_z,fin_ruta_x,fin_ruta_y,fin_ruta_z,posicion_final_x,posicion_final_y,posicion_final_z,metros,elegible_traslado,traslado_cercano");
            int id = 0;
            foreach (var person in loadPeople)
            {
                var seat = person.Seat;
                string status = person.State switch
                {
                    LoadState.Arrived => seat.role == "Visitante de prueba" ? "llegó al punto de prueba" : "ocupó el puesto",
                    LoadState.Partial => "llegó al corte de la ruta parcial",
                    LoadState.Stalled => "detenido durante el movimiento",
                    LoadState.TimedOut => "límite de tiempo en movimiento",
                    LoadState.NoApproach => "sin punto libre junto al puesto",
                    LoadState.NoSpawn => "sin aparición navegable",
                    _ => "sin ruta utilizable desde el origen"
                };
                string plan = person.CompletePlan ? "completa" : person.Target != person.Origin ? "parcial" : "ninguna";
                writer.WriteLine(FormattableString.Invariant($"{++id},{Q(seat.role)},{Q(seat.building)},{Q(seat.room)},{Q(seat.name)},{Q(status)},{plan},{CsvPoint(person.Origin)},{CsvPoint(seat.position)},{CsvPoint(person.Target)},{CsvPoint(person.Position)},{person.Distance:F2},{(person.Display?.relocationEligible == true?"sí":"no")},{(person.Display?.relocated == true?"sí":"no")}"));
            }
        }

        static string CsvPoint(Vector3 point) => FormattableString.Invariant($"{point.x:F3},{point.y:F3},{point.z:F3}");
    }
}











