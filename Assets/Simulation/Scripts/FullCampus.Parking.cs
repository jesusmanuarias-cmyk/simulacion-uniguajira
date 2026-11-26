using UnityEngine;
using UnityEngine.AI;

namespace CampusMonday
{
    public partial class FullCampus
    {
        const string ParkingShelters = "Paraderos del parqueadero";
        // Measured in the unnamed terrain strip in front of Biblioteca and Bienestar.
        static readonly Quaternion ShelterFacing = Quaternion.Euler(0,-73f,0);
        static readonly Vector3[] ShelterCenters =
        {
            new(120.315f,1.184f,191.390f), new(116.135f,1.184f,177.615f),
            new(112.050f,1.184f,164.315f), new(108.155f,1.184f,149.685f)
        };
        static readonly (Vector3 Center, float Yaw, float Pitch)[] BusBays =
        {
            (new(125.160f,.803f,189.585f),151f,0f),
            (new(120.505f,.840f,173.245f),151f,.05f),
            (new(115.945f,.892f,156.905f),149f,.60f)
        };
        const float BusLengthScale = .65f;
        static Vector3[] ParkingOrigins()=>new Vector3[]{
            new(116.5f,1.184f,194f),new(107.5f,1.184f,152.5f)
        };

        void DrawParkingFurniture(bool preview)
        {
            var meshes=new System.Collections.Generic.Dictionary<PrimitiveType,Mesh>();
            var coloredMeshes=new System.Collections.Generic.Dictionary<(PrimitiveType,Color32),Mesh>();
            var red=new Color32(167,52,32,255);
            var wood=new Color32(92,64,44,255);
            var roof=new Color32(224,197,145,255);
            var metal=new Color32(69,63,57,255);
            var rubber=new Color32(37,39,41,255);
            var glass=new Color32(51,86,106,255);
            var ivory=new Color32(238,232,212,255);
            var busYellow=new Color32(232,171,40,255);
            var pale=new Color32(245,234,167,255);

            Transform Group(string name,Vector3 center,Quaternion facing)
            {
                var child=new GameObject(name);
                child.transform.SetParent(transform,false);
                child.transform.SetPositionAndRotation(center,facing);
                return child.transform;
            }
            void Part(Transform group,string name,Vector3 position,Vector3 scale,Color32 color,
                bool blocks=false,Quaternion facing=default,PrimitiveType shape=PrimitiveType.Cube)
            {
                var obj=new GameObject(name);
                obj.transform.SetParent(group,false);
                obj.transform.SetLocalPositionAndRotation(position,facing==default?Quaternion.identity:facing);
                obj.transform.localScale=scale;
                if(!meshes.TryGetValue(shape,out var prototype))
                {
                    var sample=GameObject.CreatePrimitive(shape);
                    prototype=sample.GetComponent<MeshFilter>().sharedMesh;
                    if(Application.isPlaying)Destroy(sample);else DestroyImmediate(sample);
                    meshes.Add(shape,prototype);
                }
                if(!coloredMeshes.TryGetValue((shape,color),out var mesh))
                {
                    mesh=Instantiate(prototype);
                    mesh.colors32=System.Array.ConvertAll(mesh.vertices,_=>color);
                    coloredMeshes.Add((shape,color),mesh);
                }
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                obj.AddComponent<MeshRenderer>().sharedMaterial=mapMaterial;
                if(!preview&&blocks)obj.AddComponent<BoxCollider>();
            }
            void Obstacle(Transform group,string label,Vector3 center,Vector3 size)
            {
                if(preview)return;
                var obj=new GameObject(label);
                obj.transform.SetParent(group,false);
                obj.transform.localPosition=center;
                var obstacle=obj.AddComponent<NavMeshObstacle>();
                obstacle.shape=NavMeshObstacleShape.Box;
                obstacle.size=size;
                obstacle.carving=true;
                obstacle.carveOnlyStationary=false;
            }

            for(int i=0;i<ShelterCenters.Length;i++)
            {
                var shelter=Group($"Paradero {i+1}",ShelterCenters[i],ShelterFacing);
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side*2.62f;
                    for(int leg=-1;leg<=1;leg+=2)
                    {
                        Part(shelter,"Pata inclinada",new Vector3(x,1.14f,leg*.56f),new Vector3(.12f,2.36f,.12f),
                            red,true,Quaternion.Euler(-leg*12,0,0));
                        Obstacle(shelter,"Base del paradero",new Vector3(x,.6f,leg*.68f),new Vector3(.20f,1.2f,.31f));
                    }
                    Part(shelter,"Costilla",new Vector3(x,2.26f,0),new Vector3(.13f,.10f,1.96f),red);
                }
                for(int side=-1;side<=1;side+=2)
                {
                    var slope=Quaternion.Euler(side*7,0,0);
                    Part(shelter,"Cubierta",new Vector3(0,2.38f,side*.53f),new Vector3(5.86f,.065f,1.08f),roof,true,slope);
                    Part(shelter,"Borde rojo",new Vector3(0,2.31f,side*1.04f),new Vector3(5.91f,.11f,.08f),red);
                }
                var benches=new GameObject("Bancas escalonadas").transform;
                benches.SetParent(shelter,false);
                benches.localRotation=Quaternion.Euler(0,180,0);
                foreach(var row in BenchRows)
                {
                    for(int slat=0;slat<4;slat++)
                        Part(benches,"Listón del asiento",new Vector3(0,row.Rise+.425f,row.Depth-.165f+slat*.11f),
                            new Vector3(4.4f,.055f,.095f),wood);
                    for(int slat=0;slat<2;slat++)
                        Part(benches,"Respaldo",new Vector3(0,row.Rise+.65f+slat*.14f,row.Depth-.225f),
                            new Vector3(4.4f,.09f,.055f),wood,true);
                    foreach(float x in new[]{-1.95f,0,1.95f})
                    {
                        float support=row.Rise+.43f;
                        Part(benches,"Soporte de la banca",new Vector3(x,support*.5f,row.Depth),
                            new Vector3(.10f,support,.42f),red,true);
                    }
                    if(row.Rise>0)
                        Part(benches,"Apoyapiés posterior",new Vector3(0,.10f,row.Depth+.23f),
                            new Vector3(4.4f,.20f,.18f),metal,true);
                    if(!preview)
                    {
                        var collider=benches.gameObject.AddComponent<BoxCollider>();
                        collider.center=new Vector3(0,row.Rise+.425f,row.Depth);
                        collider.size=new Vector3(4.4f,.075f,.43f);
                    }
                    Obstacle(benches,"Banca",new Vector3(0,(row.Rise+.90f)*.5f,row.Depth),
                        new Vector3(4.42f,row.Rise+.90f,.47f));
                }
            }

            for(int i=0;i<BusBays.Length;i++)
            {
                var bay=BusBays[i];
                var heading=Quaternion.Euler(0,bay.Yaw,0);
                var bus=Group($"Bus estacionado {i+1}",bay.Center,heading);
                var body=new GameObject("Carrocería sobre la pendiente").transform;
                body.SetParent(bus,false);
                body.localRotation=Quaternion.Euler(bay.Pitch,0,0);
                body.localScale=new Vector3(1,1,BusLengthScale);
                Part(body,"Carrocería",new Vector3(0,1.38f,0),new Vector3(2.38f,1.81f,8.55f),busYellow,true);
                Part(body,"Cabina",new Vector3(0,2.58f,0),new Vector3(2.32f,.66f,8.4f),ivory,true);
                Part(body,"Techo",new Vector3(0,2.99f,0),new Vector3(2.26f,.18f,8.25f),ivory);
                Part(body,"Parabrisas",new Vector3(0,2.42f,4.283f),new Vector3(2.04f,.79f,.025f),glass);
                Part(body,"Vidrio trasero",new Vector3(0,2.46f,-4.283f),new Vector3(1.94f,.66f,.02f),glass);
                for(int side=-1;side<=1;side+=2)
                {
                    for(int window=0;window<6;window++)
                        Part(body,"Ventana",new Vector3(side*1.198f,2.40f,-3.21f+window*1.27f),new Vector3(.026f,.68f,1.08f),glass);
                    foreach(float axle in new[]{-2.72f,2.54f})
                    {
                        var axis=Quaternion.Euler(0,0,90);
                        Part(body,"Neumático",new Vector3(side*1.08f,.48f,axle),new Vector3(.95f,.19f,.95f),rubber,true,axis,PrimitiveType.Cylinder);
                        Part(body,"Rin",new Vector3(side*1.282f,.48f,axle),new Vector3(.49f,.018f,.49f),ivory,false,axis,PrimitiveType.Cylinder);
                    }
                    Part(body,"Faro delantero",new Vector3(side*.82f,1.04f,4.29f),new Vector3(.34f,.20f,.06f),pale);
                    Part(body,"Faro trasero",new Vector3(side*.85f,1.02f,-4.29f),new Vector3(.23f,.23f,.06f),red);
                }
                Obstacle(bus,"Huella del bus",new Vector3(0,1.43f,0),new Vector3(2.61f,2.9f,8.65f*BusLengthScale));
            }
        }

        static readonly (float Depth,float Rise)[] BenchRows = { (.54f,0f),(-.58f,.28f) };
        static Quaternion BenchFacing => ShelterFacing*Quaternion.Euler(0,180,0);

        void AddParkingSeats()
        {
            var seats=new System.Collections.Generic.List<Seat>(inventory.seats);
            for(int i=0;i<ShelterCenters.Length;i++)
                foreach(var row in BenchRows)
                foreach(float x in new[]{-1.40f,0,1.40f})
                    seats.Add(new Seat{
                        building=ParkingShelters,role="Visitante",room=$"Paradero {i+1}",name=$"Fila {(row.Rise>0?2:1)} · plaza {x:+0.0;-0.0;0}",
                        position=ShelterCenters[i]+BenchFacing*new Vector3(x,row.Rise,row.Depth),forward=BenchFacing*Vector3.forward
                    });
            inventory.seats=seats.ToArray();
        }
    }
}
