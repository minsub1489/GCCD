Shader "GCCD/FilmFinish"
{
    Properties { _MainTex ("Source",2D)="white"{} _BloomTex("Bloom",2D)="black"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex,_BloomTex;
        float4 _MainTex_TexelSize;
        float2 _BlurDirection;
        ENDCG
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment bright
            float4 bright(v2f_img i):SV_Target
            {
                float3 c=tex2D(_MainTex,i.uv).rgb;
                float peak=max(c.r,max(c.g,c.b));
                return float4(c*saturate((peak-1.05)/max(peak,.001)),1);
            }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blur
            float4 blur(v2f_img i):SV_Target
            {
                float3 c=tex2D(_MainTex,i.uv).rgb*.227027;
                c+=tex2D(_MainTex,i.uv+_BlurDirection*1.384615).rgb*.316216;
                c+=tex2D(_MainTex,i.uv-_BlurDirection*1.384615).rgb*.316216;
                c+=tex2D(_MainTex,i.uv+_BlurDirection*3.230769).rgb*.070270;
                c+=tex2D(_MainTex,i.uv-_BlurDirection*3.230769).rgb*.070270;
                return float4(c,1);
            }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment finish
            float4 finish(v2f_img i):SV_Target
            {
                float3 c=tex2D(_MainTex,i.uv).rgb+tex2D(_BloomTex,i.uv).rgb*.20;
                c=max(0,c)*1.03;
                c=(c*(2.51*c+.03))/(c*(2.43*c+.59)+.14);
                float luminance=dot(c,float3(.2126,.7152,.0722));
                float shadows=1-smoothstep(.03,.58,luminance);
                c+=float3(-.009,.006,.012)*shadows;
                c+=float3(.009,.004,-.006)*(1-shadows);
                c=lerp(luminance.xxx,c,.94);
                float2 p=i.uv-.5;
                c*=1-smoothstep(.15,.56,dot(p,p))*.16;
                return float4(saturate(c),1);
            }
            ENDCG
        }
    }
}
