Shader "Campus/Color del modelo" {
 Properties { _CutHeight("Corte por altura", Float) = 10000 }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
 Pass {
 Cull Off
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float _CutHeight;
 int _CampusFloorCount;
 float4 _CampusFloorBounds[24];
 float4 _CampusFloorHeights[24];
 struct Attributes {float4 position:POSITION; float3 normal:NORMAL; float4 color:COLOR;};
 struct Varyings {
   float4 position:SV_POSITION;
   float3 color:TEXCOORD0;
   float3 world:TEXCOORD1;
   float3 normal:TEXCOORD2;
   float vertexAlpha:TEXCOORD3;
 };
 Varyings vert(Attributes a){Varyings v;v.position=TransformObjectToHClip(a.position.xyz);v.world=TransformObjectToWorld(a.position.xyz);float diffuse=saturate(dot(normalize(a.normal),normalize(float3(.4,.85,-.3))));v.color=a.color.rgb*(.55+.65*diffuse);v.normal=TransformObjectToWorldNormal(a.normal);v.vertexAlpha=a.color.a;return v;}
 half4 frag(Varyings v):SV_Target{
   float cut=_CutHeight;
   [loop] for(int i=0;i<_CampusFloorCount;i++){
     float4 box=_CampusFloorBounds[i];
     if(all(v.world.xz>=box.xy)&&all(v.world.xz<=box.zw)){
       cut=_CampusFloorHeights[i].x;
       break;
     }
   }
   clip(cut-v.world.y);
   // Court panels carry the wire-mesh marker in their vertex alpha.
   if(v.vertexAlpha < .26)
   {
     float2 normalXZ = v.normal.xz;
     float2 tangent = float2(-normalXZ.y, normalXZ.x) * rsqrt(max(dot(normalXZ,normalXZ), .0001));
     float along = dot(v.world.xz,tangent);
     float2 lattice = float2(along + v.world.y,along - v.world.y) / .14;
     float footprint = max(fwidth(lattice.x), fwidth(lattice.y));
     float2 distanceToWire = abs(frac(lattice + .5) - .5);
     clip(min(.047,.84-footprint) - min(distanceToWire.x,distanceToWire.y));
     return half4(max(v.color,half3(.31,.33,.34)),1);
   }
   return half4(v.color,1);
 }
 ENDHLSL
 }
 }
}




