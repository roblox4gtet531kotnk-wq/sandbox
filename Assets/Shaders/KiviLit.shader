Shader "KiviGun/KiviLit"
{
    // Стилизованный освещаемый шейдер проекта для Universal Render Pipeline (Unity 7).
    // В Unity 7 встроенного пайплайна нет, поэтому шейдер написан на URP-HLSL
    // и не зависит от удалённого UnityCG.cginc.
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.15
        _SunDirection ("Sun Direction", Vector) = (0.35, 0.78, 0.52)
        _SunColor ("Sun Color", Color) = (1, 0.96, 0.88)
        _Ambient ("Ambient", Color) = (0.34, 0.38, 0.42)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SunColor;
                half4 _Ambient;
                float4 _SunDirection;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color      : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.color = input.color;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 l = normalize(_SunDirection.xyz);
                float ndl = saturate(dot(n, l));

                float3 view = normalize(GetCameraPositionWS() - input.positionWS);
                float rim = pow(1.0 - saturate(dot(n, view)), 3.0);

                float3 light = _Ambient.rgb + _SunColor.rgb * ndl * 0.85 + _SunColor.rgb * rim * 0.12;

                half4 baseColor = _BaseColor * input.color;
                return half4(baseColor.rgb * light, baseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
