// Flat-shaded low-poly surfaces coloured per vertex. The meshes repeat their vertices per
// triangle, so every face keeps its own normal and colour. A wrapped diffuse term keeps the
// shadowed sides bright, like the soft toy look of the 2021 prototype.
Shader "Joly World/Low Poly"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Emission ("Glow", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Wrap fullforwardshadows addshadow
        #pragma target 3.0

        fixed4 _Color;
        half _Emission;

        struct Input
        {
            float4 color : COLOR;
        };

        half4 LightingWrap(SurfaceOutput s, half3 lightDir, half atten)
        {
            half diffuse = saturate(dot(s.Normal, lightDir) * 0.6 + 0.4);
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * diffuse * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed3 albedo = IN.color.rgb * _Color.rgb;
            o.Albedo = albedo;
            o.Emission = albedo * _Emission;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
