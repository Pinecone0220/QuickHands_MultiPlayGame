Shader "Roulette/ElectricDischarge"
{
    Properties { [HDR] _BaseColor("Electrical energy",Color)=(2.2,2.2,2.2,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            Varyings Vert(Attributes input)
            { Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.color=input.color;o.uv=input.uv;return o; }
            half4 Frag(Varyings input):SV_Target
            {
                half across=abs(input.uv.y*2-1);
                half edge=exp2(-across*across*7)*saturate((1-across)*4);
                half4 color=_BaseColor*input.color;color.a*=edge;return color;
            }
            ENDHLSL
        }
    }
}
