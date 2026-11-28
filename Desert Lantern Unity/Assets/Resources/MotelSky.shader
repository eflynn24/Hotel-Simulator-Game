Shader "Desert Lantern/Motel Sky"
{
    Properties
    {
        _Daylight ("Daylight", Range(0,1)) = 1
        _Horizon ("Horizon", Color) = (.66,.75,.82,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            half _Daylight;
            fixed4 _Horizon;
            struct Interpolator { float4 position : SV_POSITION; float3 ray : TEXCOORD0; };

            Interpolator vert(appdata_base input)
            {
                Interpolator output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.ray = input.vertex.xyz;
                return output;
            }

            float hash(float2 p)
            {
                float3 n = frac(float3(p.xyx) * .1031);
                n += dot(n, n.yzx + 33.33);
                return frac((n.x + n.y) * n.z);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x),
                    lerp(hash(i + float2(0, 1)), hash(i + 1), f.x), f.y);
            }

            fixed4 frag(Interpolator input) : SV_Target
            {
                float3 ray = normalize(input.ray);
                half height = saturate(ray.y);
                half3 zenith = lerp(half3(.035,.055,.13), half3(.19,.44,.76), _Daylight);
                half3 sky = lerp(_Horizon.rgb, zenith, saturate(sqrt(height) * 1.45));
                float2 uv = ray.xz * 2.7 / (max(ray.y, .01) + .26) + float2(12.4, 1.7);
                float clouds = noise(uv) * .58 + noise(uv * 2.13) * .26 + noise(uv * 4.31) * .11 + noise(uv * 8.5) * .05;
                half coverage = smoothstep(.53, .73, clouds) * smoothstep(.025, .20, height);
                half3 cloud = lerp(half3(.52,.62,.75), half3(1,.96,.89), smoothstep(.52,.75,clouds));
                return fixed4(lerp(sky, cloud, coverage * _Daylight), 1);
            }
            ENDCG
        }
    }
}
