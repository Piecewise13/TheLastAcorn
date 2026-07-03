Shader "Custom/2D_Background_Blur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurSize ("Blur Size", Range(0, 0.05)) = 0.01
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
        }

        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha

        // Grab the background screen texture
        GrabPass { "_GrabTexture" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 grabPos  : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _GrabTexture;
            fixed4 _Color;
            float _BlurSize;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                OUT.grabPos = ComputeGrabScreenPos(OUT.vertex);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float4 uv = IN.grabPos;
                half4 sum = half4(0,0,0,0);
                float step = _BlurSize;

                // 9-Sample Box Blur Grid
                // Samples pixels in a grid around the center to blur them together
                sum += tex2Dproj(_GrabTexture, float4(uv.x - step, uv.y - step, uv.z, uv.w));
                sum += tex2Dproj(_GrabTexture, float4(uv.x,         uv.y - step, uv.z, uv.w));
                sum += tex2Dproj(_GrabTexture, float4(uv.x + step, uv.y - step, uv.z, uv.w));

                sum += tex2Dproj(_GrabTexture, float4(uv.x - step, uv.y,         uv.z, uv.w));
                sum += tex2Dproj(_GrabTexture, float4(uv.x,         uv.y,         uv.z, uv.w)); // Center
                sum += tex2Dproj(_GrabTexture, float4(uv.x + step, uv.y,         uv.z, uv.w));

                sum += tex2Dproj(_GrabTexture, float4(uv.x - step, uv.y + step, uv.z, uv.w));
                sum += tex2Dproj(_GrabTexture, float4(uv.x,         uv.y + step, uv.z, uv.w));
                sum += tex2Dproj(_GrabTexture, float4(uv.x + step, uv.y + step, uv.z, uv.w));

                // Divide by 9 to get the average blurred pixel color
                fixed4 blurredBackground = sum / 9.0;

                // Mask the blur to the shape of your sprite (respecting transparency)
                fixed4 spriteTex = tex2D(_MainTex, IN.texcoord);
                blurredBackground.a *= spriteTex.a * IN.color.a;

                return blurredBackground * IN.color;
            }
            ENDCG
        }
    }
}
