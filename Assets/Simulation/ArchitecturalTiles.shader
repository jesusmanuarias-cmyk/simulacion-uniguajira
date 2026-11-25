Shader "Campus/Piso gris con juntas"
{
 Properties {
   [MainColor] _BaseColor("Baldosa gris",Color)=(0.48,0.49,0.5,1)
   _GroutColor("Junta",Color)=(0.24,0.25,0.26,1)
   _UndersideColor("Cara inferior",Color)=(0.58,0.59,0.60,1)
   _TileSize("Lado baldosa (metros)",Float)=0.6
   _JointWidth("Ancho junta (metros)",Float)=0.006
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass {
   Name "ForwardLit"
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor, _GroutColor, _UndersideColor;
    float _TileSize, _JointWidth;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
   struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half fog:TEXCOORD2;};
   Varyings Vert(Attributes input){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(input.normalOS);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 Frag(Varyings i):SV_Target {
    float size=max(_TileSize,0.05);float2 uv=i.positionWS.xz/size;
    float2 cell=frac(uv);float2 edge=min(cell,1-cell)*size;
    float distanceToJoint=min(edge.x,edge.y);float aa=max(max(fwidth(i.positionWS.x),fwidth(i.positionWS.z)),.0005);
    float joint=1-smoothstep(_JointWidth*.5,_JointWidth*.5+aa,distanceToJoint);
    float2 tile=floor(uv);float noise=frac(sin(dot(tile,float2(127.1,311.7)))*43758.5453);
    half3 color=lerp(_BaseColor.rgb*(.97+noise*.06),_GroutColor.rgb,joint);
    half3 n=normalize(i.normalWS);color=lerp(_UndersideColor.rgb,color,smoothstep(.55,.9,n.y));
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half3 illumination=SampleSH(n)+sun.color*(1.0/PI)*saturate(dot(n,sun.direction))*sun.distanceAttenuation*sun.shadowAttenuation;
    // A matte surface: no glossy reflection washing out the grey or the joints.
    half3 result=color*illumination;
    return half4(MixFog(result,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
