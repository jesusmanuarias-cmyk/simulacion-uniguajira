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
 struct Varyings {float4 position:SV_POSITION; float3 color:TEXCOORD0; float3 world:TEXCOORD1;};
 Varyings vert(Attributes a){Varyings v;v.position=TransformObjectToHClip(a.position.xyz);v.world=TransformObjectToWorld(a.position.xyz);float diffuse=saturate(dot(normalize(a.normal),normalize(float3(.4,.85,-.3))));v.color=a.color.rgb*(.55+.65*diffuse);return v;}
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
   return half4(v.color,1);
 }
 ENDHLSL
 }
 }
}




