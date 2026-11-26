using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CampusMonday
{
    public partial class FullCampus
    {
        const string SharedOfficeBuilding = "Bloques VII y VIII · oficinas";
        Seat[] officeSeats = System.Array.Empty<Seat>();

        void LoadOffices()
        {
            string path=Path.Combine(Folder,"office_seats.json");
            if(!File.Exists(path))return;
            officeSeats=JsonUtility.FromJson<Inventory>(File.ReadAllText(path)).seats;
            inventory.seats=inventory.seats.Concat(officeSeats).ToArray();
        }

        void DrawOffices()
        {
            var vertices=new List<Vector3>();
            var indices=new List<int>();
            var colors=new List<Color32>();
            var wood=new Color32(139,96,61,255);
            var metal=new Color32(75,78,83,255);
            var chair=new Color32(54,91,123,255);
            foreach(var seat in officeSeats)
            {
                var rotation=Quaternion.LookRotation(seat.forward);
                void Box(Vector3 center,Vector3 size,Color32 color)
                {
                    int first=vertices.Count;
                    for(int i=0;i<8;i++)
                    {
                        var offset=new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f);
                        vertices.Add(seat.position+rotation*(center+Vector3.Scale(size,offset)));
                        colors.Add(color);
                    }
                    foreach(int index in OfficeCubeFaces)indices.Add(first+index);
                }
                Box(new Vector3(0,.74f,.68f),new Vector3(1.12f,.06f,.42f),wood);
                foreach(float x in new[]{-.49f,.49f})
                    Box(new Vector3(x,.35f,.68f),new Vector3(.07f,.70f,.35f),metal);
                Box(new Vector3(0,.43f,0),new Vector3(.44f,.08f,.43f),chair);
                Box(new Vector3(0,.64f,-.20f),new Vector3(.44f,.42f,.08f),chair);
                Box(new Vector3(0,.20f,0),new Vector3(.08f,.40f,.08f),metal);
                Box(new Vector3(0,.04f,0),new Vector3(.40f,.08f,.36f),metal);
                Box(new Vector3(0,.94f,.77f),new Vector3(.37f,.25f,.055f),metal);
                Box(new Vector3(0,.79f,.77f),new Vector3(.13f,.07f,.12f),metal);
            }
            if(vertices.Count==0)return;
            var mesh=new Mesh{name="Mobiliario de oficinas · piso 2"};
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices,0);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            var child=new GameObject(mesh.name);
            child.transform.SetParent(transform,false);
            child.AddComponent<MeshFilter>().sharedMesh=mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial=mapMaterial;
        }

        static readonly int[] OfficeCubeFaces =
        {
            0,2,3,0,3,1, 4,5,7,4,7,6, 0,4,6,0,6,2,
            1,3,7,1,7,5, 0,1,5,0,5,4, 2,6,7,2,7,3
        };
    }
}
