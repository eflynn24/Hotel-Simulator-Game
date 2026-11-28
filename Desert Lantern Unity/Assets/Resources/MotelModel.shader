Shader "Desert Lantern/Weathered Surface"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _Wear ("Wear", Range(0,1)) = .35
        _Grain ("Grain size", Float) = 5
        _Smoothness ("Smoothness", Range(0,1)) = .18
        _Metallic ("Metallic", Range(0,1)) = 0
        _GrainStretch ("Vertical grain scale", Range(.1,1)) = 1
        _StuccoWear ("Exposed stucco undercoat", Range(0,1)) = 0
        _RoofCourses ("Asphalt shingle courses", Range(0,1)) = 0
        _DetailMap ("Photographic detail (RGB) and roughness (A)", 2D) = "gray" {}
        _DetailScale ("Detail repeats per metre", Float) = .5
        _DetailStrength ("Detail strength", Range(0,1)) = 0
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
        half _Smoothness;
        half _Metallic;
        half _GrainStretch;
        half _StuccoWear;
        half _RoofCourses;
        sampler2D _DetailMap;
        half _DetailScale;
        half _DetailStrength;
        struct Input { float3 worldPos; float3 worldNormal; };
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
            p.y *= _GrainStretch;
            float patches = noise(p * .12);
            float grain = noise(p);
            float micro = noise(p * 5.5);
            float weather = .57 * patches + .31 * grain + .12 * micro;
            half3 albedo = _Color.rgb * lerp(1, .68 + weather * .48, _Wear);
            half smoothness = _Smoothness + (grain - .5) * .24 * _Wear;
            if (_DetailStrength > 0.0)
            {
                float3 normal = abs(IN.worldNormal);
                float2 uv = IN.worldPos.xy;
                if (normal.y > max(normal.x, normal.z))
                    uv = IN.worldPos.xz;
                else if (normal.x > normal.z)
                    uv = IN.worldPos.zy;

                fixed4 detail = tex2D(_DetailMap, uv * _DetailScale);
                albedo *= lerp(1.0, .36 + detail.r * 1.23, _DetailStrength);
                smoothness = lerp(smoothness, 1.0 - detail.a, _DetailStrength * .65);
            }
            if (_StuccoWear > 0)
            {
                float3 wall = IN.worldPos * float3(1.6, 2.7, 1.6);
                half baseWear = 1 - smoothstep(.15,.85, frac((IN.worldPos.y + .045) / 3.08) * 3.08);
                half eroded = smoothstep(.61,.70, noise(wall) * .72 + grain * .20 + baseWear * .23);
                half exposed = smoothstep(.73,.80, noise(wall) * .72 + grain * .20 + baseWear * .23);
                albedo *= lerp(1, .76 + micro * .17, eroded * _StuccoWear);
                albedo = lerp(albedo, albedo * half3(.79,.68,.55), exposed * _StuccoWear);
            }
            if (_RoofCourses > 0)
            {
                float2 course = IN.worldPos.xz * float2(3.1, 5.8) + IN.worldPos.y * float2(.77, .94);
                course.x += fmod(floor(course.y), 2) * .5;
                float2 seam = frac(course);
                half edge = max(1 - smoothstep(.018, .082, seam.y), (1 - smoothstep(.00, .046, seam.x)) * .55);
                albedo *= lerp(.83, 1.14, hash(float3(floor(course), 7.0))) * (1 - edge * .20);
            }
            o.Albedo = albedo;
            o.Emission = _EmissionColor.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = saturate(smoothness);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
