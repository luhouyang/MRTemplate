Shader "Custom/HeatmapOverlay"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Alpha ("Alpha Multiplier", Range(0, 1)) = 1.0
        _FlipNormals("Flip Normals", Float) = 0.0
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        
        // Disable depth writes and culling so both sides can be rendered
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Alpha;
            float _FlipNormals;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Compute the view direction from the fragment to the camera.
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                
                // Optionally flip the normal based on the _FlipNormals property.
                float3 normal = (_FlipNormals > 0.5) ? -i.worldNormal : i.worldNormal;
                
                // Compute how much the surface is facing the camera.
                float facing = dot(normal, viewDir);
                
                // Use saturate to clamp the value between 0 and 1.
                // This creates a smooth transition rather than a hard cutoff.
                col.a *= saturate(facing) * _Alpha;
                
                return col;
            }
            ENDCG
        }
    }
}
