Shader "Personal/StudioSurface" {
    Properties { _MainTex ("Artwork", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; float4 _Color;
            struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float3 normal:TEXCOORD1; float2 uv:TEXCOORD0; };
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal); o.uv=TRANSFORM_TEX(v.uv,_MainTex); return o; }
            fixed4 frag(Output i):SV_Target { float shade=0.48+0.52*saturate(dot(normalize(i.normal),normalize(float3(0.3,0.85,0.4)))); return fixed4(tex2D(_MainTex,i.uv).rgb*_Color.rgb*shade,1); }
            ENDCG
        }
    }
}
