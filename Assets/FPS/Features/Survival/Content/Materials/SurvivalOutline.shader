Shader "FPS/Survival/InteractionOutline"
{
    Properties
    {
        _OutlineColor ("Outline", Color) = (0.8,0.65,0.3,1)
        _Width ("World Width", Range(0.001,0.02)) = 0.003
    }
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
        Pass
        {
            Cull Front
            ZWrite Off
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _OutlineColor;
            float _Width;
            float4 vert(appdata_base v) : SV_POSITION
            {
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                world += UnityObjectToWorldNormal(v.normal) * _Width;
                return mul(UNITY_MATRIX_VP, float4(world, 1));
            }
            fixed4 frag() : SV_Target { return _OutlineColor; }
            ENDCG
        }
    }
}
