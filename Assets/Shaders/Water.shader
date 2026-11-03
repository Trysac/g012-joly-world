// The river: a see-through low-poly sheet that ripples slowly.
Shader "Joly World/Water"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 0.8)
        _Wave ("Wave height", Float) = 0.07
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Wrap alpha:fade vertex:vert
        #pragma target 3.0

        fixed4 _Color;
        float _Wave;

        struct Input
        {
            float4 color : COLOR;
        };

        void vert(inout appdata_full v)
        {
            float4 world = mul(unity_ObjectToWorld, v.vertex);
            v.vertex.y += sin(_Time.y * 1.4 + world.x * 0.55 + world.z * 0.35) * _Wave;
        }

        half4 LightingWrap(SurfaceOutput s, half3 lightDir, half atten)
        {
            half diffuse = saturate(dot(s.Normal, lightDir) * 0.5 + 0.5);
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * diffuse * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed3 albedo = IN.color.rgb * _Color.rgb;
            o.Albedo = albedo;
            o.Emission = albedo * 0.25;
            o.Alpha = _Color.a * IN.color.a;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
