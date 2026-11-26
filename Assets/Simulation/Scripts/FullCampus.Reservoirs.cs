using UnityEngine;
using UnityEngine.AI;

namespace CampusMonday
{
    public partial class FullCampus
    {
        // Corner pairs are the short ends of the imported reservoir surfaces.
        static readonly Vector2[][] ReservoirCorners =
        {
            new[]{new Vector2(-188.54f,-37.21f),new Vector2(-166.65f,-51.12f),
                  new Vector2(-236.33f,-104.75f),new Vector2(-208.22f,-115.01f)},
            new[]{new Vector2(-211.93f,-15.16f),new Vector2(-191.50f,-35.32f),
                  new Vector2(-262.01f,-81.96f),new Vector2(-240.35f,-103.28f)}
        };

        void DrawReservoirs()
        {
            foreach(var corners in ReservoirCorners)
            {
                var child = new GameObject("Superficie del embalse");
                child.transform.SetParent(transform,false);
                var mesh = new Mesh
                {
                    name = "Agua del embalse",
                    vertices = System.Array.ConvertAll(corners,p=>new Vector3(p.x,-.70f,p.y)),
                    triangles = new[]{0,1,3,0,3,2},
                    colors32 = System.Array.ConvertAll(corners,_=>new Color32(37,121,174,255))
                };
                mesh.RecalculateNormals();
                child.AddComponent<MeshFilter>().sharedMesh=mesh;
                child.AddComponent<MeshRenderer>().sharedMaterial=mapMaterial;
            }
        }

        void InstallReservoirObstacles()
        {
            const int strips=20;
            foreach(var corners in ReservoirCorners)
            {
                var centerline=(corners[2]+corners[3]-corners[0]-corners[1])*.5f;
                var axis=centerline.normalized;
                var cross=new Vector2(axis.y,-axis.x);
                var rotation=Quaternion.LookRotation(new Vector3(axis.x,0,axis.y));
                for(int i=0;i<strips;i++)
                {
                    float t0=(float)i/strips,t1=(float)(i+1)/strips;
                    var a=Vector2.Lerp(corners[0],corners[2],t0);
                    var b=Vector2.Lerp(corners[1],corners[3],t0);
                    var c=Vector2.Lerp(corners[0],corners[2],t1);
                    var d=Vector2.Lerp(corners[1],corners[3],t1);
                    float x0=Mathf.Min(Vector2.Dot(a,cross),Vector2.Dot(c,cross),Vector2.Dot(b,cross),Vector2.Dot(d,cross));
                    float x1=Mathf.Max(Vector2.Dot(a,cross),Vector2.Dot(c,cross),Vector2.Dot(b,cross),Vector2.Dot(d,cross));
                    float z0=Mathf.Min(Vector2.Dot(a,axis),Vector2.Dot(c,axis),Vector2.Dot(b,axis),Vector2.Dot(d,axis));
                    float z1=Mathf.Max(Vector2.Dot(a,axis),Vector2.Dot(c,axis),Vector2.Dot(b,axis),Vector2.Dot(d,axis));
                    var center=cross*((x0+x1)*.5f)+axis*((z0+z1)*.5f);
                    var child=new GameObject("Embalse no transitable");
                    child.transform.SetParent(transform,false);
                    child.transform.SetPositionAndRotation(new Vector3(center.x,-.7f,center.y),rotation);
                    var obstacle=child.AddComponent<NavMeshObstacle>();
                    obstacle.shape=NavMeshObstacleShape.Box;
                    obstacle.size=new Vector3(x1-x0,2,z1-z0);
                    obstacle.carving=true;
                    obstacle.carveOnlyStationary=false;
                }
            }
        }
    }
}
