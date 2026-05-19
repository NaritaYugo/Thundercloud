Shader "Custom/BoltRender"
{
    Properties
    {
        [HDR] _Color("Tip Color", Color) = (0.5, 0.8, 1.0, 1)
        _ShieldingRate ("Shielding Rate", Range(0.01, 1)) = 0.3
        _StepSize ("Raymarch Step Size", Range(0.01, 0.1)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "BoltPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Tip {
                float3 currPos;
                float3 prevPos;
            };

            StructuredBuffer<Tip> _BoltBuffer;
            StructuredBuffer<int> _IsAliveTip;
            float4 _Color;
            float3 _GridRes;
            float3 _BoundsSize;
            float3 _Locate;
            float _ScrollSpeed;

            TEXTURE3D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _ShieldingRate;
            float _StepSize;
            float3 _CameraUV;

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0;
            };

            Varyings vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) {
                Varyings OUT;
                Tip tip = _BoltBuffer[instanceID];
                
                float3 pos = (vertexID == 0) ? tip.prevPos : tip.currPos;

                float3 uv = pos / _GridRes;
                OUT.uv = uv;

                float3 localPos = (pos / _GridRes - 0.5) * _BoundsSize + _Locate;
                
                OUT.positionCS = TransformObjectToHClip(localPos);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target {
                if (_IsAliveTip[0] == 0) discard;

                float3 rayDir = _CameraUV - IN.uv;
                float dist = length(rayDir);
                rayDir /= dist;

                float accumDensity = 0;
                int maxSteps = min(50, (int)(dist / _StepSize));

                for (int i = 0; i < maxSteps; i++) {
                    float3 samplePos = IN.uv + rayDir * (i * _StepSize);
                    
                    if (any(samplePos < 0.0) || any(samplePos > 1.0)) break;
                    
                    float density = SAMPLE_TEXTURE3D(_MainTex, sampler_MainTex, frac(samplePos + _Time.y * _ScrollSpeed)).r;
                    accumDensity += density * _StepSize * _ShieldingRate;
                }

                return _Color * saturate(accumDensity / _ShieldingRate);
            }
            ENDHLSL
        }
    }
}