Shader "Custom/MaskStencil"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _StencilID ("Stencil ID", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Geometry-1" }

        // Don't render color
        ColorMask 0

        Stencil
        {
            Ref [_StencilID]
            Comp Always
            Pass Replace
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // 🔥 THIS LINE FIXES YOUR ISSUE
                clip(col.a - 0.9);

                return 0;
            }
            ENDCG
        }
    }
}