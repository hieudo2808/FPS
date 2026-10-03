Shader "FPS/Survival/LocalHeat"
{
    Properties
    {
        _MainTex ("Soft mask", 2D) = "white" {}
        _Pixels ("Maximum distortion in pixels", Range(0, 3)) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        // Named grab is shared by every fire zone for this camera in the Built-in pipeline.
        GrabPass { "_SurvivalHeatBackground" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _SurvivalHeatBackground;
            float4 _SurvivalHeatBackground_TexelSize;
            float _Pixels;
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 screen : TEXCOORD1; fixed4 color : COLOR; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeGrabScreenPos(o.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(Varyings i) : SV_Target
            {
                float mask = tex2D(_MainTex, i.uv).a * i.color.a;
                float2 ripple = float2(sin(i.uv.y * 21 - _Time.y * 4), cos(i.uv.x * 17 + _Time.y * 3));
                float2 uv = i.screen.xy / i.screen.w;
                uv += ripple * abs(_SurvivalHeatBackground_TexelSize.xy) * _Pixels * mask;
                return fixed4(tex2D(_SurvivalHeatBackground, uv).rgb, mask);
            }
            ENDCG
        }
    }
}
