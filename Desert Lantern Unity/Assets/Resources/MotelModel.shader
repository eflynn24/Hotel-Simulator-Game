Shader "Desert Lantern/Weathered Surface"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _Wear ("Wear", Range(0,1)) = .35
        _Grain ("Grain size", Float) = 5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        fixed4 _EmissionColor;
        half _Wear;
        half _Grain;
        struct Input { float3 worldPos; };
        float hash(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }
        float noise(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f*f*(3-2*f);
            float x = lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),
                           lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y);
            float y = lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),
                           lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y);
            return lerp(x,y,f.z);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = IN.worldPos * _Grain;
            float weather = .65*noise(p*.16) + .25*noise(p) + .1*noise(p*9);
            o.Albedo = _Color.rgb * lerp(1, .58 + weather*.58, _Wear);
            o.Emission = _EmissionColor.rgb;
            o.Metallic = 0;
            o.Smoothness = .1;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
