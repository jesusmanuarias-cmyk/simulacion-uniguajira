Shader "Campus/Color del modelo" {
 Properties { _CutHeight("Corte por altura", Float) = 10000 }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
 Pass {
 Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float _CutHeight;
 struct Attributes {float4 position:POSITION; float3 normal:NORMAL; float4 color:COLOR;};
 struct Varyings {float4 position:SV_POSITION; float3 color:TEXCOORD0; float height:TEXCOORD1;};
 Varyings vert(Attributes a){Varyings v;v.position=TransformObjectToHClip(a.position.xyz);v.height=TransformObjectToWorld(a.position.xyz).y;float diffuse=saturate(dot(normalize(a.normal),normalize(float3(.4,.85,-.3))));v.color=a.color.rgb*(.55+.65*diffuse);return v;}
 half4 frag(Varyings v):SV_Target{clip(_CutHeight-v.height);return half4(v.color,1);}
 ENDHLSL
 }
 }
}


