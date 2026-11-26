using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace CampusMonday
{
    public partial class FullCampus
    {
        // X/Z bounds from the campus model. Height cuts sit below the slab above each floor.
        static readonly (Vector4 Bounds, float[] Cuts)[] FloorCuts =
        {
            (new(-53,165,-16,199),new[]{2.40f,5.24f,8.14f}), // I
            (new(3,100,41,136),new[]{2.55f,5.19f}), // II
            (new(-51,90,-8,131),new[]{2.7f,6.45f,10.2f}), // III
            (new(-107,80,-47.9f,128),new[]{2.58f,5.69f,8.80f,11.50f}), // IV
            (new(-60,36,-19,81),new[]{2.39f,5.38f,8.36f,11.34f}), // V
            (new(-120,-6,-70,40),new[]{2.78f,5.9f,8.99f,12.09f}), // VI
            (new(113,-219,179,-171),new[]{3.12f,6.58f,10.02f,13.48f}), // VII
            (new(107,-268,173,-220),new[]{3.10f,6.53f,9.97f,13.40f}), // VIII
            (new(62,229,132,272),new[]{3.64f,7.46f}), // Administrativo Principal
            (new(-5,162,32,194),new[]{2.37f,5.73f}), // Administrativo B
            (new(56,51,86,81),new[]{4.15f,7.41f,10.85f}), // Admisiones
            (new(39,174,74,221),new[]{3.10f,6.69f,9.70f}), // Biblioteca
            (new(67,88,95,113),new[]{3.29f,6.44f}), // Recursos Físicos
            (new(44,143,75,173),new[]{3.07f}), // Bienestar
            (new(75,-106,121,-63),new[]{2.95f}), // SILAB C
            (new(121.1f,-107,161,-69),new[]{3.70f,6.85f}), // SILAB B
            (new(104,-125,140,-89),new[]{3.08f,5.95f}), // SILAB A
            (new(-356,-228,-262,-164),new[]{2.65f}), // Centro Recreacional y Comedor
            (new(-182.6f,113.1f,-115.3f,171.2f),new[]{3.07f,6.80f,10.46f,14.26f}), // X
            (new(-122.8f,197.3f,-57.0f,260.3f),new[]{2.86f,6.60f,10.26f,14.05f}) // X.001
        };

        static readonly (string Building, float[] Cuts)[] SeatBoundFloorCuts =
        {
            ("Bloque Administrativo A · oficinas en dos pisos", new[]{2.77f,6.1f}),
        };

        readonly Vector4[] floorBounds = new Vector4[24];
        readonly Vector4[] floorHeights = new Vector4[24];
        int floorCount;
        bool FloorCutActive => Floor > 0 && !PlayerControlled;

        void ApplyFloorCuts()
        {
            if(!mapMaterial)return;
            floorCount=0;
            if(FloorCutActive)
            {
                foreach(var block in FloorCuts)AddFloorCut(block.Bounds,block.Cuts);
                foreach(var block in SeatBoundFloorCuts)
                {
                    var seats=inventory.seats.Where(s=>s.building==block.Building).ToArray();
                    if(seats.Length==0)continue;
                    AddFloorCut(new Vector4(seats.Min(s=>s.position.x)-5, seats.Min(s=>s.position.z)-5,
                        seats.Max(s=>s.position.x)+5, seats.Max(s=>s.position.z)+5),block.Cuts);
                }
            }
            mapMaterial.SetFloat("_CutHeight",10000);
            mapMaterial.SetInt("_CampusFloorCount",floorCount);
            if(floorCount==0)return;
            mapMaterial.SetVectorArray("_CampusFloorBounds",floorBounds);
            mapMaterial.SetVectorArray("_CampusFloorHeights",floorHeights);
        }

        void AddFloorCut(Vector4 boundary,float[] cuts)
        {
            floorBounds[floorCount]=boundary;
            floorHeights[floorCount++]=new Vector4(cuts[Mathf.Min(Floor,cuts.Length)-1],0,0,0);
        }

        float VisibleHeight(Vector3 position)
        {
            for(int i=0;i<floorCount;i++)
            {
                var box=floorBounds[i];
                if(position.x>=box.x && position.x<=box.z && position.z>=box.y && position.z<=box.w)
                    return floorHeights[i].x;
            }
            return 10000;
        }

        bool TryRelocateNearSeat(Trip trip)
        {
            var assigned=trip.seat.position;
            var from=trip.agent.transform.position;
            if(Mathf.Abs(from.y-assigned.y)>1.2f ||
               Vector3.ProjectOnPlane(from-assigned,Vector3.up).sqrMagnitude>64 ||
               !NavMesh.SamplePosition(trip.destination,out var landing,.4f,NavMesh.AllAreas) ||
               Mathf.Abs(landing.position.y-assigned.y)>.55f ||
               Vector3.ProjectOnPlane(landing.position-assigned,Vector3.up).sqrMagnitude>1.1f)
                return false;

            // Select every second eligible arrival. Walkers and more distant stalls never enter this counter.
            trip.relocationEligible=true;
            if(++eligibleStalls%2!=0 || !trip.agent.Warp(landing.position))return false;

            trip.relocated=true;
            trip.beforeRelocation=from;
            trip.phase=2;
            trip.sat=elapsed;
            trip.agent.enabled=false;
            return true;
        }
    }
}

