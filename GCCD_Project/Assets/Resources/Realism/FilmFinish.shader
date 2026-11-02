Shader "GCCD/FilmFinish" { Properties { _MainTex ("Source", 2D) = "white" {} }
SubShader { Cull Off ZWrite Off ZTest Always Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;
fixed4 frag(v2f_img i):SV_Target {
 float3 c=tex2D(_MainTex,i.uv).rgb;
 c=(c*(2.51*c+.03))/(c*(2.43*c+.59)+.14);
 float2 p=i.uv-.5;float vignette=1-smoothstep(.17,.65,dot(p,p))*.3;
 float lum=dot(c,float3(.2126,.7152,.0722));c=lerp(lum.xxx,c,.88);
 return float4(saturate(c*vignette),1);
} ENDCG } } }
