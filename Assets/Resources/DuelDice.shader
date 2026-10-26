Shader "Roulette/Dice"
{
    Properties { _BaseColor("Face color",Color)=(.7,.55,.3,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            Varyings Vert(Attributes input){Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);return output;}
            half4 Frag(Varyings input):SV_Target{return _BaseColor;}
            ENDHLSL
        }
    }
}
