Shader "HSD/nDebug/HalfLitSolid"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                float3 normal = normalize(i.normal);
                float ndotl1 = max(0, dot(normal, normalize(float3(0, .5, .5))));
                float ndotl2 = max(0, dot(normal, normalize(float3(0, -.5, -.5))));
                float halfLambert1 = ndotl1 * 0.5 + 0.5;
                float halfLambert2 = (ndotl2 * 0.5 + 0.5) * 0.65;
                col *= max(halfLambert1, halfLambert2);
                col.a = _Color.a;// Simple diffuse lighting
                return col;
            }
            ENDCG
        }
        
    }
    FallBack "Diffuse"
}
