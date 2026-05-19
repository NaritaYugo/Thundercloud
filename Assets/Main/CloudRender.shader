Shader "Custom/CloudRender"
{
    Properties
    {
        // C#から届くが、propertyに無いとエラーメッセージが出ることがある
        _MainTex ("Volume Texture", 3D) = "" {} 
        _FlashTex ("Flash Texture", 3D) = "" {}

        _LightDir ("Light Direction", Vector) = (1,1,1)
        _StepSize ("Step Size", Range(0.001, 1)) = 0.01
        _AlphaThreshold ("Alpha Threshold", Range(0, 1)) = 0.1
        _Absorption ("Absorption Rate", Range(0, 1)) = 0.3
        _Penetration ("Penetration Rate", Range(0, 1)) = 0.1
        _Strength ("Bleeding Strength", Range(0, 1)) = 0.3
        _CloudBaseColor ("Cloud Base Color (Light)", Color) = (1, 1, 1, 1)
        _CloudShadowColor ("Cloud Shadow Color (Ambient)", Color) = (0.3, 0.4, 0.6, 1)
        [HDR] _FlashColor("Flash Color", Color) = (0.5, 0.8, 1.0, 1)
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes 
            {
                float4 positionOS : POSITION;
                float3 uv : TEXCOORD0;
            };

            struct Varyings 
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 localPos   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            TEXTURE3D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE3D(_FlashTex);
            SAMPLER(sampler_FlashTex);

            float _ScrollSpeed;

            CBUFFER_START(UnityPerMaterial)
                float _StepSize;
                float _AlphaThreshold;
                float _Absorption;
                float _Penetration;
                float _Strength;
                float3 _LightDir;
                half4 _CloudBaseColor;
                half4 _CloudShadowColor;
                half4 _FlashColor;
            CBUFFER_END

            float HenyeyGreenstein (float3 rayDir, float3 lightDir){
                float g = 0.5;

                float c = dot(rayDir, lightDir);
                float p = (1 - g*g) / pow((1 + g*g - 2*g*c), 1.5);
                return p;
            }

            Varyings vert (Attributes IN) 
            {
                Varyings OUT;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vertexInput.positionCS;
                OUT.positionWS = vertexInput.positionWS;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.localPos = IN.positionOS.xyz + 0.5; 
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target 
            {
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                half3 lightColor = mainLight.color;

                float3 objSpaceCameraPos = TransformWorldToObject(GetCameraPositionWS());
                float3 rayDir = normalize(IN.positionOS - objSpaceCameraPos);
                
                float3 p = IN.localPos; 
                half4 accumColor = half4(0.0, 0.0, 0.0, 0.0);

                [loop]
                for (int s = 0; s < 100; s++) {
                    float density = SAMPLE_TEXTURE3D(_MainTex, sampler_MainTex, frac(p + _Time.y * _ScrollSpeed)).r;

                    // スクロールしたテクスチャを参照して計算しているのでスクロール不要
                    float lightning = SAMPLE_TEXTURE3D(_FlashTex, sampler_FlashTex, p).r;

                    if (density > _AlphaThreshold) 
                    {
                        float3 q = p;
                        float accumDensity = 0;
                        
                        float lightStepSize = _StepSize * 5.0; 

                        // 各点から光源に向かってサブマーチ
                        [loop]
                        for (int t = 0; t < 6; t++)
                        {
                            q += lightDir * lightStepSize;

                            if (any(q < 0.0) || any(q > 1.0))
                                break;
                            
                            accumDensity += SAMPLE_TEXTURE3D(_MainTex, sampler_MainTex, frac(q + _Time.y * _ScrollSpeed)).r;

                            if (accumDensity > 3.0) 
                                break;
                        }

                        float alpha = (density - _AlphaThreshold) * _StepSize * 10.0;

                        float attenuation = exp(-accumDensity * _Absorption);

                        float fakeScattering = exp(-accumDensity * _Penetration) * _Strength; 
                        float finalLightOcclusion = attenuation + fakeScattering;

                        half3 illumination = lerp(_CloudShadowColor.rgb, _CloudBaseColor.rgb, attenuation);

                        half3 cloudColor = (1.0 - density * 0.5)
                                         * HenyeyGreenstein(rayDir, lightDir)
                                         * lightColor
                                         * illumination
                                         * finalLightOcclusion;
                        
                        cloudColor += lightning * _FlashColor;

                        accumColor.rgb += (1.0 - accumColor.a) * cloudColor * alpha;
                        accumColor.a += (1.0 - accumColor.a) * alpha;
                    }

                    p += rayDir * _StepSize;

                    if (any(p < 0.0) || any(p > 1.0) || accumColor.a >= 0.95)
                        break;
                }
                
                return accumColor;
            }
            ENDHLSL
        }
    }
}