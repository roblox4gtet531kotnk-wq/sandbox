Shader "KiviGun/KiviUnlit"
{
    // Прозрачный/светящийся шейдер проекта для Universal Render Pipeline (Unity 7).
    // Поддерживает прозрачность через _SrcBlend/_DstBlend/_ZWrite (их ставит
    // MaterialFactory.SetupTransparent), поэтому стекло и эффекты выглядят как надо.
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Surface ("Surface", Float) = 0
        _Blend ("Blend", Float) = 0
        _SrcBlend ("SrcBlend", Float) = 1
        _DstBlend ("DstBlend", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Surface;
                float _Blend;
                float _SrcBlend;
                float _DstBlend;
                float _ZWrite;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = _BaseColor * input.color;
                return half4(c.rgb, c.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
