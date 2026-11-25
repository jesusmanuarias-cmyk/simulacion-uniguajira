using UnityEngine;

namespace CampusMonday {
 // NavMesh paths approximate stairs with slopes. The visible feet must use the real treads.
 public static class NpcGroundSurface {
   static readonly RaycastHit[] hits=new RaycastHit[64];
   public static bool IsWalkableSurface(Collider collider){
     string name=collider.name.ToLowerInvariant();
     return name.Contains("losa")||name.Contains("piso")||name.Contains("escal")||name.Contains("rampa")||name.Contains("anden")||name.Contains("suelo")||name.Contains("pavimento")||name.Contains("terreno")||name.Contains("calle");
   }
   public static bool TryHeight(Vector3 point,out float height,float radius=0,float maxRise=.75f){
     height=float.NegativeInfinity;
     bool previousBackfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
     float originHeight=maxRise+.2f;
     int count;try{count=radius>0?Physics.SphereCastNonAlloc(point+Vector3.up*originHeight,radius,Vector3.down,hits,originHeight+.85f,~(1<<2),QueryTriggerInteraction.Ignore):Physics.RaycastNonAlloc(point+Vector3.up*originHeight,Vector3.down,hits,originHeight+.85f,~(1<<2),QueryTriggerInteraction.Ignore);}finally{Physics.queriesHitBackfaces=previousBackfaces;}
     for(int i=0;i<count;i++){
       var hit=hits[i];if(Mathf.Abs(hit.normal.y)<.55f||!IsWalkableSurface(hit.collider)||hit.point.y>point.y+maxRise||hit.point.y<point.y-.8f)continue;
       if(hit.point.y>height)height=hit.point.y;
     }
     if(radius>0&&TryHeight(point,out float rayHeight,0,maxRise))height=Mathf.Max(height,rayHeight);
     return !float.IsNegativeInfinity(height);
   }
   // A long NavMesh corner segment can lag behind the landing by over one metre.
   // Stay below half a storey's height so another floor cannot be selected.
   public static Vector3 Project(Vector3 point){if(TryHeight(point,out float height,0,1.45f))point.y=height+.012f;return point;}
 }
}
