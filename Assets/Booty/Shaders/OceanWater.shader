Shader "Booty/OceanWater"
{
    Properties
    {
        _ShallowColor   ("Shallow Color",    Color) = (0.1, 0.4, 0.7, 0.85)
        _DeepColor      ("Deep Color",       Color) = (0.02, 0.1, 0.35, 1.0)
        _FoamColor      ("Foam Color",       Color) = (0.9, 0.95, 1.0, 1.0)
        _WaveSpeed      ("Wave Speed",       Float) = 1.5
        _WaveFrequency  ("Wave Frequency",   Float) = 0.1
        _WaveAmplitude  ("Wave Amplitude",   Float) = 0.3
        _FoamThreshold  ("Foam Threshold",   Range(0.0, 1.0)) = 0.6
        _Smoothness     ("Smoothness",       Range(0.0, 1.0)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "OceanWaterForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   OceanVert
            #pragma fragment OceanFrag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ---------------------------------------------------------------------------
            // Properties (CBUFFER required by URP SRP Batcher)
            // ---------------------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float  _WaveSpeed;
                float  _WaveFrequency;
                float  _WaveAmplitude;
                float  _FoamThreshold;
                float  _Smoothness;
            CBUFFER_END

            // ---------------------------------------------------------------------------
            // Vertex input / output
            // ---------------------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float3 positionWS    : TEXCOORD0;   // world-space position
                float3 normalWS      : TEXCOORD1;
                float  displacement  : TEXCOORD2;   // normalised [-1,+1] wave height
                float  fogFactor     : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---------------------------------------------------------------------------
            // Wave helpers
            // ---------------------------------------------------------------------------
            // Returns combined Y displacement for two sine-wave layers.
            // Primary:   amp=_WaveAmplitude,       freq=_WaveFrequency,       speed=_WaveSpeed
            // Secondary: amp=_WaveAmplitude*0.5,   freq=_WaveFrequency*2.2,   speed=_WaveSpeed*1.4
            float ComputeWaveDisplacement(float3 worldPos)
            {
                float t = _Time.y;

                float primary   = sin(worldPos.x * _WaveFrequency       + t * _WaveSpeed)       * _WaveAmplitude;
                float secondary = sin(worldPos.z * (_WaveFrequency*2.2) + t * (_WaveSpeed*1.4)) * (_WaveAmplitude*0.5);

                return primary + secondary;
            }

            // Finite-difference approximation of the displaced surface normal.
            float3 ComputeWaveNormal(float3 worldPos, float eps)
            {
                float t = _Time.y;

                // Neighbour displacements along X and Z
                float dx = sin((worldPos.x + eps) * _WaveFrequency       + t * _WaveSpeed)       * _WaveAmplitude
                         + sin(worldPos.z          * (_WaveFrequency*2.2) + t * (_WaveSpeed*1.4)) * (_WaveAmplitude*0.5)
                         - ComputeWaveDisplacement(worldPos);

                float dz = sin(worldPos.x          * _WaveFrequency       + t * _WaveSpeed)       * _WaveAmplitude
                         + sin((worldPos.z + eps)   * (_WaveFrequency*2.2) + t * (_WaveSpeed*1.4)) * (_WaveAmplitude*0.5)
                         - ComputeWaveDisplacement(worldPos);

                // Cross product of tangent vectors gives the perturbed normal
                return normalize(float3(-dx / eps, 1.0, -dz / eps));
            }

            // ---------------------------------------------------------------------------
            // Vertex shader
            // ---------------------------------------------------------------------------
            Varyings OceanVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Transform to world space first so we can drive waves from world XZ
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                // --- Wave vertex displacement (Y axis only, visual only per PRD) ---
                float disp = ComputeWaveDisplacement(posWS);
                posWS.y += disp;

                // Normalise displacement to [0,1] for colour/foam use in fragment
                // Primary amp + secondary amp = 1.5x _WaveAmplitude maximum
                float maxDisp = _WaveAmplitude * 1.5;
                output.displacement = saturate((disp + maxDisp) / (2.0 * maxDisp));

                // Perturbed normal
                output.normalWS  = ComputeWaveNormal(posWS, 0.05);
                output.positionWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);

                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            // ---------------------------------------------------------------------------
            // Fragment shader
            // ---------------------------------------------------------------------------
            half4 OceanFrag(Varyings input) : SV_Target
            {
                // --- Water colour: shallow→deep gradient based on wave height ---
                half4 waterColor = lerp(_DeepColor, _ShallowColor, input.displacement);

                // --- Foam at wave crests ---
                // displacement > _FoamThreshold → soft foam blend
                float foamMask = smoothstep(_FoamThreshold - 0.05, _FoamThreshold + 0.05, input.displacement);
                half4 color    = lerp(waterColor, _FoamColor, foamMask * _FoamColor.a);

                // --- Specular highlight (Blinn-Phong) ---
                Light mainLight  = GetMainLight();
                float3 lightDir  = normalize(mainLight.direction);
                float3 viewDir   = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfVec   = normalize(lightDir + viewDir);
                float3 normalWS  = normalize(input.normalWS);

                float NdotH      = saturate(dot(normalWS, halfVec));
                // Shininess: map _Smoothness [0,1] → exponent [8,256]
                float shininess  = exp2(_Smoothness * 8.0);
                float specular   = pow(NdotH, shininess);

                // Modulate specular by main light colour and add to water
                half3 specColor  = mainLight.color * specular * 0.6;
                color.rgb       += specColor;

                // --- Fresnel rim for extra realism ---
                float fresnel    = pow(1.0 - saturate(dot(normalWS, viewDir)), 3.0);
                color.rgb       += fresnel * _ShallowColor.rgb * 0.15;

                // --- Alpha: keep shader translucency, boost opacity at crests ---
                color.a          = lerp(waterColor.a, 1.0, foamMask);

                // --- Fog ---
                color.rgb        = MixFog(color.rgb, input.fogFactor);

                return color;
            }

            ENDHLSL
        }
    }

    // Unity will fall back to this if the URP pass is unavailable
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
